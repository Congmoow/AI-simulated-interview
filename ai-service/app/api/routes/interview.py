import logging

from fastapi import APIRouter, Depends, HTTPException

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
        return service.start(request)
    except Exception as exc:
        logger.exception(
            "start_interview_failed fallback_to_template=true exception_type=%s",
            exc.__class__.__name__,
        )
        raise HTTPException(status_code=503, detail="首题生成失败，请稍后重试。") from exc


@router.post("/answer", response_model=AnswerInterviewResponse)
async def answer_interview(request: AnswerInterviewRequest):
    service = InterviewService(get_provider())
    return service.answer(request)
