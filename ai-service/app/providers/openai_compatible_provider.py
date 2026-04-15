from __future__ import annotations

import json
import logging
import time
from typing import Any

import httpx

from app.providers.base import ModelProvider
from app.schemas.document import ChunkResult, ProcessDocumentRequest, ProcessDocumentResponse
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


class ProviderCallError(RuntimeError):
    def __init__(
        self,
        message: str,
        *,
        status_code: int | None = None,
        response_body_snippet: str = "",
        timeout_seconds: float | None = None,
        elapsed_ms: float | None = None,
        received_response_headers: bool = False,
    ) -> None:
        super().__init__(message)
        self.status_code = status_code
        self.response_body_snippet = response_body_snippet
        self.timeout_seconds = timeout_seconds
        self.elapsed_ms = elapsed_ms
        self.received_response_headers = received_response_headers


class OpenAICompatibleProvider(ModelProvider):
    def __init__(self, settings: RuntimeAiSettings) -> None:
        self.settings = settings
        self.model_version = f"{settings.provider}:{settings.model}"

    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        try:
            payload = self._chat_json(
                step="start_interview",
                system_prompt=(
                    "你是一名中文技术面试官。"
                    "请基于给定题目，生成一条更自然、更专业、适合真实面试开场的提问。"
                    "必须只返回 JSON，对象字段包含 title、content、suggestions。"
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
            title = self._as_text(payload.get("title"))
            content = self._as_text(payload.get("content"))
            if not title or not content:
                raise ValueError("真实 provider 未返回有效的首题 title/content")

            return StartInterviewResponse(
                questionId=request.source_question.question_id,
                title=title,
                type=request.source_question.type,
                content=content,
                suggestions=self._as_str_list(payload.get("suggestions")),
            )
        except Exception as exc:
            self._log_step_failure("start_interview", exc)
            raise

    def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        next_question_text = (
            f"下一题候选标题：{request.next_question_candidate.title}\n"
            f"下一题候选内容：{request.next_question_candidate.content}\n"
            if request.next_question_candidate
            else "当前没有下一题候选。\n"
        )

        try:
            payload = self._chat_json(
                step="answer_interview",
                system_prompt=(
                    "你是一名中文技术面试官。"
                    "请判断当前回答后，应该继续追问还是切到下一题。"
                    "必须只返回 JSON，对象字段包含 decision、content、suggestions。"
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
                    "如果回答过短、模糊、缺少关键细节，优先选择 follow_up。"
                    "如果回答已经完整，且有下一题候选，则可选择 next_question。"
                ),
                temperature=max(self.settings.temperature, 0.2),
                max_tokens=min(max(self.settings.max_tokens, 256), 800),
            )
            decision = self._as_text(payload.get("decision")).lower()
            suggestions = self._as_str_list(payload.get("suggestions"))

            if decision == "next_question":
                if request.next_question_candidate is None:
                    raise ValueError("真实 provider 要求进入下一题，但当前不存在 next_question_candidate")

                return AnswerInterviewResponse(
                    type="next_question",
                    content=request.next_question_candidate.title,
                    suggestions=suggestions,
                    nextQuestion=request.next_question_candidate,
                )

            content = self._as_text(payload.get("content"))
            if decision != "follow_up" or not content:
                raise ValueError("真实 provider 未返回有效的追问决策或追问内容")

            return AnswerInterviewResponse(
                type="follow_up",
                content=content,
                suggestions=suggestions,
                nextQuestion=None,
            )
        except Exception as exc:
            self._log_step_failure("answer_interview", exc)
            raise

    def finish_interview(self, request: FinishInterviewRequest) -> FinishInterviewResponse:
        try:
            content = self._chat_text(
                step="finish_interview",
                system_prompt="你是一名中文技术面试官，请用一句话总结这场面试已结束。",
                user_prompt=(
                    f"岗位：{request.position_code}\n"
                    f"总轮数：{request.total_rounds}\n"
                    "请输出 1 句简短总结。"
                ),
                temperature=0.2,
                max_tokens=120,
            ).strip()
            if not content:
                raise ValueError("真实 provider 未返回面试结束总结")
            return FinishInterviewResponse(summary=content)
        except Exception as exc:
            self._log_step_failure("finish_interview", exc)
            raise

    def score_interview(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        rounds_text = self._build_score_rounds_text(request.rounds)
        score_timeout_seconds = 75.0
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

        try:
            logger.info(
                "评分请求准备发送：step=%s round_count=%s input_summary_length=%s timeout_seconds=%s provider=%s base_url=%s model=%s",
                "score_interview",
                len(request.rounds),
                len(rounds_text),
                score_timeout_seconds,
                self.settings.provider,
                self.settings.base_url,
                self.settings.model,
            )
            payload = self._chat_json(
                step="score_interview",
                system_prompt=(
                    "你是一名中文技术面试评分官。"
                    "请根据面试记录只返回 JSON。"
                    "JSON 字段必须包含 overallScore、rankPercentile、dimensionScores、dimensionDetails、scoreBreakdown。"
                    "dimensionScores 必须包含 technicalAccuracy、knowledgeDepth、logicalThinking、positionMatch、"
                    "projectAuthenticity、fluency、clarity、confidence 八个字段，且每个字段为 0-100 的数字。"
                ),
                user_prompt=(
                    f"岗位：{request.position_code}\n"
                    f"面试记录：\n{rounds_text}\n"
                    "请严格输出 JSON，不要额外解释。"
                ),
                temperature=0.2,
                max_tokens=min(max(self.settings.max_tokens, 512), 1400),
                timeout_seconds=score_timeout_seconds,
            )

            overall_score = self._as_number(payload.get("overallScore"))
            rank_percentile = self._as_number(payload.get("rankPercentile"))
            if overall_score is None or rank_percentile is None:
                raise ValueError("真实 provider 未返回有效的 overallScore 或 rankPercentile")

            parsed_dimension_scores = payload.get("dimensionScores")
            if not isinstance(parsed_dimension_scores, dict):
                raise ValueError("真实 provider 未返回有效的 dimensionScores")

            dimension_scores: dict[str, DimensionScore] = {}
            for key, weight in weights.items():
                raw_score = self._as_number(parsed_dimension_scores.get(key))
                if raw_score is None:
                    raise ValueError(f"真实 provider 缺少维度分数：{key}")
                dimension_scores[key] = DimensionScore(
                    score=max(0, min(raw_score, 100)),
                    weight=weight,
                )

            score_breakdown = payload.get("scoreBreakdown")
            if score_breakdown is None:
                score_breakdown = {}
            if not isinstance(score_breakdown, dict):
                raise ValueError("真实 provider 返回的 scoreBreakdown 不是对象")

            return ScoreInterviewResponse(
                overallScore=max(0, min(overall_score, 100)),
                dimensionScores=dimension_scores,
                dimensionDetails=self._normalize_detail_map(payload.get("dimensionDetails")),
                scoreBreakdown=score_breakdown,
                rankPercentile=max(0, min(rank_percentile, 100)),
                modelVersion=self.model_version,
            )
        except Exception as exc:
            self._log_step_failure(
                "score_interview",
                exc,
                round_count=len(request.rounds),
                input_summary_length=len(rounds_text),
                timeout_seconds=score_timeout_seconds,
            )
            raise

    def generate_report(self, request: GenerateReportRequest) -> GenerateReportResponse:
        dimension_text = "\n".join(
            [
                f"- {key}: score={value.score:.1f}, weight={value.weight:.2f}"
                for key, value in request.dimension_scores.items()
            ]
        )
        rounds_text = self._build_rounds_text(request.rounds)

        try:
            payload = self._chat_json(
                step="generate_report",
                system_prompt=(
                    "你是一名中文技术面试复盘顾问。"
                    "请基于分数和真实问答记录，生成结构化复盘报告。"
                    "必须只返回 JSON。"
                    "JSON 字段包含 executiveSummary、strengths、weaknesses、detailedAnalysis、"
                    "learningSuggestions、trainingPlan、nextInterviewFocus。"
                ),
                user_prompt=(
                    f"岗位：{request.position_code}\n"
                    f"综合得分：{request.overall_score:.1f}\n"
                    f"维度分数：\n{dimension_text or '无'}\n"
                    f"问答记录：\n{rounds_text or '无'}\n"
                    "请根据上面的真实内容生成报告，不要使用固定模板，不要输出 JSON 以外内容。"
                ),
                temperature=max(self.settings.temperature, 0.2),
                max_tokens=min(max(self.settings.max_tokens, 600), 1800),
            )

            executive_summary = self._as_text(payload.get("executiveSummary"))
            if not executive_summary:
                raise ValueError("真实 provider 未返回 executiveSummary")

            detailed_analysis = payload.get("detailedAnalysis")
            if detailed_analysis is None:
                detailed_analysis = {}
            if not isinstance(detailed_analysis, dict):
                raise ValueError("真实 provider 返回的 detailedAnalysis 不是对象")

            training_plan = payload.get("trainingPlan")
            if training_plan is None:
                training_plan = []
            if not isinstance(training_plan, list):
                raise ValueError("真实 provider 返回的 trainingPlan 不是数组")

            return GenerateReportResponse(
                executiveSummary=executive_summary,
                strengths=self._as_str_list(payload.get("strengths")),
                weaknesses=self._as_str_list(payload.get("weaknesses")),
                detailedAnalysis=detailed_analysis,
                learningSuggestions=self._as_str_list(payload.get("learningSuggestions")),
                trainingPlan=training_plan,
                nextInterviewFocus=self._as_str_list(payload.get("nextInterviewFocus")),
                modelVersion=self.model_version,
            )
        except Exception as exc:
            self._log_step_failure("generate_report", exc)
            raise

    def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        raise NotImplementedError("recommend_resources 暂未接入真实 AI，请勿调用")

    def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        raise NotImplementedError("generate_training_plan 暂未接入真实 AI，请勿调用")

    def search_rag(self, request: RagSearchRequest) -> RagSearchResponse:
        raise NotImplementedError("search_rag 暂未接入真实 AI，请勿调用")

    def process_document(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        chunk_count = max(1, len(request.title) // 10 + 3)
        chunks = [
            ChunkResult(
                chunkIndex=index,
                content=f"[{request.title}] 文档切片 {index + 1}：当前链路仍使用本地切片占位结果。",
                tokenCount=max(10, len(request.title) + 20),
                metadata={
                    "source": "local-document-processor",
                    "fileName": request.file_name,
                    "fileType": request.file_type,
                    "chunkIndex": index,
                },
            )
            for index in range(chunk_count)
        ]
        return ProcessDocumentResponse(documentId=request.document_id, chunks=chunks)

    def _chat_text(
        self,
        *,
        step: str,
        system_prompt: str,
        user_prompt: str,
        temperature: float,
        max_tokens: int,
        timeout_seconds: float = 30.0,
    ) -> str:
        started_at = time.monotonic()
        try:
            with httpx.Client(timeout=timeout_seconds, http2=False) as client:
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
        except httpx.HTTPStatusError as exc:
            raise ProviderCallError(
                f"{step} 上游返回非成功状态",
                status_code=exc.response.status_code if exc.response is not None else None,
                response_body_snippet=self._response_body_snippet(exc.response),
                timeout_seconds=timeout_seconds,
                elapsed_ms=round((time.monotonic() - started_at) * 1000, 2),
                received_response_headers=exc.response is not None,
            ) from exc
        except httpx.RequestError as exc:
            raise ProviderCallError(
                f"{step} 请求上游失败",
                timeout_seconds=timeout_seconds,
                elapsed_ms=round((time.monotonic() - started_at) * 1000, 2),
                received_response_headers=getattr(exc, "response", None) is not None,
            ) from exc
        except Exception as exc:
            raise ProviderCallError(
                f"{step} 调用上游时发生异常",
                timeout_seconds=timeout_seconds,
                elapsed_ms=round((time.monotonic() - started_at) * 1000, 2),
                received_response_headers=False,
            ) from exc

        try:
            content = payload["choices"][0]["message"]["content"]
        except Exception as exc:
            raise ValueError(f"{step} 上游返回格式不符合预期") from exc

        text = self._as_text(content)
        if not text:
            raise ValueError(f"{step} 上游返回空内容")
        return text

    def _chat_json(
        self,
        *,
        step: str,
        system_prompt: str,
        user_prompt: str,
        temperature: float,
        max_tokens: int,
        timeout_seconds: float = 30.0,
    ) -> dict[str, Any]:
        content = self._chat_text(
            step=step,
            system_prompt=system_prompt,
            user_prompt=user_prompt,
            temperature=temperature,
            max_tokens=max_tokens,
            timeout_seconds=timeout_seconds,
        )

        try:
            return json.loads(self._extract_json_object(content))
        except json.JSONDecodeError as exc:
            raise ValueError(f"{step} 返回的内容不是合法 JSON") from exc

    def _log_step_failure(self, step: str, exc: Exception, **context: Any) -> None:
        status_code = getattr(exc, "status_code", None)
        response_body_snippet = getattr(exc, "response_body_snippet", "")
        timeout_seconds = getattr(exc, "timeout_seconds", None)
        elapsed_ms = getattr(exc, "elapsed_ms", None)
        received_response_headers = getattr(exc, "received_response_headers", False)
        inner_exception = exc.__cause__.__class__.__name__ if exc.__cause__ is not None else "n/a"
        logger.exception(
            "真实 provider 调用失败：step=%s provider=%s base_url=%s model=%s upstream_status_code=%s exception_type=%s inner_exception=%s timeout_seconds=%s elapsed_ms=%s received_response_headers=%s round_count=%s input_summary_length=%s response_body_snippet=%s",
            step,
            self.settings.provider,
            self.settings.base_url,
            self.settings.model,
            status_code if status_code is not None else "n/a",
            exc.__class__.__name__,
            inner_exception,
            timeout_seconds if timeout_seconds is not None else context.get("timeout_seconds", "n/a"),
            elapsed_ms if elapsed_ms is not None else "n/a",
            received_response_headers,
            context.get("round_count", "n/a"),
            context.get("input_summary_length", "n/a"),
            response_body_snippet,
        )

    def _response_body_snippet(self, response: httpx.Response | None) -> str:
        if response is None:
            return ""
        return self._sanitize_text(" ".join(response.text.split())[:320])

    def _sanitize_text(self, value: str) -> str:
        if not value:
            return ""
        return value.replace(self.settings.api_key, "[REDACTED]")

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

    @staticmethod
    def _build_rounds_text(rounds: list[Any]) -> str:
        parts: list[str] = []
        for round_item in rounds:
            follow_ups = "；".join(round_item.follow_ups) if round_item.follow_ups else "无"
            parts.append(
                f"第{round_item.round_number}轮\n"
                f"题型：{round_item.question_type}\n"
                f"题目：{round_item.question_title}\n"
                f"题目内容：{round_item.question_content}\n"
                f"回答：{round_item.answer or '未作答'}\n"
                f"追问：{follow_ups}"
            )
        return "\n\n".join(parts)

    @classmethod
    def _build_score_rounds_text(cls, rounds: list[Any]) -> str:
        parts: list[str] = []
        for round_item in rounds:
            follow_ups = round_item.follow_ups[-2:] if round_item.follow_ups else []
            follow_up_text = "；".join(cls._truncate_text(item, 160) for item in follow_ups) if follow_ups else "无"
            parts.append(
                f"第{round_item.round_number}轮\n"
                f"题型：{cls._truncate_text(round_item.question_type, 40)}\n"
                f"题目：{cls._truncate_text(round_item.question_title, 120)}\n"
                f"题目内容：{cls._truncate_text(round_item.question_content, 240)}\n"
                f"回答：{cls._truncate_text(round_item.answer or '未作答', 600)}\n"
                f"追问：{follow_up_text}"
            )

        summary = "\n\n".join(parts)
        if len(summary) <= 2400:
            return summary

        return summary[: 2400 - len("[TRUNCATED]")] + "[TRUNCATED]"

    @staticmethod
    def _truncate_text(value: str, limit: int) -> str:
        if len(value) <= limit:
            return value
        return value[: limit - len("...[截断]")] + "...[截断]"
