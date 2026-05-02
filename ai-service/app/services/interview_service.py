from __future__ import annotations

import logging

from app.providers.base import ModelProvider
from app.providers.openai_compatible_provider import ProviderCallError
from app.schemas.interview import (
    AnswerInterviewRequest,
    AnswerInterviewResponse,
    CandidateQuestion,
    StartInterviewRequest,
    StartInterviewResponse,
)

logger = logging.getLogger(__name__)


class InterviewService:
    def __init__(self, provider: ModelProvider | None) -> None:
        self.provider = provider

    async def start(self, request: StartInterviewRequest) -> StartInterviewResponse:
        if self.provider is None:
            logger.warning(
                "start_interview_fallback provider_available=false original_reason=provider_unavailable",
            )
            return self._build_fallback_start_response(request)

        try:
            return await self.provider.start_interview(request)
        except ProviderCallError as exc:
            logger.warning(
                "start_interview_upstream_failed fallback_to_template=true exception_type=%s timeout_seconds=%s",
                exc.__class__.__name__,
                exc.timeout_seconds,
            )
        except ValueError as exc:
            logger.warning(
                "start_interview_invalid_payload fallback_to_template=true exception_type=%s message=%s",
                exc.__class__.__name__,
                str(exc),
            )
        except Exception as exc:
            logger.exception(
                "start_interview_unexpected_failure fallback_to_template=true exception_type=%s",
                exc.__class__.__name__,
            )

        try:
            fallback = self._build_fallback_start_response(request)
            logger.info(
                "start_interview_fallback_success position_code=%s selected_question_id=%s",
                request.position_code,
                fallback.selected_question_id,
            )
            return fallback
        except Exception:
            logger.exception(
                "start_interview_fallback_failed position_code=%s",
                request.position_code,
            )
            raise

    async def answer(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        if self.provider is None:
            raise ProviderCallError("provider unavailable")

        try:
            return await self.provider.answer_interview(request)
        except (ProviderCallError, ValueError):
            raise
        except Exception as exc:
            logger.exception("answer_interview_unexpected_failure exception_type=%s", exc.__class__.__name__)
            raise ProviderCallError("answer interview failed") from exc

    def _build_fallback_start_response(self, request: StartInterviewRequest) -> StartInterviewResponse:
        selected_question = self._pick_fallback_question(request.question_bank, request.asked_question_ids)
        content = selected_question.content.strip() or self._default_content(request.position_code)
        return StartInterviewResponse(
            action="question",
            messageType="opening",
            content=content,
            selectedQuestionId=selected_question.question_id,
            suggestions=[
                "先讲背景",
                "再讲职责与结果",
            ],
            metadata={
                "selectedQuestionTitle": selected_question.title,
            },
        )

    @staticmethod
    def _pick_fallback_question(question_bank: list[CandidateQuestion], asked_question_ids: list) -> CandidateQuestion:
        if not question_bank:
            raise ValueError("question bank is empty")

        asked = set(asked_question_ids)
        for question in question_bank:
            if question.question_id not in asked:
                return question

        return question_bank[0]

    @staticmethod
    def _default_content(position_code: str) -> str:
        normalized = position_code.lower()
        if any(keyword in normalized for keyword in ("frontend", "react", "vue", "web")):
            return "请先介绍一个与你目标岗位最相关的前端项目，重点说明场景、职责和结果。"
        if any(keyword in normalized for keyword in ("backend", "java", "golang", "python", "service")):
            return "请先介绍一个与你目标岗位最相关的后端项目，重点说明场景、职责和结果。"
        return "请先做一个简短自我介绍，并说明一段与你目标岗位最相关的项目经历。"
