from app.providers.base import ModelProvider
from app.schemas.interview import ScoreInterviewRequest, ScoreInterviewResponse


class EvaluationService:
    def __init__(self, provider: ModelProvider) -> None:
        self.provider = provider

    def score(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        return self.provider.score_interview(request)
