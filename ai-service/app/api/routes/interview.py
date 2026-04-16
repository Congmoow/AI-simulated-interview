from fastapi import APIRouter, Depends

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


@router.post("/start", response_model=StartInterviewResponse)
async def start_interview(request: StartInterviewRequest):
    service = InterviewService(get_provider())
    return service.start(request)


@router.post("/answer", response_model=AnswerInterviewResponse)
async def answer_interview(request: AnswerInterviewRequest):
    service = InterviewService(get_provider())
    return service.answer(request)
