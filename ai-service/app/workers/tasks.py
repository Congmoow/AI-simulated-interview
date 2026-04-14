import time

import httpx
from celery.exceptions import MaxRetriesExceededError
from celery.utils.log import get_task_logger

from app.core.settings import get_settings
from app.schemas.document import ProcessDocumentRequest
from app.services.dependencies import get_provider
from app.services.document_service import DocumentService
from app.workers.celery_app import celery_app

logger = get_task_logger(__name__)


@celery_app.task(name="report.generate")
def generate_report_task(interview_id: str) -> dict[str, str]:
    return {
        "interviewId": interview_id,
        "status": "accepted",
    }


def _send_callback_with_retry(
    url: str,
    payload: dict,
    headers: dict[str, str],
    max_retries: int = 3,
) -> None:
    last_exc: Exception | None = None
    for attempt in range(max_retries):
        try:
            with httpx.Client(timeout=30) as client:
                client.post(url, json=payload, headers=headers).raise_for_status()
            logger.info("Callback 发送成功 (attempt %d/%d)", attempt + 1, max_retries)
            return
        except Exception as exc:
            last_exc = exc
            if attempt < max_retries - 1:
                wait = 5 * (attempt + 1)
                logger.warning(
                    "Callback 失败 (attempt %d/%d): %s，%ds 后重试",
                    attempt + 1,
                    max_retries,
                    exc,
                    wait,
                )
                time.sleep(wait)
            else:
                logger.error("Callback 最终失败（%d 次已耗尽）: %s", max_retries, exc)
    raise last_exc  # 触发 Celery 失败语义，配合 acks_late 重入队


@celery_app.task(
    bind=True,
    name="knowledge.process_document",
    max_retries=3,
    default_retry_delay=30,
)
def process_knowledge_document_task(
    self,
    document_id: str,
    file_name: str,
    file_type: str,
    title: str,
) -> None:
    settings = get_settings()
    callback_url = (
        f"{settings.backend_url}/api/v1/internal/knowledge/documents/{document_id}/callback"
    )
    headers: dict[str, str] = {}
    if settings.api_key:
        headers["Authorization"] = f"Bearer {settings.api_key}"

    logger.info(
        "开始处理文档 %s (file=%s, attempt %d/%d)",
        document_id,
        file_name,
        self.request.retries + 1,
        self.max_retries + 1,
    )

    try:
        provider = get_provider()
        service = DocumentService(provider)
        result = service.process(
            ProcessDocumentRequest(
                documentId=document_id,
                fileName=file_name,
                fileType=file_type,
                title=title,
            )
        )
        payload: dict = {
            "status": "ready",
            "chunks": [c.model_dump(by_alias=True) for c in result.chunks],
            "error": None,
        }
        logger.info("文档 %s 处理完成，共 %d 个 chunks", document_id, len(result.chunks))
    except Exception as exc:
        logger.warning(
            "文档 %s 处理失败 (attempt %d/%d): %s",
            document_id,
            self.request.retries + 1,
            self.max_retries + 1,
            exc,
        )
        try:
            raise self.retry(exc=exc)
        except MaxRetriesExceededError:
            logger.error("文档 %s 重试次数耗尽，最终标记为 failed", document_id)
            payload = {
                "status": "failed",
                "chunks": [],
                "error": str(exc),
            }

    _send_callback_with_retry(callback_url, payload, headers)
