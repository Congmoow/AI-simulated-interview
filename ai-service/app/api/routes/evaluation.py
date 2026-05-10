from fastapi import APIRouter, Depends

from app.core.security import verify_internal_request
from app.schemas.interview import ScoreAndReportResponse, ScoreInterviewRequest, ScoreInterviewResponse
from app.services.dependencies import get_provider
from app.services.evaluation_service import EvaluationService

router = APIRouter(dependencies=[Depends(verify_internal_request)])


@router.post("/score", response_model=ScoreInterviewResponse)
async def score_interview(request: ScoreInterviewRequest):
    service = EvaluationService(get_provider())
    return await service.score(request)


@router.post("/score-and-report", response_model=ScoreAndReportResponse)
async def score_and_report(request: ScoreInterviewRequest):
    service = EvaluationService(get_provider())
    return await service.score_and_report(request)
