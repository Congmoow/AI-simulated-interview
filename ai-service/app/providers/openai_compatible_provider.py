from __future__ import annotations

import json
import logging
import threading
import time
from typing import Any

import httpx

from app.providers.base import ModelProvider
from app.schemas.document import ChunkResult, ProcessDocumentRequest, ProcessDocumentResponse
from app.schemas.interview import (
    AnswerInterviewRequest,
    AnswerInterviewResponse,
    DimensionScore,
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

_shared_clients: dict[tuple[str, str], httpx.Client] = {}
_shared_clients_lock = threading.Lock()


def get_shared_http_client(base_url: str, api_key: str) -> httpx.Client:
    key = (base_url.rstrip("/"), api_key)
    with _shared_clients_lock:
        client = _shared_clients.get(key)
        if client is None:
            client = httpx.Client(
                base_url=f"{base_url.rstrip('/')}/",
                headers={
                    "Authorization": f"Bearer {api_key}",
                    "Content-Type": "application/json",
                },
                http2=False,
            )
            _shared_clients[key] = client
        return client


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
        self._http_client: httpx.Client | None = None

    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        start_timeout_seconds = 12.0
        start_max_tokens = min(max(self.settings.max_tokens, 120), 220)
        try:
            payload = self._chat_json(
                step="start_interview",
                system_prompt=(
                    "你是中文技术面试官。"
                    "基于给定题干生成首题。"
                    "仅返回 JSON：title、content、suggestions。"
                    "content 只保留一句话，suggestions 最多 2 条短语。"
                ),
                user_prompt=(
                    f"岗位：{request.position_code}\n"
                    f"题目：{request.source_question.title}\n"
                    f"题干：{request.source_question.content}"
                ),
                temperature=max(self.settings.temperature, 0.2),
                max_tokens=start_max_tokens,
                timeout_seconds=start_timeout_seconds,
            )
            title = self._as_text(payload.get("title"))
            content = self._as_text(payload.get("content"))
            if not title or not content:
                raise ValueError("missing start interview title/content")

            return StartInterviewResponse(
                questionId=request.source_question.question_id,
                title=title,
                type=request.source_question.type,
                content=content,
                suggestions=self._as_str_list(payload.get("suggestions"))[:2],
            )
        except Exception as exc:
            self._log_step_failure("start_interview", exc)
            raise

    def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        next_question_text = (
            f"Next candidate question title: {request.next_question_candidate.title}\n"
            f"Next candidate question content: {request.next_question_candidate.content}\n"
            if request.next_question_candidate
            else "No next candidate question.\n"
        )

        try:
            payload = self._chat_json(
                step="answer_interview",
                system_prompt=(
                    "You are a Chinese technical interviewer. "
                    "Return JSON only with decision, content and suggestions. "
                    "Decision must be follow_up or next_question."
                ),
                user_prompt=(
                    f"Position: {request.position_code}\n"
                    f"Mode: {request.interview_mode}\n"
                    f"Question title: {request.question_title}\n"
                    f"Question content: {request.question_content}\n"
                    f"Answer: {request.answer}\n"
                    f"Round: {request.current_round}/{request.total_rounds}\n"
                    f"Follow-up count: {request.follow_up_count}\n"
                    f"{next_question_text}"
                ),
                temperature=max(self.settings.temperature, 0.2),
                max_tokens=min(max(self.settings.max_tokens, 256), 800),
            )
            decision = self._as_text(payload.get("decision")).lower()
            suggestions = self._as_str_list(payload.get("suggestions"))

            if decision == "next_question":
                if request.next_question_candidate is None:
                    raise ValueError("next question requested without candidate")

                return AnswerInterviewResponse(
                    type="next_question",
                    content=request.next_question_candidate.title,
                    suggestions=suggestions,
                    nextQuestion=request.next_question_candidate,
                )

            content = self._as_text(payload.get("content"))
            if decision != "follow_up" or not content:
                raise ValueError("invalid follow-up response")

            return AnswerInterviewResponse(
                type="follow_up",
                content=content,
                suggestions=suggestions,
                nextQuestion=None,
            )
        except Exception as exc:
            self._log_step_failure("answer_interview", exc)
            raise

    def score_interview(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        rounds_text = self._build_score_rounds_text(request.rounds)
        score_timeout_seconds = 45.0
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
                "score_request_prepared step=%s round_count=%s input_summary_length=%s timeout_seconds=%s provider=%s base_url=%s model=%s",
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
                    "You are a Chinese technical interview scorer. "
                    "Return JSON only with overallScore, rankPercentile, "
                    "dimensionScores, dimensionDetails and scoreBreakdown."
                ),
                user_prompt=(
                    f"Position: {request.position_code}\n"
                    f"Interview summary:\n{rounds_text}\n"
                    "Return JSON only."
                ),
                temperature=0.2,
                max_tokens=min(max(self.settings.max_tokens, 512), 900),
                timeout_seconds=score_timeout_seconds,
            )

            overall_score = self._as_number(payload.get("overallScore"))
            rank_percentile = self._as_number(payload.get("rankPercentile"))
            if overall_score is None or rank_percentile is None:
                raise ValueError("missing score fields")

            parsed_dimension_scores = payload.get("dimensionScores")
            if not isinstance(parsed_dimension_scores, dict):
                raise ValueError("dimensionScores must be an object")

            dimension_scores: dict[str, DimensionScore] = {}
            for key, weight in weights.items():
                raw_score = self._as_number(parsed_dimension_scores.get(key))
                if raw_score is None:
                    raise ValueError(f"missing dimension score: {key}")
                dimension_scores[key] = DimensionScore(
                    score=max(0, min(raw_score, 100)),
                    weight=weight,
                )

            score_breakdown = payload.get("scoreBreakdown")
            if score_breakdown is None:
                score_breakdown = {}
            if not isinstance(score_breakdown, dict):
                raise ValueError("scoreBreakdown must be an object")

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
        dimension_detail_text = "\n".join(
            [
                f"- {key}: {self._truncate_with_marker(value, 120)}"
                for key, value in request.dimension_details.items()
            ]
        )
        rounds_text = self._build_report_rounds_text(request.rounds)
        report_timeout_seconds = 60.0

        try:
            logger.info(
                "report_request_prepared step=%s timeout_seconds=%s input_summary_length=%s provider=%s base_url=%s model=%s",
                "generate_report",
                report_timeout_seconds,
                len(rounds_text),
                self.settings.provider,
                self.settings.base_url,
                self.settings.model,
            )
            payload = self._chat_json(
                step="generate_report",
                system_prompt=(
                    "You are a Chinese interview report writer. "
                    "Return JSON only with executiveSummary, strengths, weaknesses, "
                    "detailedAnalysis, learningSuggestions, trainingPlan and nextInterviewFocus."
                ),
                user_prompt=(
                    f"Position: {request.position_code}\n"
                    f"Overall score: {request.overall_score:.1f}\n"
                    f"Dimension scores:\n{dimension_text or 'N/A'}\n"
                    f"Dimension details:\n{dimension_detail_text or 'N/A'}\n"
                    f"Interview summary:\n{rounds_text or 'N/A'}\n"
                    "Build the report from scores first, then use the interview summary for evidence."
                ),
                temperature=max(self.settings.temperature, 0.2),
                max_tokens=min(max(self.settings.max_tokens, 600), 1200),
                timeout_seconds=report_timeout_seconds,
            )

            executive_summary = self._normalize_executive_summary(payload)
            if not executive_summary:
                raise ValueError("missing executiveSummary")

            strengths = self._normalize_string_list(payload.get("strengths"))
            weaknesses = self._normalize_string_list(payload.get("weaknesses"))
            learning_suggestions = self._normalize_string_list(payload.get("learningSuggestions"))

            return GenerateReportResponse(
                executiveSummary=executive_summary,
                strengths=strengths,
                weaknesses=weaknesses,
                detailedAnalysis=self._normalize_detailed_analysis(payload.get("detailedAnalysis")),
                learningSuggestions=learning_suggestions,
                trainingPlan=self._normalize_training_plan(payload.get("trainingPlan")),
                nextInterviewFocus=self._normalize_string_list(payload.get("nextInterviewFocus")),
                modelVersion=self.model_version,
            )
        except Exception as exc:
            response_payload_snippet = ""
            if "payload" in locals():
                response_payload_snippet = self._sanitize_text(
                    json.dumps(payload, ensure_ascii=False, default=str)[:320]
                )
            self._log_step_failure(
                "generate_report",
                exc,
                timeout_seconds=report_timeout_seconds,
                input_summary_length=len(rounds_text),
                response_body_snippet=response_payload_snippet,
            )
            raise

    def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        raise NotImplementedError("recommend_resources is not connected to real AI")

    def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        raise NotImplementedError("generate_training_plan is not connected to real AI")

    def search_rag(self, request: RagSearchRequest) -> RagSearchResponse:
        raise NotImplementedError("search_rag is not connected to real AI")

    def process_document(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        chunk_count = max(1, len(request.title) // 10 + 3)
        chunks = [
            ChunkResult(
                chunkIndex=index,
                content=f"[{request.title}] chunk {index + 1}",
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

    def _get_http_client(self) -> httpx.Client:
        if self._http_client is None:
            self._http_client = get_shared_http_client(self.settings.base_url, self.settings.api_key)
        return self._http_client

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
        client = self._get_http_client()
        try:
            response = client.post(
                "chat/completions",
                json={
                    "model": self.settings.model,
                    "messages": [
                        {"role": "system", "content": system_prompt},
                        {"role": "user", "content": user_prompt},
                    ],
                    "temperature": temperature,
                    "max_tokens": max_tokens,
                },
                timeout=timeout_seconds,
            )
            response.raise_for_status()
            payload = response.json()
        except httpx.HTTPStatusError as exc:
            raise ProviderCallError(
                f"{step} upstream returned non-success status",
                status_code=exc.response.status_code if exc.response is not None else None,
                response_body_snippet=self._response_body_snippet(exc.response),
                timeout_seconds=timeout_seconds,
                elapsed_ms=round((time.monotonic() - started_at) * 1000, 2),
                received_response_headers=exc.response is not None,
            ) from exc
        except httpx.RequestError as exc:
            raise ProviderCallError(
                f"{step} request to upstream failed",
                timeout_seconds=timeout_seconds,
                elapsed_ms=round((time.monotonic() - started_at) * 1000, 2),
                received_response_headers=getattr(exc, "response", None) is not None,
            ) from exc
        except Exception as exc:
            raise ProviderCallError(
                f"{step} unexpected upstream error",
                timeout_seconds=timeout_seconds,
                elapsed_ms=round((time.monotonic() - started_at) * 1000, 2),
                received_response_headers=False,
            ) from exc

        try:
            content = payload["choices"][0]["message"]["content"]
        except Exception as exc:
            raise ValueError(f"{step} response format is invalid") from exc

        text = self._as_text(content)
        if not text:
            raise ValueError(f"{step} returned empty content")
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
            raise ValueError(f"{step} did not return valid JSON") from exc

    def _log_step_failure(self, step: str, exc: Exception, **context: Any) -> None:
        status_code = getattr(exc, "status_code", None)
        response_body_snippet = getattr(exc, "response_body_snippet", "") or context.get("response_body_snippet", "")
        timeout_seconds = getattr(exc, "timeout_seconds", None)
        elapsed_ms = getattr(exc, "elapsed_ms", None)
        received_response_headers = getattr(exc, "received_response_headers", False)
        inner_exception = exc.__cause__.__class__.__name__ if exc.__cause__ is not None else "n/a"
        logger.exception(
            "provider_call_failed step=%s provider=%s base_url=%s model=%s upstream_status_code=%s exception_type=%s inner_exception=%s timeout_seconds=%s elapsed_ms=%s received_response_headers=%s round_count=%s input_summary_length=%s response_body_snippet=%s",
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

    @classmethod
    def _build_score_rounds_text(cls, rounds: list[Any]) -> str:
        return cls._build_compact_rounds_text(
            rounds,
            question_type_limit=30,
            title_limit=100,
            content_limit=180,
            answer_limit=450,
            follow_up_count=2,
            follow_up_limit=120,
            total_limit=1800,
        )

    @classmethod
    def _build_report_rounds_text(cls, rounds: list[Any]) -> str:
        return cls._build_compact_rounds_text(
            rounds,
            question_type_limit=30,
            title_limit=80,
            content_limit=120,
            answer_limit=320,
            follow_up_count=1,
            follow_up_limit=100,
            total_limit=1200,
        )

    @classmethod
    def _build_compact_rounds_text(
        cls,
        rounds: list[Any],
        *,
        question_type_limit: int,
        title_limit: int,
        content_limit: int,
        answer_limit: int,
        follow_up_count: int,
        follow_up_limit: int,
        total_limit: int,
    ) -> str:
        parts: list[str] = []
        for round_item in rounds:
            follow_ups = round_item.follow_ups[-follow_up_count:] if round_item.follow_ups else []
            follow_up_text = " | ".join(cls._truncate_text(item, follow_up_limit) for item in follow_ups) if follow_ups else "N/A"
            parts.append(
                "\n".join(
                    [
                        f"Round {round_item.round_number}",
                        f"Type: {cls._truncate_text(round_item.question_type, question_type_limit)}",
                        f"Title: {cls._truncate_text(round_item.question_title, title_limit)}",
                        f"Content: {cls._truncate_text(round_item.question_content, content_limit)}",
                        f"Answer: {cls._truncate_text(round_item.answer or 'N/A', answer_limit)}",
                        f"FollowUps: {follow_up_text}",
                    ]
                )
            )

        summary = "\n\n".join(parts)
        return cls._truncate_with_marker(summary, total_limit)

    @staticmethod
    def _truncate_text(value: str, limit: int) -> str:
        if len(value) <= limit:
            return value
        marker = "[TRUNCATED]"
        return value[: limit - len(marker)] + marker

    @classmethod
    def _truncate_with_marker(cls, value: str, limit: int, marker: str = "[TRUNCATED]") -> str:
        if len(value) <= limit:
            return value
        return value[: limit - len(marker)] + marker

    @classmethod
    def _normalize_executive_summary(cls, payload: dict[str, Any]) -> str:
        for key in ("executiveSummary", "summary", "overallSummary"):
            text = cls._as_text(payload.get(key))
            if text:
                return cls._truncate_with_marker(text, 1200)
        return ""

    @classmethod
    def _normalize_string_list(cls, value: Any) -> list[str]:
        if isinstance(value, str):
            text = cls._as_text(value)
            return [text] if text else []
        if isinstance(value, list):
            return [cls._as_text(item) for item in value if cls._as_text(item)]
        return []

    @staticmethod
    def _normalize_detailed_analysis(value: Any) -> dict[str, Any]:
        if isinstance(value, dict):
            return value
        if isinstance(value, str):
            text = value.strip()
            return {"summary": text} if text else {}
        if isinstance(value, list):
            return {"items": value} if value else {}
        return {}

    @staticmethod
    def _normalize_training_plan(value: Any) -> list[Any]:
        if value is None:
            return []
        if isinstance(value, list):
            return value
        if isinstance(value, dict):
            return [value]
        return []
