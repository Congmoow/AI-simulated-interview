from app.providers.base import ModelProvider
from app.schemas.interview import ScoreAndReportResponse, ScoreInterviewRequest, ScoreInterviewResponse


class EvaluationService:
    def __init__(self, provider: ModelProvider) -> None:
        self.provider = provider

    async def score(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        return await self.provider.score_interview(request)

    async def score_and_report(self, request: ScoreInterviewRequest) -> ScoreAndReportResponse:
        return await self.provider.score_and_report_interview(request)
