import logging
from uuid import uuid4

import httpx
import pytest

from app.providers.openai_compatible_provider import OpenAICompatibleProvider, ProviderCallError
from app.schemas.interview import (
    AnswerInterviewRequest,
    CandidateQuestion,
    CurrentMainQuestion,
    DimensionScore,
    InterviewLimits,
    InterviewMessage,
    ScoreInterviewRequest,
    ScoreRound,
    StartInterviewRequest,
)
from app.schemas.rag import RagSearchRequest
from app.schemas.recommendation import ResourceRecommendationRequest, TrainingPlanRequest
from app.schemas.report import GenerateReportRequest
from app.services.backend_ai_settings import RuntimeAiSettings


def _build_provider() -> OpenAICompatibleProvider:
    return OpenAICompatibleProvider(
        RuntimeAiSettings(
            provider="qwen",
            baseUrl="https://example.com/v1",
            model="qwen-plus",
            apiKey="secret-key",
            temperature=0.3,
            maxTokens=512,
            systemPrompt="test",
        )
    )


def _build_candidate_question() -> CandidateQuestion:
    return CandidateQuestion(
        questionId=uuid4(),
        title="Introduce your project",
        type="project",
        content="Describe an order system you built",
        difficulty="medium",
    )


def _build_start_request() -> StartInterviewRequest:
    return StartInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        positionName="Java 后端工程师",
        interviewMode="standard",
        questionTypes=["project"],
        questionBank=[_build_candidate_question()],
        askedQuestionIds=[],
        currentMainQuestion=None,
        recentMessages=[],
        historyAnswerSummaries=[],
        limits=InterviewLimits(
            maxMainQuestions=5,
            currentMainQuestionCount=0,
            maxMessages=30,
            currentMessageCount=0,
            maxDurationMinutes=30,
            currentDurationMinutes=0,
        ),
    )


def _build_answer_request() -> AnswerInterviewRequest:
    current_question = _build_candidate_question()
    return AnswerInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        positionName="Java 后端工程师",
        interviewMode="standard",
        questionBank=[
            current_question,
            _build_candidate_question(),
        ],
        askedQuestionIds=[current_question.question_id],
        currentMainQuestion=CurrentMainQuestion(
            roundNumber=1,
            questionId=current_question.question_id,
            title=current_question.title,
            type=current_question.type,
            askedContent="请先介绍一个最相关的后端项目。",
            followUpCount=0,
        ),
        recentMessages=[
            InterviewMessage(
                role="assistant",
                messageType="opening",
                content="请先介绍一个最相关的后端项目。",
                relatedQuestionId=current_question.question_id,
                sequence=1,
            ),
            InterviewMessage(
                role="user",
                messageType="answer",
                content="I owned ordering and inventory consistency",
                relatedQuestionId=current_question.question_id,
                sequence=2,
            ),
        ],
        historyAnswerSummaries=["第1题：Introduce your project；回答：I owned ordering and inventory consistency"],
        limits=InterviewLimits(
            maxMainQuestions=3,
            currentMainQuestionCount=1,
            maxMessages=30,
            currentMessageCount=2,
            maxDurationMinutes=30,
            currentDurationMinutes=8,
        ),
    )


def _build_score_request() -> ScoreInterviewRequest:
    return ScoreInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        rounds=[
            ScoreRound(
                roundNumber=1,
                questionType="project",
                questionTitle="Introduce your project",
                questionContent="Describe an order system you built",
                answer="I owned ordering and inventory consistency",
                followUps=["Show load test details", "Explain retry strategy", "Share metrics"],
            )
        ],
    )


def _build_report_request() -> GenerateReportRequest:
    return GenerateReportRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        overallScore=82.5,
        dimensionScores={
            "technicalAccuracy": DimensionScore(score=80, weight=0.3),
        },
        dimensionDetails={
            "technicalAccuracy": "Strong fundamentals but should add more metrics.",
        },
        rounds=[
            ScoreRound(
                roundNumber=1,
                questionType="project",
                questionTitle="Introduce your project",
                questionContent="Describe an order system you built",
                answer="I owned ordering and inventory consistency",
                followUps=["Show load test details", "Explain retry strategy", "Share metrics"],
            )
        ],
    )


def test_secondary_methods_should_raise_not_implemented() -> None:
    provider = _build_provider()
    interview_id = uuid4()

    with pytest.raises(NotImplementedError):
        provider.recommend_resources(
            ResourceRecommendationRequest(
                interviewId=interview_id,
                positionCode="java-backend",
                weaknesses=["depth"],
            )
        )

    with pytest.raises(NotImplementedError):
        provider.generate_training_plan(
            TrainingPlanRequest(
                interviewId=interview_id,
                positionCode="java-backend",
                weaknesses=["clarity"],
            )
        )

    with pytest.raises(NotImplementedError):
        provider.search_rag(
            RagSearchRequest(
                query="Spring transaction propagation",
                positionCode="java-backend",
                topK=3,
            )
        )


