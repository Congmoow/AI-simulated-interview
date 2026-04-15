from __future__ import annotations

import json
import logging
from typing import Any

import httpx

from app.providers.base import ModelProvider
from app.providers.mock_provider import MockProvider
from app.schemas.document import ProcessDocumentRequest, ProcessDocumentResponse
from app.schemas.interview import (
    AnswerInterviewRequest,
    AnswerInterviewResponse,
    DimensionScore,
    FinishInterviewRequest,
    FinishInterviewResponse,
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
from app.services.backend_ai_settings import RuntimeAiSettings

logger = logging.getLogger(__name__)


class OpenAICompatibleProvider(ModelProvider):
    def __init__(self, settings: RuntimeAiSettings) -> None:
        self.settings = settings
        self.model_version = f"{settings.provider}:{settings.model}"
        self._mock = MockProvider()

    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        fallback = self._mock.start_interview(request)
        try:
            payload = self._chat_json(
                system_prompt=(
                    "你是一位中文技术面试官。"
                    "请基于给定题目生成一条更自然、专业、适合真实面试开场的提问。"
                    "必须返回 JSON，包含 title、content、suggestions。"
                ),
                user_prompt=(
                    f"岗位：{request.position_code}\n"
                    f"模式：{request.interview_mode}\n"
                    f"题目标题：{request.source_question.title}\n"
                    f"题目内容：{request.source_question.content}\n"
                    "请保留原题主题，不要偏题。"
                ),
                temperature=max(self.settings.temperature, 0.2),
                max_tokens=min(max(self.settings.max_tokens, 256), 800),
            )
            title = self._as_text(payload.get("title")) or fallback.title
            content = self._as_text(payload.get("content")) or fallback.content
            suggestions = self._as_str_list(payload.get("suggestions")) or fallback.suggestions
            return StartInterviewResponse(
                questionId=fallback.question_id,
                title=title,
                type=fallback.type,
                content=content,
                suggestions=suggestions,
            )
        except Exception:
            logger.exception("真实 provider 生成首题失败，回退到 mock provider")
            return fallback

    def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        fallback = self._mock.answer_interview(request)
        try:
            next_question_text = (
                f"下一题候选：{request.next_question_candidate.title}\n"
                f"下一题内容：{request.next_question_candidate.content}\n"
                if request.next_question_candidate
                else "当前没有下一题候选。\n"
            )
            payload = self._chat_json(
                system_prompt=(
                    "你是一位中文技术面试官。"
                    "请判断当前回答后，应该继续追问还是切到下一题。"
                    "必须返回 JSON，包含 decision、content、suggestions。"
                    "decision 只能是 follow_up 或 next_question。"
                ),
                user_prompt=(
                    f"岗位：{request.position_code}\n"
                    f"模式：{request.interview_mode}\n"
                    f"当前题目：{request.question_title}\n"
                    f"题目内容：{request.question_content}\n"
                    f"候选人回答：{request.answer}\n"
                    f"当前轮次：{request.current_round}/{request.total_rounds}\n"
                    f"已追问次数：{request.follow_up_count}\n"
                    f"{next_question_text}"
                    "如果回答过短、模糊、缺少关键细节，优先 follow_up；"
                    "如果回答已经足够完整，并且有下一题候选，则可以 next_question。"
                ),
                temperature=max(self.settings.temperature, 0.2),
                max_tokens=min(max(self.settings.max_tokens, 256), 800),
            )

            decision = self._as_text(payload.get("decision")).lower()
            suggestions = self._as_str_list(payload.get("suggestions")) or fallback.suggestions
            content = self._as_text(payload.get("content")) or fallback.content

            if decision == "next_question" and request.next_question_candidate is not None:
                return AnswerInterviewResponse(
                    type="next_question",
                    content=request.next_question_candidate.title,
                    suggestions=suggestions,
                    nextQuestion=request.next_question_candidate,
                )

            return AnswerInterviewResponse(
                type="follow_up",
                content=content,
                suggestions=suggestions,
                nextQuestion=None,
            )
        except Exception:
            logger.exception("真实 provider 生成追问/下一题失败，回退到 mock provider")
            return fallback

    def finish_interview(self, request: FinishInterviewRequest) -> FinishInterviewResponse:
        fallback = self._mock.finish_interview(request)
        try:
            content = self._chat_text(
                system_prompt="你是一位中文技术面试官，请用一句话总结这场面试已结束。",
                user_prompt=(
                    f"岗位：{request.position_code}\n"
                    f"总轮数：{request.total_rounds}\n"
                    "请输出 1 句简短总结。"
                ),
                temperature=0.2,
                max_tokens=120,
            ).strip()
            return FinishInterviewResponse(summary=content or fallback.summary)
        except Exception:
            logger.exception("真实 provider 生成面试结束总结失败，回退到 mock provider")
            return fallback

    def score_interview(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        fallback = self._mock.score_interview(request)
        try:
            rounds_text = "\n\n".join(
                [
                    f"第{round_item.round_number}轮\n"
                    f"题型：{round_item.question_type}\n"
                    f"题目：{round_item.question_title}\n"
                    f"回答：{round_item.answer or '未作答'}"
                    for round_item in request.rounds
                ]
            )
            payload = self._chat_json(
                system_prompt=(
                    "你是一位中文技术面试评分官。"
                    "请根据面试记录输出 JSON，包含 overallScore、rankPercentile、"
                    "dimensionScores、dimensionDetails。"
                    "dimensionScores 必须包含 technicalAccuracy、knowledgeDepth、"
                    "logicalThinking、positionMatch、projectAuthenticity、fluency、clarity、confidence，"
                    "每个字段是 0-100 的数字。"
                ),
                user_prompt=(
                    f"岗位：{request.position_code}\n"
                    f"面试记录：\n{rounds_text}\n"
                    "请严格输出 JSON，不要额外解释。"
                ),
                temperature=0.2,
                max_tokens=min(max(self.settings.max_tokens, 512), 1400),
            )

            weights = {
                "technicalAccuracy": 0.30,
                "knowledgeDepth": 0.20,
                "logicalThinking": 0.15,
                "positionMatch": 0.15,
                "projectAuthenticity": 0.10,
                "fluency": 0.05,
                "clarity": 0.03,
                "confidence": 0.02,
            }

            parsed_dimension_scores = payload.get("dimensionScores", {})
            dimension_scores: dict[str, DimensionScore] = {}
            for key, weight in weights.items():
                raw_score = self._as_number(parsed_dimension_scores.get(key))
                if raw_score is None:
                    dimension_scores[key] = fallback.dimension_scores[key]
                else:
                    dimension_scores[key] = DimensionScore(score=max(0, min(raw_score, 100)), weight=weight)

            overall_score = self._as_number(payload.get("overallScore"))
            rank_percentile = self._as_number(payload.get("rankPercentile"))
            dimension_details = payload.get("dimensionDetails")

            return ScoreInterviewResponse(
                overallScore=overall_score if overall_score is not None else fallback.overall_score,
                dimensionScores=dimension_scores,
                dimensionDetails=self._normalize_detail_map(dimension_details) or fallback.dimension_details,
                scoreBreakdown=fallback.score_breakdown,
                rankPercentile=rank_percentile if rank_percentile is not None else fallback.rank_percentile,
                modelVersion=self.model_version,
            )
        except Exception:
            logger.exception("真实 provider 评分失败，回退到 mock provider")
            return fallback

    def generate_report(self, request: GenerateReportRequest) -> GenerateReportResponse:
        return self._mock.generate_report(request)

    def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        return self._mock.recommend_resources(request)

    def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        return self._mock.generate_training_plan(request)

    def search_rag(self, request: RagSearchRequest) -> RagSearchResponse:
        return self._mock.search_rag(request)

    def process_document(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        return self._mock.process_document(request)

    def _chat_text(
        self,
        *,
        system_prompt: str,
        user_prompt: str,
        temperature: float,
        max_tokens: int,
    ) -> str:
        with httpx.Client(timeout=30.0, http2=False) as client:
            response = client.post(
                f"{self.settings.base_url.rstrip('/')}/chat/completions",
                headers={
                    "Authorization": f"Bearer {self.settings.api_key}",
                    "Content-Type": "application/json",
                },
                json={
                    "model": self.settings.model,
                    "messages": [
                        {"role": "system", "content": system_prompt},
                        {"role": "user", "content": user_prompt},
                    ],
                    "temperature": temperature,
                    "max_tokens": max_tokens,
                },
            )
            response.raise_for_status()
            payload = response.json()

        return payload["choices"][0]["message"]["content"]

    def _chat_json(
        self,
        *,
        system_prompt: str,
        user_prompt: str,
        temperature: float,
        max_tokens: int,
    ) -> dict[str, Any]:
        content = self._chat_text(
            system_prompt=system_prompt,
            user_prompt=user_prompt,
            temperature=temperature,
            max_tokens=max_tokens,
        )
        return json.loads(self._extract_json_object(content))

    @staticmethod
    def _extract_json_object(content: str) -> str:
        trimmed = content.strip()
        if trimmed.startswith("```"):
            first_newline = trimmed.find("\n")
            if first_newline >= 0:
                trimmed = trimmed[first_newline + 1 :].lstrip()
            if trimmed.endswith("```"):
                trimmed = trimmed[:-3].rstrip()

        start = trimmed.find("{")
        end = trimmed.rfind("}")
        if start >= 0 and end > start:
            return trimmed[start : end + 1]
        return trimmed

    @staticmethod
    def _as_text(value: Any) -> str:
        if value is None:
            return ""
        return str(value).strip()

    @staticmethod
    def _as_str_list(value: Any) -> list[str]:
        if not isinstance(value, list):
            return []
        return [str(item).strip() for item in value if str(item).strip()]

    @staticmethod
    def _as_number(value: Any) -> float | None:
        if value is None:
            return None
        try:
            return float(value)
        except (TypeError, ValueError):
            return None

    @staticmethod
    def _normalize_detail_map(value: Any) -> dict[str, str]:
        if not isinstance(value, dict):
            return {}
        return {
            str(key): str(detail).strip()
            for key, detail in value.items()
            if str(detail).strip()
        }
