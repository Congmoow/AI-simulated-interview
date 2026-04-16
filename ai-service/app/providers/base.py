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
    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        ...

    def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        ...

    def score_interview(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        ...

    def generate_report(self, request: GenerateReportRequest) -> GenerateReportResponse:
        ...

    def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        ...

    def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        ...

    def search_rag(self, request: RagSearchRequest) -> RagSearchResponse:
        ...

    def process_document(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        ...