def test_generate_report_should_use_real_ai_response(monkeypatch) -> None:
    provider = _build_provider()

    monkeypatch.setattr(
        provider,
        "_chat_json",
        lambda **_: {
            "executiveSummary": "summary",
            "strengths": ["strength-1"],
            "weaknesses": ["weakness-1"],
            "detailedAnalysis": {"technicalAccuracy": "analysis"},
            "learningSuggestions": ["suggestion-1"],
            "trainingPlan": [{"week": 1, "focus": "basics"}],
            "nextInterviewFocus": ["go deeper on metrics"],
        },
    )

    response = provider.generate_report(_build_report_request())

    assert response.executive_summary == "summary"
    assert response.strengths == ["strength-1"]
    assert response.model_version == "qwen:qwen-plus"


def test_score_and_report_should_use_new_timeouts_and_skip_finish_step(monkeypatch) -> None:
    provider = _build_provider()
    calls: list[tuple[str, float, int]] = []
    start_request = _build_start_request()
    answer_request = _build_answer_request()

    def fake_chat_text(
        *,
        step: str,
        system_prompt: str,
        user_prompt: str,
        temperature: float,
        max_tokens: int,
        timeout_seconds: float = 30.0,
    ) -> str:
        calls.append((step, timeout_seconds, max_tokens))
        if step == "start_interview":
            return '{"action":"question","messageType":"opening","content":"Tell me about your system","selectedQuestionId":"' + str(start_request.question_bank[0].question_id) + '","suggestions":["start with scope"]}'
        if step == "answer_interview":
            return '{"action":"follow_up","messageType":"follow_up","content":"Add more details","suggestions":["mention metrics"]}'
        if step == "score_interview":
            return '{"overallScore":85,"rankPercentile":88,"dimensionScores":{"technicalAccuracy":82,"knowledgeDepth":80,"logicalThinking":83,"positionMatch":84,"projectAuthenticity":81,"fluency":86,"clarity":85,"confidence":84},"dimensionDetails":{"technicalAccuracy":"stable"},"scoreBreakdown":{}}'
        if step == "generate_report":
            return '{"executiveSummary":"summary","strengths":["strength"],"weaknesses":["weakness"],"detailedAnalysis":{"technicalAccuracy":"analysis"},"learningSuggestions":["suggestion"],"trainingPlan":[],"nextInterviewFocus":["go deeper"]}'
        raise AssertionError(step)

    monkeypatch.setattr(provider, "_chat_text", fake_chat_text)

    provider.start_interview(start_request)
    provider.answer_interview(answer_request)
    provider.score_interview(_build_score_request())
    provider.generate_report(_build_report_request())

    assert calls == [
        ("start_interview", 25.0, 220),
        ("answer_interview", 60.0, 220),
        ("score_interview", 45.0, 512),
        ("generate_report", 60.0, 600),
    ]


def test_build_score_rounds_text_should_trim_fields_and_total_length() -> None:
    provider = _build_provider()
    request = ScoreInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        rounds=[
            ScoreRound(
                roundNumber=index + 1,
                questionType="scenario",
                questionTitle="Question" + ("A" * 400),
                questionContent="Content" + ("B" * 1000),
                answer="Answer" + ("C" * 1600),
                followUps=[
                    "Follow-up-1" + ("D" * 300),
                    "Follow-up-2" + ("E" * 300),
                    "Follow-up-3" + ("F" * 300),
                ],
            )
            for index in range(6)
        ],
    )

    summary = provider._build_score_rounds_text(request.rounds)

    assert len(summary) <= 1800
    assert "Follow-up-1" not in summary
    assert ("[TRUNCATED]" in summary) or (len(summary) < 1800)


def test_build_report_rounds_text_should_trim_fields_and_total_length() -> None:
    provider = _build_provider()
    request = GenerateReportRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        overallScore=88,
        dimensionScores={},
        dimensionDetails={},
        rounds=[
            ScoreRound(
                roundNumber=index + 1,
                questionType="scenario",
                questionTitle="Question" + ("A" * 400),
                questionContent="Content" + ("B" * 1000),
                answer="Answer" + ("C" * 1600),
                followUps=[
                    "Follow-up-1" + ("D" * 300),
                    "Follow-up-2" + ("E" * 300),
                    "Follow-up-3" + ("F" * 300),
                ],
            )
            for index in range(6)
        ],
    )

    summary = provider._build_report_rounds_text(request.rounds)

    assert len(summary) <= 1200
    assert "Follow-up-1" not in summary
    assert "Follow-up-2" not in summary
    assert ("Follow-up-3" in summary) or ("[TRUNCATED]" in summary)
    assert ("[TRUNCATED]" in summary) or (len(summary) < 1200)


