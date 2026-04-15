import logging
from uuid import uuid4

import httpx
import pytest

from app.providers.openai_compatible_provider import OpenAICompatibleProvider, ProviderCallError
from app.schemas.interview import (
    AnswerInterviewRequest,
    CandidateQuestion,
    DimensionScore,
    FinishInterviewRequest,
    ScoreInterviewRequest,
    ScoreRound,
    StartInterviewRequest,
)
from app.schemas.report import GenerateReportRequest
from app.schemas.recommendation import ResourceRecommendationRequest, TrainingPlanRequest
from app.schemas.rag import RagSearchRequest
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
        title="介绍项目",
        type="project",
        content="请介绍你做过的项目",
        difficulty="medium",
    )


def _build_start_request() -> StartInterviewRequest:
    return StartInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        interviewMode="standard",
        roundNumber=1,
        questionTypes=["project"],
        sourceQuestion=_build_candidate_question(),
    )


def _build_answer_request() -> AnswerInterviewRequest:
    return AnswerInterviewRequest(
        interviewId=uuid4(),
        roundNumber=1,
        interviewMode="standard",
        positionCode="java-backend",
        questionTitle="介绍项目",
        questionContent="请介绍你做过的项目",
        answer="我负责订单系统。",
        followUpCount=0,
        currentRound=1,
        totalRounds=3,
        nextQuestionCandidate=_build_candidate_question(),
    )


def _build_finish_request() -> FinishInterviewRequest:
    return FinishInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        totalRounds=3,
    )


def _build_score_request() -> ScoreInterviewRequest:
    return ScoreInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        rounds=[
            ScoreRound(
                roundNumber=1,
                questionType="project",
                questionTitle="介绍项目",
                questionContent="请介绍你做过的项目",
                answer="我负责订单系统。",
                followUps=["请继续说明性能优化。"],
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
        rounds=[
            ScoreRound(
                roundNumber=1,
                questionType="project",
                questionTitle="介绍项目",
                questionContent="请介绍你做过的项目",
                answer="我负责订单系统。",
                followUps=["请继续说明性能优化。"],
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
                weaknesses=["技术深度"],
            )
        )

    with pytest.raises(NotImplementedError):
        provider.generate_training_plan(
            TrainingPlanRequest(
                interviewId=interview_id,
                positionCode="java-backend",
                weaknesses=["表达清晰度"],
            )
        )

    with pytest.raises(NotImplementedError):
        provider.search_rag(
            RagSearchRequest(
                query="Spring 事务传播",
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
            "executiveSummary": "总结",
            "strengths": ["优点1"],
            "weaknesses": ["不足1"],
            "detailedAnalysis": {"technicalAccuracy": "分析"},
            "learningSuggestions": ["建议1"],
            "trainingPlan": [{"week": 1, "focus": "基础"}],
            "nextInterviewFocus": ["项目深挖"],
        },
    )

    response = provider.generate_report(_build_report_request())

    assert response.executive_summary == "总结"
    assert response.strengths == ["优点1"]
    assert response.model_version == "qwen:qwen-plus"


def test_score_and_report_should_use_explicit_75s_timeout_and_other_steps_keep_default(monkeypatch) -> None:
    provider = _build_provider()
    calls: list[tuple[str, float]] = []

    def fake_chat_text(
        *,
        step: str,
        system_prompt: str,
        user_prompt: str,
        temperature: float,
        max_tokens: int,
        timeout_seconds: float = 30.0,
    ) -> str:
        calls.append((step, timeout_seconds))
        if step == "start_interview":
            return '{"title":"项目介绍","content":"请介绍你负责的项目","suggestions":["先讲背景"]}'
        if step == "answer_interview":
            return '{"decision":"follow_up","content":"请继续补充细节","suggestions":["讲清指标"]}'
        if step == "finish_interview":
            return "面试结束。"
        if step == "score_interview":
            return '{"overallScore":85,"rankPercentile":88,"dimensionScores":{"technicalAccuracy":82,"knowledgeDepth":80,"logicalThinking":83,"positionMatch":84,"projectAuthenticity":81,"fluency":86,"clarity":85,"confidence":84},"dimensionDetails":{"technicalAccuracy":"稳定"},"scoreBreakdown":{}}'
        if step == "generate_report":
            return '{"executiveSummary":"总结","strengths":["优点"],"weaknesses":["不足"],"detailedAnalysis":{"technicalAccuracy":"分析"},"learningSuggestions":["建议"],"trainingPlan":[],"nextInterviewFocus":["项目深挖"]}'
        raise AssertionError(step)

    monkeypatch.setattr(provider, "_chat_text", fake_chat_text)

    provider.start_interview(_build_start_request())
    provider.answer_interview(_build_answer_request())
    provider.finish_interview(_build_finish_request())
    provider.score_interview(_build_score_request())
    provider.generate_report(_build_report_request())

    assert calls == [
        ("start_interview", 30.0),
        ("answer_interview", 30.0),
        ("finish_interview", 30.0),
        ("score_interview", 75.0),
        ("generate_report", 75.0),
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
                questionTitle="题目" + ("A" * 400),
                questionContent="内容" + ("B" * 1000),
                answer="回答" + ("C" * 1600),
                followUps=[
                    "追问1" + ("D" * 300),
                    "追问2" + ("E" * 300),
                    "追问3" + ("F" * 300),
                ],
            )
            for index in range(6)
        ],
    )

    summary = provider._build_score_rounds_text(request.rounds)

    assert len(summary) <= 2400
    assert "追问1" not in summary
    assert ("[TRUNCATED]" in summary) or (len(summary) < 2400)


