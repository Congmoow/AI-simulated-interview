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

    def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        return self.provider.recommend_resources(request)

    def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        return self.provider.generate_training_plan(request)
