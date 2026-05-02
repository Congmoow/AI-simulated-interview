from fastapi import APIRouter, Depends

from app.core.security import verify_internal_request
from app.schemas.recommendation import (
    ResourceRecommendationRequest,
    ResourceRecommendationResponse,
    TrainingPlanRequest,
    TrainingPlanResponse,
)
from app.services.dependencies import get_provider
from app.services.recommendation_service import RecommendationService

router = APIRouter(dependencies=[Depends(verify_internal_request)])


@router.post("/resources", response_model=ResourceRecommendationResponse)
async def recommend_resources(request: ResourceRecommendationRequest):
    service = RecommendationService(get_provider())
    return await service.recommend_resources(request)


@router.post("/training-plan", response_model=TrainingPlanResponse)
async def generate_training_plan(request: TrainingPlanRequest):
    service = RecommendationService(get_provider())
    return await service.generate_training_plan(request)
