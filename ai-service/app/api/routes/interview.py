import json
import logging

from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import StreamingResponse

from app.core.security import verify_internal_request
from app.schemas.interview import (
    AnswerInterviewRequest,
    AnswerInterviewResponse,
    StartInterviewRequest,
    StartInterviewResponse,
)
from app.services.dependencies import get_provider
from app.services.interview_service import InterviewService

router = APIRouter(dependencies=[Depends(verify_internal_request)])
logger = logging.getLogger(__name__)


@router.post("/start", response_model=StartInterviewResponse)
async def start_interview(request: StartInterviewRequest):
    provider = None
    try:
        provider = get_provider()
    except Exception as exc:
        logger.warning(
            "start_interview_provider_unavailable fallback_to_template=true exception_type=%s",
            exc.__class__.__name__,
        )

    service = InterviewService(provider)
    try:
        return await service.start(request)
    except Exception as exc:
        logger.exception(
            "start_interview_failed fallback_to_template=true exception_type=%s",
            exc.__class__.__name__,
        )
        raise HTTPException(status_code=503, detail="首题生成失败，请稍后重试。") from exc


@router.post("/answer", response_model=AnswerInterviewResponse)
async def answer_interview(request: AnswerInterviewRequest):
    try:
        provider = get_provider()
    except Exception as exc:
        logger.warning(
            "answer_interview_provider_unavailable exception_type=%s",
            exc.__class__.__name__,
        )
        raise HTTPException(status_code=503, detail="AI 服务不可用，请稍后重试。") from exc

    service = InterviewService(provider)
    try:
        return await service.answer(request)
    except HTTPException:
        raise
    except Exception as exc:
        logger.exception(
            "answer_interview_failed exception_type=%s",
            exc.__class__.__name__,
        )
        raise HTTPException(status_code=502, detail="回答处理失败，请稍后重试。") from exc


@router.post("/answer-stream")
async def answer_interview_stream(request: AnswerInterviewRequest):
    try:
        provider = get_provider()
    except Exception as exc:
        raise HTTPException(status_code=503, detail="AI 服务不可用。") from exc

    async def event_generator():
        try:
            async for event in provider.answer_interview_streaming(request):
                event_type = event.get("type", "chunk")
                if event_type == "chunk":
                    yield f"event: chunk\ndata: {json.dumps({'text': event.get('text', '')}, ensure_ascii=False)}\n\n"
                elif event_type == "done":
                    resp = event.get("response")
                    if resp is not None:
                        yield f"event: done\ndata: {resp.model_dump_json()}\n\n"
                    else:
                        yield f"event: done\ndata: {{}}\n\n"
                elif event_type == "error":
                    yield f"event: error\ndata: {json.dumps({'message': event.get('message', '')}, ensure_ascii=False)}\n\n"
        except Exception as exc:
            logger.exception("answer_stream_failed")
            yield f"event: error\ndata: {json.dumps({'message': str(exc)}, ensure_ascii=False)}\n\n"

    return StreamingResponse(event_generator(), media_type="text/event-stream")
