from typing import Protocol

from app.schemas.document import ProcessDocumentRequest, ProcessDocumentResponse
from app.schemas.interview import (
    AnswerInterviewRequest,
    AnswerInterviewResponse,
    ScoreInterviewRequest,
    ScoreInterviewResponse,
    StartInterviewRequest,
    StartInterviewResponse,
)
from app.schemas.rag import RagSearchRequest, RagSearchResponse
from app.schemas.recommendation import (
    ResourceRecommendationRequest,
    ResourceRecommendationResponse,
    TrainingPlanRequest,
    TrainingPlanResponse,
)
from app.schemas.report import GenerateReportRequest, GenerateReportResponse


class ModelProvider(Protocol):
    async def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        ...

    async def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        ...

    async def score_interview(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        ...

    async def generate_report(self, request: GenerateReportRequest) -> GenerateReportResponse:
        ...

    async def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        ...

    async def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        ...

    async def search_rag(self, request: RagSearchRequest) -> RagSearchResponse:
        ...

    async def process_document(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        ...