def test_build_report_rounds_text_should_trim_fields_and_total_length() -> None:
    provider = _build_provider()
    request = GenerateReportRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        overallScore=88,
        dimensionScores={},
        rounds=[
            ScoreRound(
                roundNumber=index + 1,
                questionType="scenario",
                questionTitle="题目" + ("A" * 400),
                questionContent="内容" + ("B" * 1000),
                answer="回答" + ("C" * 1600),
                followUps=[
                    "追问1" + ("D" * 300),
                    "追问2" + ("E" * 300),
                    "追问3" + ("F" * 300),
                ],
            )
            for index in range(6)
        ],
    )

    summary = provider._build_report_rounds_text(request.rounds)

    assert len(summary) <= 2400
    assert "追问1" not in summary
    assert ("[TRUNCATED]" in summary) or (len(summary) < 2400)


def test_generate_report_should_normalize_non_core_fields(monkeypatch) -> None:
    provider = _build_provider()

    monkeypatch.setattr(
        provider,
        "_chat_json",
        lambda **_: {
            "summary": "这是一段总结",
            "strengths": "结构清晰",
            "weaknesses": ["细节略少"],
            "learningSuggestions": "补充指标",
            "detailedAnalysis": ["分析项1", "分析项2"],
            "trainingPlan": {"week": 1, "topic": "缓存"},
            "nextInterviewFocus": "一致性设计",
        },
    )

    response = provider.generate_report(_build_report_request())

    assert response.executive_summary == "这是一段总结"
    assert response.strengths == ["结构清晰"]
    assert response.weaknesses == ["细节略少"]
    assert response.learning_suggestions == ["补充指标"]
    assert response.detailed_analysis == {"items": ["分析项1", "分析项2"]}
    assert response.training_plan == [{"week": 1, "topic": "缓存"}]
    assert response.next_interview_focus == ["一致性设计"]


def test_generate_report_should_accept_minimal_report_payload(monkeypatch) -> None:
    provider = _build_provider()

    monkeypatch.setattr(
        provider,
        "_chat_json",
        lambda **_: {
            "executiveSummary": "最小总结",
            "strengths": ["优点"],
            "weaknesses": ["不足"],
            "learningSuggestions": ["建议"],
        },
    )

    response = provider.generate_report(_build_report_request())

    assert response.executive_summary == "最小总结"
    assert response.strengths == ["优点"]
    assert response.weaknesses == ["不足"]
    assert response.learning_suggestions == ["建议"]
    assert response.detailed_analysis == {}
    assert response.training_plan == []
    assert response.next_interview_focus == []


def test_score_interview_should_log_observability_fields_on_timeout(monkeypatch, caplog) -> None:
    provider = _build_provider()

    timeout_error = httpx.ReadTimeout("timed out")

    def raise_provider_call_error(**_: object) -> dict[str, object]:
        raise ProviderCallError(
            "score_interview 请求上游失败",
            timeout_seconds=75.0,
            elapsed_ms=31234.0,
            received_response_headers=False,
        ) from timeout_error

    monkeypatch.setattr(provider, "_chat_json", raise_provider_call_error)

    with caplog.at_level(logging.ERROR):
        with pytest.raises(Exception):
            provider.score_interview(_build_score_request())

    message = "\n".join(caplog.messages)
    assert "round_count=1" in message
    assert "timeout_seconds=75.0" in message
    assert "received_response_headers=False" in message
    assert "input_summary_length=" in message
    assert "inner_exception=ReadTimeout" in message


def test_generate_report_should_log_observability_fields_on_timeout(monkeypatch, caplog) -> None:
    provider = _build_provider()

    timeout_error = httpx.ReadTimeout("timed out")

    def raise_provider_call_error(**_: object) -> dict[str, object]:
        raise ProviderCallError(
            "generate_report 请求上游失败",
            timeout_seconds=75.0,
            elapsed_ms=40123.0,
            received_response_headers=False,
        ) from timeout_error

    monkeypatch.setattr(provider, "_chat_json", raise_provider_call_error)

    with caplog.at_level(logging.ERROR):
        with pytest.raises(Exception):
            provider.generate_report(_build_report_request())

    message = "\n".join(caplog.messages)
    assert "step=generate_report" in message
    assert "timeout_seconds=75.0" in message
    assert "input_summary_length=" in message
    assert "inner_exception=ReadTimeout" in message
    assert "response_body_snippet=" in message
