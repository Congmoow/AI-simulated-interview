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

    def start(self, request: StartInterviewRequest) -> StartInterviewResponse:
        if self.provider is None:
            logger.warning(
                "start_interview_fallback provider_available=false original_reason=provider_unavailable",
            )
            return self._build_fallback_start_response(request)

        try:
            return self.provider.start_interview(request)
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
                "start_interview_fallback_success position_code=%s title=%s",
                request.position_code,
                fallback.title,
            )
            return fallback
        except Exception:
            logger.exception(
                "start_interview_fallback_failed position_code=%s",
                request.position_code,
            )
            raise

    def answer(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        return self.provider.answer_interview(request)

    def _build_fallback_start_response(self, request: StartInterviewRequest) -> StartInterviewResponse:
        source_question = request.source_question
        title = source_question.title.strip() if source_question.title.strip() else self._default_title(request.position_code)
        content = (
            source_question.content.strip()
            if source_question.content.strip()
            else self._default_content(request.position_code)
        )
        question_type = source_question.type.strip() if source_question.type.strip() else "project"

        return StartInterviewResponse(
            questionId=source_question.question_id,
            title=title,
            type=question_type,
            content=content,
            suggestions=[
                "先讲背景",
                "再讲职责与结果",
            ],
        )

    @staticmethod
    def _default_title(position_code: str) -> str:
        normalized = position_code.lower()
        if any(keyword in normalized for keyword in ("frontend", "react", "vue", "web")):
            return "请介绍一个你最熟悉的前端项目"
        if any(keyword in normalized for keyword in ("backend", "java", "golang", "python", "service")):
            return "请介绍一个你最相关的后端项目"
        return "请先做一个简短自我介绍，并说明你最相关的项目经历"

    @staticmethod
    def _default_content(position_code: str) -> str:
        normalized = position_code.lower()
        if any(keyword in normalized for keyword in ("frontend", "react", "vue", "web")):
            return "请介绍一个你最熟悉的前端项目，重点说明场景、你的职责和最终效果。"
        if any(keyword in normalized for keyword in ("backend", "java", "golang", "python", "service")):
            return "请介绍一个你最相关的后端项目，重点说明架构、职责、难点和结果。"
        return "请用 1-2 分钟介绍你的背景，并重点说明一段与你目标岗位最相关的项目经历。"
