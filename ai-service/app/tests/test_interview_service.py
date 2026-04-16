import pytest

from app.providers.openai_compatible_provider import ProviderCallError
from app.schemas.interview import (
    CandidateQuestion,
    StartInterviewRequest,
    StartInterviewResponse,
)
from app.services.interview_service import InterviewService


def _build_start_request() -> StartInterviewRequest:
    return StartInterviewRequest(
        interviewId="550e8400-e29b-41d4-a716-446655440010",
        positionCode="java-backend",
        interviewMode="standard",
        roundNumber=1,
        questionTypes=["project"],
        sourceQuestion=CandidateQuestion(
            questionId="550e8400-e29b-41d4-a716-446655440011",
            title="Introduce your project",
            type="project",
            content="Describe your most relevant project",
            difficulty="medium",
        ),
    )


class _SuccessProvider:
    def __init__(self, response: StartInterviewResponse) -> None:
        self.response = response

    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        return self.response


class _TimeoutProvider:
    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        raise ProviderCallError("start_interview timeout", timeout_seconds=12.0)


class _InvalidPayloadProvider:
    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        raise ValueError("invalid start interview payload")


def test_start_should_return_provider_response_when_upstream_succeeds() -> None:
    request = _build_start_request()
    expected = StartInterviewResponse(
        questionId=request.source_question.question_id,
        title="Project intro",
        type="project",
        content="Tell me about your order system",
        suggestions=["Start with scope"],
    )
    service = InterviewService(_SuccessProvider(expected))

    result = service.start(request)

    assert result == expected


def test_start_should_fallback_to_local_question_when_upstream_times_out() -> None:
    request = _build_start_request()
    service = InterviewService(_TimeoutProvider())

    result = service.start(request)

    assert result.question_id == request.source_question.question_id
    assert result.title
    assert result.content
    assert result.type
    assert isinstance(result.suggestions, list)


def test_start_should_fallback_to_local_question_when_upstream_returns_invalid_payload() -> None:
    request = _build_start_request()
    service = InterviewService(_InvalidPayloadProvider())

    result = service.start(request)

    assert result.question_id == request.source_question.question_id
    assert result.title
    assert result.content
    assert result.type


def test_start_should_raise_only_when_upstream_and_local_fallback_both_fail(monkeypatch) -> None:
    request = _build_start_request()
    service = InterviewService(_TimeoutProvider())
    monkeypatch.setattr(service, "_build_fallback_start_response", lambda _: (_ for _ in ()).throw(RuntimeError("fallback broken")))

    with pytest.raises(RuntimeError, match="fallback broken"):
        service.start(request)


def test_start_fallback_should_keep_same_dto_shape_as_normal_path() -> None:
    request = _build_start_request()
    service = InterviewService(_TimeoutProvider())

    result = service.start(request)

    assert isinstance(result, StartInterviewResponse)
    assert result.question_id
    assert isinstance(result.title, str)
    assert isinstance(result.type, str)
    assert isinstance(result.content, str)
    assert isinstance(result.suggestions, list)