def test_generate_report_should_normalize_non_core_fields(monkeypatch) -> None:
    provider = _build_provider()

    monkeypatch.setattr(
        provider,
        "_chat_json",
        lambda **_: {
            "summary": "summary from fallback key",
            "strengths": "clear structure",
            "weaknesses": ["not enough detail"],
            "learningSuggestions": "add metrics",
            "detailedAnalysis": ["analysis-item-1", "analysis-item-2"],
            "trainingPlan": {"week": 1, "topic": "cache"},
            "nextInterviewFocus": "consistency design",
        },
    )

    response = provider.generate_report(_build_report_request())

    assert response.executive_summary == "summary from fallback key"
    assert response.strengths == ["clear structure"]
    assert response.weaknesses == ["not enough detail"]
    assert response.learning_suggestions == ["add metrics"]
    assert response.detailed_analysis == {"items": ["analysis-item-1", "analysis-item-2"]}
    assert response.training_plan == [{"week": 1, "topic": "cache"}]
    assert response.next_interview_focus == ["consistency design"]


def test_generate_report_should_accept_minimal_report_payload(monkeypatch) -> None:
    provider = _build_provider()

    monkeypatch.setattr(
        provider,
        "_chat_json",
        lambda **_: {
            "executiveSummary": "minimal summary",
            "strengths": ["strength"],
            "weaknesses": ["weakness"],
            "learningSuggestions": ["suggestion"],
        },
    )

    response = provider.generate_report(_build_report_request())

    assert response.executive_summary == "minimal summary"
    assert response.strengths == ["strength"]
    assert response.weaknesses == ["weakness"]
    assert response.learning_suggestions == ["suggestion"]
    assert response.detailed_analysis == {}
    assert response.training_plan == []
    assert response.next_interview_focus == []


def test_score_interview_should_log_observability_fields_on_timeout(monkeypatch, caplog) -> None:
    provider = _build_provider()

    timeout_error = httpx.ReadTimeout("timed out")

    def raise_provider_call_error(**_: object) -> dict[str, object]:
        raise ProviderCallError(
            "score_interview request failed",
            timeout_seconds=45.0,
            elapsed_ms=31234.0,
            received_response_headers=False,
        ) from timeout_error

    monkeypatch.setattr(provider, "_chat_json", raise_provider_call_error)

    with caplog.at_level(logging.ERROR):
        with pytest.raises(Exception):
            provider.score_interview(_build_score_request())

    message = "\n".join(caplog.messages)
    assert "round_count=1" in message
    assert "timeout_seconds=45.0" in message
    assert "received_response_headers=False" in message
    assert "input_summary_length=" in message
    assert "inner_exception=ReadTimeout" in message


def test_generate_report_should_log_observability_fields_on_timeout(monkeypatch, caplog) -> None:
    provider = _build_provider()

    timeout_error = httpx.ReadTimeout("timed out")

    def raise_provider_call_error(**_: object) -> dict[str, object]:
        raise ProviderCallError(
            "generate_report request failed",
            timeout_seconds=60.0,
            elapsed_ms=40123.0,
            received_response_headers=False,
        ) from timeout_error

    monkeypatch.setattr(provider, "_chat_json", raise_provider_call_error)

    with caplog.at_level(logging.ERROR):
        with pytest.raises(Exception):
            provider.generate_report(_build_report_request())

    message = "\n".join(caplog.messages)
    assert "step=generate_report" in message
    assert "timeout_seconds=60.0" in message
    assert "input_summary_length=" in message
    assert "inner_exception=ReadTimeout" in message
    assert "response_body_snippet=" in message


def test_chat_text_should_reuse_shared_http_client(monkeypatch) -> None:
    provider = _build_provider()
    created_clients: list[tuple[str, str]] = []

    class FakeResponse:
        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict:
            return {"choices": [{"message": {"content": "ok"}}]}

    class FakeClient:
        def post(self, *args, **kwargs) -> FakeResponse:
            return FakeResponse()

    def fake_get_client(base_url: str, api_key: str) -> FakeClient:
        created_clients.append((base_url, api_key))
        return FakeClient()

    monkeypatch.setattr(
        "app.providers.openai_compatible_provider.get_shared_http_client",
        fake_get_client,
    )

    provider._chat_text(
        step="score_interview",
        system_prompt="system",
        user_prompt="user",
        temperature=0.2,
        max_tokens=128,
        timeout_seconds=45.0,
    )
    provider._chat_text(
        step="generate_report",
        system_prompt="system",
        user_prompt="user",
        temperature=0.2,
        max_tokens=128,
        timeout_seconds=60.0,
    )

    assert created_clients == [("https://example.com/v1", "secret-key")]
