from app.providers.base import ModelProvider
from app.schemas.recommendation import (
    ResourceRecommendationRequest,
    ResourceRecommendationResponse,
    TrainingPlanRequest,
    TrainingPlanResponse,
)


class RecommendationService:
    def __init__(self, provider: ModelProvider) -> None:
        self.provider = provider

    async def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        return await self.provider.recommend_resources(request)

    async def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        return await self.provider.generate_training_plan(request)
