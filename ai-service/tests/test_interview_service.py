from uuid import uuid4

import pytest

from app.providers.openai_compatible_provider import ProviderCallError
from app.schemas.interview import (
    AnswerInterviewRequest,
    AnswerInterviewResponse,
    CandidateQuestion,
    CurrentMainQuestion,
    InterviewLimits,
    InterviewMessage,
    StartInterviewRequest,
    StartInterviewResponse,
)
from app.services.interview_service import InterviewService


def _build_candidate_question(question_id=None, title="介绍订单系统项目", type_="project"):
    return CandidateQuestion(
        questionId=question_id or uuid4(),
        title=title,
        type=type_,
        content=f"{title} 的具体背景与职责",
        difficulty="medium",
    )


def _build_limits() -> InterviewLimits:
    return InterviewLimits(
        maxMainQuestions=5,
        currentMainQuestionCount=0,
        maxMessages=30,
        currentMessageCount=0,
        maxDurationMinutes=30,
        currentDurationMinutes=0,
    )


def _build_start_request() -> StartInterviewRequest:
    return StartInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        positionName="Java 后端工程师",
        interviewMode="standard",
        questionTypes=["project", "technical"],
        questionBank=[
            _build_candidate_question(title="介绍订单系统项目"),
            _build_candidate_question(title="事务传播行为", type_="technical"),
        ],
        askedQuestionIds=[],
        currentMainQuestion=None,
        recentMessages=[],
        historyAnswerSummaries=[],
        limits=_build_limits(),
    )


def _build_answer_request() -> AnswerInterviewRequest:
    current_question = _build_candidate_question(title="介绍订单系统项目")
    return AnswerInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        positionName="Java 后端工程师",
        interviewMode="standard",
        questionBank=[
            current_question,
            _build_candidate_question(title="事务传播行为", type_="technical"),
        ],
        askedQuestionIds=[current_question.question_id],
        currentMainQuestion=CurrentMainQuestion(
            roundNumber=1,
            questionId=current_question.question_id,
            title=current_question.title,
            type=current_question.type,
            askedContent="先请你介绍一个最相关的后端项目。",
            followUpCount=1,
        ),
        recentMessages=[
            InterviewMessage(
                role="assistant",
                messageType="opening",
                content="先请你介绍一个最相关的后端项目。",
                relatedQuestionId=current_question.question_id,
                sequence=1,
            ),
            InterviewMessage(
                role="user",
                messageType="answer",
                content="我主要负责订单和库存一致性。",
                relatedQuestionId=current_question.question_id,
                sequence=2,
            ),
        ],
        historyAnswerSummaries=["第1题：介绍订单系统项目；回答：我主要负责订单和库存一致性。"],
        limits=InterviewLimits(
            maxMainQuestions=5,
            currentMainQuestionCount=1,
            maxMessages=30,
            currentMessageCount=2,
            maxDurationMinutes=30,
            currentDurationMinutes=8,
        ),
    )


class _SuccessProvider:
    def __init__(self, start_response: StartInterviewResponse | None = None, answer_response: AnswerInterviewResponse | None = None) -> None:
        self.start_response = start_response
        self.answer_response = answer_response

    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        return self.start_response or StartInterviewResponse(
            action="question",
            messageType="opening",
            content="请先介绍一个最相关的后端项目。",
            selectedQuestionId=request.question_bank[0].question_id,
            suggestions=["先讲背景"],
        )

    def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        return self.answer_response or AnswerInterviewResponse(
            action="follow_up",
            messageType="follow_up",
            content="你刚才提到库存一致性，能具体展开补偿和对账策略吗？",
            suggestions=["结合一次故障说明"],
        )


class _TimeoutProvider:
    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        raise ProviderCallError("start_interview timeout", timeout_seconds=12.0)


class _InvalidPayloadProvider:
    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        raise ValueError("invalid start interview payload")


def test_start_should_return_provider_response_when_upstream_succeeds() -> None:
    request = _build_start_request()
    expected = StartInterviewResponse(
        action="question",
        messageType="opening",
        content="请先介绍一个最相关的后端项目。",
        selectedQuestionId=request.question_bank[0].question_id,
        suggestions=["先讲背景"],
    )
    service = InterviewService(_SuccessProvider(start_response=expected))

    result = service.start(request)

    assert result == expected


def test_start_should_fallback_to_first_unasked_question_when_upstream_times_out() -> None:
    request = _build_start_request()
    service = InterviewService(_TimeoutProvider())

    result = service.start(request)

    assert result.action == "question"
    assert result.message_type == "opening"
    assert result.selected_question_id == request.question_bank[0].question_id
    assert result.content


def test_start_should_skip_already_asked_question_in_fallback() -> None:
    request = _build_start_request()
    request.asked_question_ids = [request.question_bank[0].question_id]
    service = InterviewService(_InvalidPayloadProvider())

    result = service.start(request)

    assert result.action == "question"
    assert result.selected_question_id == request.question_bank[1].question_id


def test_answer_should_return_provider_decision_with_new_shape() -> None:
    request = _build_answer_request()
    expected = AnswerInterviewResponse(
        action="question",
        messageType="question",
        content="下面切到事务设计，你通常如何选择 Spring 事务传播行为？",
        selectedQuestionId=request.question_bank[1].question_id,
        suggestions=["结合真实接口说明"],
    )
    service = InterviewService(_SuccessProvider(answer_response=expected))

    result = service.answer(request)

    assert result == expected


def test_start_should_raise_only_when_upstream_and_local_fallback_both_fail(monkeypatch) -> None:
    request = _build_start_request()
    service = InterviewService(_TimeoutProvider())
    monkeypatch.setattr(service, "_build_fallback_start_response", lambda _: (_ for _ in ()).throw(RuntimeError("fallback broken")))

    with pytest.raises(RuntimeError, match="fallback broken"):
        service.start(request)
