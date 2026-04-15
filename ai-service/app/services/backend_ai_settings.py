from __future__ import annotations

import logging

from pydantic import BaseModel, Field
import httpx

from app.core.settings import get_settings

logger = logging.getLogger(__name__)


class RuntimeAiSettings(BaseModel):
    provider: str
    base_url: str = Field(alias="baseUrl")
    model: str
    api_key: str = Field(alias="apiKey")
    temperature: float = 0.7
    max_tokens: int = Field(alias="maxTokens", default=2048)
    system_prompt: str = Field(alias="systemPrompt", default="")


def _summarize_text(text: str, limit: int = 240) -> str:
    compact = " ".join(text.split())
    return compact[:limit]


def fetch_runtime_ai_settings() -> RuntimeAiSettings | None:
    settings = get_settings()
    headers: dict[str, str] = {}
    if settings.api_key:
        headers["Authorization"] = f"Bearer {settings.api_key}"

    url = f"{settings.backend_url.rstrip('/')}/api/v1/internal/ai/runtime-settings"
    try:
        with httpx.Client(timeout=10.0, http2=False) as client:
            response = client.get(url, headers=headers)
            response.raise_for_status()
            payload = response.json()
    except httpx.HTTPStatusError as exc:
        response_body = _summarize_text(exc.response.text) if exc.response is not None else ""
        logger.exception(
            "读取 runtime settings 失败：upstream_status_code=%s exception_type=%s response_body_snippet=%s",
            exc.response.status_code if exc.response is not None else "n/a",
            exc.__class__.__name__,
            response_body,
        )
        raise
    except httpx.RequestError as exc:
        logger.exception(
            "读取 runtime settings 请求失败：backend_url=%s exception_type=%s",
            settings.backend_url,
            exc.__class__.__name__,
        )
        raise
    except Exception as exc:
        logger.exception(
            "解析 runtime settings 失败：backend_url=%s exception_type=%s",
            settings.backend_url,
            exc.__class__.__name__,
        )
        raise

    data = payload.get("data")
    if not data:
        logger.warning("runtime settings 读取成功但 data 为空：backend_url=%s", settings.backend_url)
        return None

    runtime_settings = RuntimeAiSettings.model_validate(data)
    logger.info(
        "runtime settings 读取成功：provider=%s base_url=%s model=%s",
        runtime_settings.provider,
        runtime_settings.base_url,
        runtime_settings.model,
    )
    return runtime_settings
