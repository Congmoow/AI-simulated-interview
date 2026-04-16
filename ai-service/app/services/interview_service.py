from app.providers.base import ModelProvider
from app.schemas.interview import (
    AnswerInterviewRequest,
    AnswerInterviewResponse,
    StartInterviewRequest,
    StartInterviewResponse,
)


class InterviewService:
    def __init__(self, provider: ModelProvider) -> None:
        self.provider = provider

    def start(self, request: StartInterviewRequest) -> StartInterviewResponse:
        return self.provider.start_interview(request)

    def answer(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        return self.provider.answer_interview(request)
