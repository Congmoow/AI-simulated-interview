from __future__ import annotations

import json
import logging
import threading
import time
from typing import Any
from uuid import UUID

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
STANDARD_DIMENSION_WEIGHTS = {
    "technicalAccuracy": 0.30,
    "knowledgeDepth": 0.20,
    "logicalThinking": 0.15,
    "positionMatch": 0.15,
    "projectAuthenticity": 0.10,
    "fluency": 0.05,
    "clarity": 0.03,
    "confidence": 0.02,
}
STANDARD_DIMENSION_ALIASES = {
    "technicalAccuracy": (
        "technicalAccuracy",
        "technicalFoundation",
        "frameworkKnowledge",
        "technicalDepth",
        "Java 核心与框架",
    ),
    "knowledgeDepth": (
        "knowledgeDepth",
        "technicalDepth",
        "frameworkKnowledge",
        "technicalFoundation",
        "problemSolving",
        "systemDesign",
        "Java 核心与框架",
        "分布式与高并发",
    ),
    "logicalThinking": (
        "logicalThinking",
        "problemSolving",
        "systemDesign",
        "分布式与高并发",
    ),
    "positionMatch": (
        "positionMatch",
        "projectExperience",
        "项目实战经验",
        "systemDesign",
        "technicalDepth",
        "frameworkKnowledge",
        "Java 核心与框架",
    ),
    "projectAuthenticity": (
        "projectAuthenticity",
        "projectExperience",
        "项目实战经验",
    ),
    "fluency": (
        "fluency",
        "communication",
        "沟通与表达",
    ),
    "clarity": (
        "clarity",
        "communication",
        "沟通与表达",
    ),
    "confidence": (
        "confidence",
        "communication",
        "沟通与表达",
    ),
}


def get_shared_http_client(base_url: str, api_key: str) -> httpx.Client:
    key = (base_url.rstrip("/"), api_key)
    with _shared_clients_lock:
        client = _shared_clients.get(key)
        if client is None:
            client = httpx.Client(
                base_url=f"{base_url.rstrip('/')}/",
                headers={"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"},
                http2=False,
            )
            _shared_clients[key] = client
        return client


class ProviderCallError(RuntimeError):
    def __init__(self, message: str, *, status_code: int | None = None, response_body_snippet: str = "", timeout_seconds: float | None = None, elapsed_ms: float | None = None, received_response_headers: bool = False) -> None:
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
        start_timeout_seconds = 25.0
        payload = self._chat_json(
            step="start_interview",
            system_prompt="你是一名中文技术面试官。主问题只能从给定题库中选择。返回 JSON：action、messageType、content、selectedQuestionId、suggestions、metadata。",
            user_prompt=(
                f"岗位：{request.position_name} ({request.position_code})\n"
                f"题库：\n{self._build_question_bank_text(request.question_bank, 1800)}\n"
                f"已问主问题：{', '.join(str(x) for x in request.asked_question_ids) or '无'}\n"
                "请直接选择一条最适合作为开场主问题的题目，并生成自然的面试官消息。"
            ),
            temperature=max(self.settings.temperature, 0.2),
            max_tokens=min(max(self.settings.max_tokens, 120), 220),
            timeout_seconds=start_timeout_seconds,
        )
        action = self._as_text(payload.get("action")).lower()
        message_type = self._as_text(payload.get("messageType"))
        content = self._as_text(payload.get("content"))
        selected_question_id = self._parse_uuid(payload.get("selectedQuestionId"))
        if action != "question" or not message_type or not content or selected_question_id is None:
            raise ValueError("invalid start interview payload")
        return StartInterviewResponse(
            action=action,
            messageType=message_type,
            content=content,
            selectedQuestionId=selected_question_id,
            suggestions=self._as_str_list(payload.get("suggestions"))[:2],
            metadata=self._as_dict(payload.get("metadata")),
        )

    def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        payload = self._chat_json(
            step="answer_interview",
            system_prompt="你是一名中文技术面试官。主问题只能从给定题库中选择；追问必须围绕当前主问题；不得重复已问主问题。返回 JSON：action、messageType、content、selectedQuestionId、suggestions、metadata。action 只能是 question、follow_up、finish。",
            user_prompt=self._build_compact_answer_prompt(request),
            temperature=max(self.settings.temperature, 0.2),
            max_tokens=220,
            timeout_seconds=60.0,
        )
        return self._parse_answer_payload(request, payload)

    def score_interview(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        rounds_text = self._build_score_rounds_text(request.rounds)
        try:
            payload = self._chat_json(
                step="score_interview",
                system_prompt=(
                    "You are a Chinese technical interview scorer. Return JSON only. "
                    "You must score exactly these eight dimension keys in dimensionScores or dimensions: "
                    "technicalAccuracy, knowledgeDepth, logicalThinking, positionMatch, "
                    "projectAuthenticity, fluency, clarity, confidence. "
                    "Do not rename any dimension key. Each dimension must contain a numeric score between 0 and 100. "
                    "You may also include detail text for each dimension. "
                    "Always return overallScore, rankPercentile, dimensionScores or dimensions, dimensionDetails and scoreBreakdown."
                ),
                user_prompt=(
                    f"Position: {request.position_code}\n"
                    f"Interview summary:\n{rounds_text}\n"
                    "Return JSON only. Score every required dimension even when the evidence is weak or negative."
                ),
                temperature=0.2,
                max_tokens=min(max(self.settings.max_tokens, 512), 900),
                timeout_seconds=45.0,
            )
            overall_score = self._as_number(payload.get("overallScore"))
            rank_percentile = self._as_number(payload.get("rankPercentile"))
            parsed_dimension_scores = payload.get("dimensionScores")
            parsed_dimensions = payload.get("dimensions")
            if overall_score is None or rank_percentile is None:
                raise ValueError("invalid score payload")
            score_sources = [
                source
                for source in (parsed_dimension_scores, parsed_dimensions)
                if isinstance(source, dict)
            ]
            if not score_sources:
                raise ValueError("invalid score payload")
            raw_detail_map = self._merge_detail_maps(
                self._normalize_detail_map(payload.get("dimensionDetails")),
                self._extract_dimension_details(parsed_dimension_scores),
                self._extract_dimension_details(parsed_dimensions),
            )
            dimension_scores = self._normalize_standard_dimension_scores(score_sources, overall_score)
            score_breakdown = payload.get("scoreBreakdown")
            if score_breakdown is None or not isinstance(score_breakdown, dict):
                score_breakdown = {}
            return ScoreInterviewResponse(
                overallScore=max(0, min(overall_score, 100)),
                dimensionScores=dimension_scores,
                dimensionDetails=self._normalize_standard_dimension_details(score_sources, raw_detail_map),
                scoreBreakdown=score_breakdown,
                rankPercentile=max(0, min(rank_percentile, 100)),
                modelVersion=self.model_version,
            )
        except Exception as exc:
            self._log_step_failure("score_interview", exc, round_count=len(request.rounds), input_summary_length=len(rounds_text), timeout_seconds=45.0)
            raise

    def generate_report(self, request: GenerateReportRequest) -> GenerateReportResponse:
        rounds_text = self._build_report_rounds_text(request.rounds)
        dimension_text = "\n".join([f"- {key}: score={value.score:.1f}, weight={value.weight:.2f}" for key, value in request.dimension_scores.items()])
        dimension_detail_text = "\n".join([f"- {key}: {self._truncate_with_marker(value, 120)}" for key, value in request.dimension_details.items()])
        try:
            payload = self._chat_json(
                step="generate_report",
                system_prompt="You are a Chinese interview report writer. Return JSON only with executiveSummary, strengths, weaknesses, detailedAnalysis, learningSuggestions, trainingPlan and nextInterviewFocus.",
                user_prompt=f"Position: {request.position_code}\nOverall score: {request.overall_score:.1f}\nDimension scores:\n{dimension_text or 'N/A'}\nDimension details:\n{dimension_detail_text or 'N/A'}\nInterview summary:\n{rounds_text or 'N/A'}\nBuild the report from scores first, then use the interview summary for evidence.",
                temperature=max(self.settings.temperature, 0.2),
                max_tokens=min(max(self.settings.max_tokens, 600), 1200),
                timeout_seconds=60.0,
            )
            executive_summary = self._normalize_executive_summary(payload)
            if not executive_summary:
                raise ValueError("missing executiveSummary")
            return GenerateReportResponse(
                executiveSummary=executive_summary,
                strengths=self._normalize_string_list(payload.get("strengths")),
                weaknesses=self._normalize_string_list(payload.get("weaknesses")),
                detailedAnalysis=self._normalize_detailed_analysis(payload.get("detailedAnalysis")),
                learningSuggestions=self._normalize_string_list(payload.get("learningSuggestions")),
                trainingPlan=self._normalize_training_plan(payload.get("trainingPlan")),
                nextInterviewFocus=self._normalize_string_list(payload.get("nextInterviewFocus")),
                modelVersion=self.model_version,
            )
        except Exception as exc:
            response_payload_snippet = self._sanitize_text(json.dumps(payload, ensure_ascii=False, default=str)[:320]) if "payload" in locals() else ""
            self._log_step_failure("generate_report", exc, timeout_seconds=60.0, input_summary_length=len(rounds_text), response_body_snippet=response_payload_snippet)
            raise

    def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        raise NotImplementedError("recommend_resources is not connected to real AI")

    def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        raise NotImplementedError("generate_training_plan is not connected to real AI")

    def search_rag(self, request: RagSearchRequest) -> RagSearchResponse:
        raise NotImplementedError("search_rag is not connected to real AI")

    def process_document(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        chunk_count = max(1, len(request.title) // 10 + 3)
        chunks = [ChunkResult(chunkIndex=index, content=f"[{request.title}] chunk {index + 1}", tokenCount=max(10, len(request.title) + 20), metadata={"source": "local-document-processor", "fileName": request.file_name, "fileType": request.file_type, "chunkIndex": index}) for index in range(chunk_count)]
        return ProcessDocumentResponse(documentId=request.document_id, chunks=chunks)

    def _build_compact_answer_prompt(self, request: AnswerInterviewRequest) -> str:
        current_title = request.current_main_question.title if request.current_main_question else "无"
        follow_up_count = request.current_main_question.follow_up_count if request.current_main_question else 0
        return (
            f"岗位：{request.position_name} ({request.position_code})\n"
            f"当前主问题：{current_title}\n"
            f"当前追问次数：{follow_up_count}\n"
            f"最新候选人回答：{self._build_latest_user_answer_text(request.recent_messages)}\n"
            f"已问主问题ID：{', '.join(str(x) for x in request.asked_question_ids) or '无'}\n"
            f"限制：主问题数 {request.limits.current_main_question_count}/{request.limits.max_main_questions}\n"
            f"可选题库：\n{self._build_question_bank_choices_text(request.question_bank)}\n"
            "请直接决定：继续追问、切换主问题，或结束面试。"
        )

    def _parse_answer_payload(self, request: AnswerInterviewRequest, payload: dict[str, Any]) -> AnswerInterviewResponse:
        action = self._as_text(payload.get("action")).lower()
        message_type = self._as_text(payload.get("messageType"))
        content = self._as_text(payload.get("content"))
        suggestions = self._as_str_list(payload.get("suggestions"))
        selected_question_id = self._parse_uuid(payload.get("selectedQuestionId"))
        metadata = self._as_dict(payload.get("metadata"))
        if action == "question":
            if not content or selected_question_id is None:
                raise ValueError("question action requires content and selectedQuestionId")
            if all(item.question_id != selected_question_id for item in request.question_bank):
                raise ValueError("selectedQuestionId is not in question bank")
            if selected_question_id in set(request.asked_question_ids):
                raise ValueError("selectedQuestionId was already asked")
            return AnswerInterviewResponse(action="question", messageType=message_type or "question", content=content, selectedQuestionId=selected_question_id, suggestions=suggestions, metadata=metadata)
        if action == "follow_up":
            if not content:
                raise ValueError("invalid follow_up payload")
            return AnswerInterviewResponse(action="follow_up", messageType=message_type or "follow_up", content=content, selectedQuestionId=None, suggestions=suggestions, metadata=metadata)
        if action == "finish":
            if not content:
                raise ValueError("invalid finish payload")
            return AnswerInterviewResponse(action="finish", messageType=message_type or "closing", content=content, selectedQuestionId=None, suggestions=suggestions, metadata=metadata)
        raise ValueError("invalid action response")

    def _get_http_client(self) -> httpx.Client:
        if self._http_client is None:
            self._http_client = get_shared_http_client(self.settings.base_url, self.settings.api_key)
        return self._http_client

    def _chat_text(self, *, step: str, system_prompt: str, user_prompt: str, temperature: float, max_tokens: int, timeout_seconds: float = 30.0) -> str:
        started_at = time.monotonic()
        client = self._get_http_client()
        try:
            response = client.post("chat/completions", json={"model": self.settings.model, "messages": [{"role": "system", "content": system_prompt}, {"role": "user", "content": user_prompt}], "temperature": temperature, "max_tokens": max_tokens}, timeout=timeout_seconds)
            response.raise_for_status()
            payload = response.json()
        except httpx.HTTPStatusError as exc:
            raise ProviderCallError(f"{step} upstream returned non-success status", status_code=exc.response.status_code if exc.response is not None else None, response_body_snippet=self._response_body_snippet(exc.response), timeout_seconds=timeout_seconds, elapsed_ms=round((time.monotonic() - started_at) * 1000, 2), received_response_headers=exc.response is not None) from exc
        except httpx.RequestError as exc:
            raise ProviderCallError(f"{step} request to upstream failed", timeout_seconds=timeout_seconds, elapsed_ms=round((time.monotonic() - started_at) * 1000, 2), received_response_headers=getattr(exc, "response", None) is not None) from exc
        except Exception as exc:
            raise ProviderCallError(f"{step} unexpected upstream error", timeout_seconds=timeout_seconds, elapsed_ms=round((time.monotonic() - started_at) * 1000, 2), received_response_headers=False) from exc
        try:
            content = payload["choices"][0]["message"]["content"]
        except Exception as exc:
            raise ValueError(f"{step} response format is invalid") from exc
        text = self._as_text(content)
        if not text:
            raise ValueError(f"{step} returned empty content")
        return text

    def _chat_json(self, *, step: str, system_prompt: str, user_prompt: str, temperature: float, max_tokens: int, timeout_seconds: float = 30.0) -> dict[str, Any]:
        content = self._chat_text(step=step, system_prompt=system_prompt, user_prompt=user_prompt, temperature=temperature, max_tokens=max_tokens, timeout_seconds=timeout_seconds)
        try:
            return json.loads(self._extract_json_object(content))
        except json.JSONDecodeError as exc:
            raise ValueError(f"{step} did not return valid JSON") from exc

    def _response_body_snippet(self, response: httpx.Response | None) -> str:
        return "" if response is None else self._sanitize_text(" ".join(response.text.split())[:320])

    def _sanitize_text(self, value: str) -> str:
        return "" if not value else value.replace(self.settings.api_key, "[REDACTED]")

    def _log_step_failure(self, step: str, exc: Exception, **context: Any) -> None:
        status_code = getattr(exc, "status_code", None)
        response_body_snippet = getattr(exc, "response_body_snippet", "") or context.get("response_body_snippet", "")
        timeout_seconds = getattr(exc, "timeout_seconds", None)
        elapsed_ms = getattr(exc, "elapsed_ms", None)
        received_response_headers = getattr(exc, "received_response_headers", False)
        inner_exception = exc.__cause__.__class__.__name__ if exc.__cause__ is not None else "n/a"
        logger.exception(
            "provider_call_failed step=%s provider=%s base_url=%s model=%s upstream_status_code=%s exception_type=%s inner_exception=%s timeout_seconds=%s elapsed_ms=%s received_response_headers=%s round_count=%s input_summary_length=%s response_body_snippet=%s",
            step, self.settings.provider, self.settings.base_url, self.settings.model, status_code if status_code is not None else "n/a", exc.__class__.__name__, inner_exception, timeout_seconds if timeout_seconds is not None else context.get("timeout_seconds", "n/a"), elapsed_ms if elapsed_ms is not None else "n/a", received_response_headers, context.get("round_count", "n/a"), context.get("input_summary_length", "n/a"), response_body_snippet,
        )

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
        return trimmed[start : end + 1] if start >= 0 and end > start else trimmed

    @staticmethod
    def _as_text(value: Any) -> str:
        return "" if value is None else str(value).strip()

    @staticmethod
    def _parse_uuid(value: Any) -> UUID | None:
        if value in (None, ""):
            return None
        try:
            return UUID(str(value))
        except (TypeError, ValueError):
            return None

    @staticmethod
    def _as_str_list(value: Any) -> list[str]:
        return [] if not isinstance(value, list) else [str(item).strip() for item in value if str(item).strip()]

    @staticmethod
    def _as_dict(value: Any) -> dict[str, Any]:
        return {} if not isinstance(value, dict) else {str(key): item for key, item in value.items()}

    @staticmethod
    def _as_number(value: Any) -> float | None:
        try:
            return None if value is None else float(value)
        except (TypeError, ValueError):
            return None

    @staticmethod
    def _normalize_detail_map(value: Any) -> dict[str, str]:
        return {} if not isinstance(value, dict) else {str(key): str(detail).strip() for key, detail in value.items() if str(detail).strip()}

    @staticmethod
    def _extract_dimension_details(value: Any) -> dict[str, str]:
        if not isinstance(value, dict):
            return {}
        result: dict[str, str] = {}
        for key, item in value.items():
            if isinstance(item, dict) and item.get("detail"):
                result[str(key)] = str(item["detail"]).strip()
        return result

    @staticmethod
    def _merge_detail_maps(*maps: dict[str, str]) -> dict[str, str]:
        result: dict[str, str] = {}
        for item in maps:
            for key, value in item.items():
                if key not in result and value:
                    result[key] = value
        return result

    @classmethod
    def _normalize_standard_dimension_scores(cls, score_sources: list[dict[str, Any]], overall_score: float) -> dict[str, DimensionScore]:
        result: dict[str, DimensionScore] = {}
        for key, weight in STANDARD_DIMENSION_WEIGHTS.items():
            raw_score = cls._resolve_dimension_score(score_sources, key)
            if raw_score is None:
                raw_score = overall_score
            result[key] = DimensionScore(score=max(0, min(raw_score, 100)), weight=weight)
        return result

    @classmethod
    def _normalize_standard_dimension_details(cls, score_sources: list[dict[str, Any]], raw_details: dict[str, str]) -> dict[str, str]:
        result: dict[str, str] = {}
        for key in STANDARD_DIMENSION_WEIGHTS:
            detail = cls._resolve_dimension_detail(score_sources, raw_details, key)
            if detail:
                result[key] = detail
        return result

    @classmethod
    def _resolve_dimension_score(cls, score_sources: list[dict[str, Any]], standard_key: str) -> float | None:
        aliases = STANDARD_DIMENSION_ALIASES.get(standard_key, (standard_key,))
        for source in score_sources:
            raw_value = cls._find_matching_dimension_value(source, aliases)
            raw_score = cls._extract_dimension_score(raw_value)
            if raw_score is not None:
                return raw_score
        return None

    @classmethod
    def _resolve_dimension_detail(cls, score_sources: list[dict[str, Any]], raw_details: dict[str, str], standard_key: str) -> str:
        aliases = STANDARD_DIMENSION_ALIASES.get(standard_key, (standard_key,))
        detail = cls._find_matching_dimension_text(raw_details, aliases)
        if detail:
            return detail
        for source in score_sources:
            raw_value = cls._find_matching_dimension_value(source, aliases)
            if isinstance(raw_value, dict):
                detail = cls._as_text(raw_value.get("detail"))
                if detail:
                    return detail
        return ""

    @classmethod
    def _find_matching_dimension_value(cls, source: dict[str, Any], aliases: tuple[str, ...]) -> Any:
        for alias in aliases:
            for key, value in source.items():
                if cls._dimension_key_matches(key, alias):
                    return value
        return None

    @classmethod
    def _find_matching_dimension_text(cls, source: dict[str, str], aliases: tuple[str, ...]) -> str:
        for alias in aliases:
            for key, value in source.items():
                if cls._dimension_key_matches(key, alias):
                    return value
        return ""

    @classmethod
    def _dimension_key_matches(cls, raw_key: Any, alias: str) -> bool:
        text = cls._as_text(raw_key)
        if not text:
            return False
        return text == alias or text.lower() == alias.lower()

    @classmethod
    def _extract_dimension_score(cls, raw_value: Any) -> float | None:
        if isinstance(raw_value, dict):
            return cls._as_number(raw_value.get("score"))
        return cls._as_number(raw_value)

    @classmethod
    def _build_score_rounds_text(cls, rounds: list[Any]) -> str:
        return cls._build_rounds_text(rounds, 1800, 450, 2, 120)

    @classmethod
    def _build_report_rounds_text(cls, rounds: list[Any]) -> str:
        return cls._build_rounds_text(rounds, 1200, 320, 1, 100)

    @classmethod
    def _build_rounds_text(cls, rounds: list[Any], total_limit: int, answer_limit: int, follow_up_count: int, follow_up_limit: int) -> str:
        parts: list[str] = []
        for round_item in rounds:
            follow_ups = round_item.follow_ups[-follow_up_count:] if round_item.follow_ups else []
            follow_up_text = " | ".join(cls._truncate_text(item, follow_up_limit) for item in follow_ups) if follow_ups else "N/A"
            parts.append("\n".join([f"Round {round_item.round_number}", f"Type: {cls._truncate_text(round_item.question_type, 30)}", f"Title: {cls._truncate_text(round_item.question_title, 100)}", f"Content: {cls._truncate_text(round_item.question_content, 180)}", f"Answer: {cls._truncate_text(round_item.answer or 'N/A', answer_limit)}", f"FollowUps: {follow_up_text}"]))
        return cls._truncate_with_marker("\n\n".join(parts), total_limit)

    @classmethod
    def _build_question_bank_text(cls, questions: list[Any], total_limit: int) -> str:
        if not questions:
            return "无"
        parts = ["\n".join([f"- ID: {item.question_id}", f"  Title: {cls._truncate_text(item.title, 120)}", f"  Type: {cls._truncate_text(item.type, 30)}", f"  Difficulty: {cls._truncate_text(item.difficulty, 20)}", f"  Content: {cls._truncate_text(item.content, 220)}"]) for item in questions]
        return cls._truncate_with_marker("\n".join(parts), total_limit)

    @classmethod
    def _build_question_bank_choices_text(cls, questions: list[Any]) -> str:
        if not questions:
            return "无"
        parts = [f"- ID: {item.question_id} | Title: {cls._truncate_text(item.title, 80)} | Type: {cls._truncate_text(item.type, 20)}" for item in questions]
        return cls._truncate_with_marker("\n".join(parts), 800)

    @classmethod
    def _build_recent_messages_text(cls, messages: list[Any]) -> str:
        if not messages:
            return "无"
        parts = [f"{item.sequence}. {item.role}/{item.message_type}: {cls._truncate_text(item.content, 220)}" for item in messages[-8:]]
        return cls._truncate_with_marker("\n".join(parts), 1200)

    @classmethod
    def _build_latest_user_answer_text(cls, messages: list[Any]) -> str:
        for item in reversed(messages):
            if item.role == "user":
                return cls._truncate_text(item.content, 300)
        return "无"

    @classmethod
    def _build_history_summaries_text(cls, summaries: list[str]) -> str:
        if not summaries:
            return "无"
        return cls._truncate_with_marker("\n".join(cls._truncate_text(item, 180) for item in summaries[-5:]), 1200)

    @staticmethod
    def _truncate_text(value: str, limit: int) -> str:
        if len(value) <= limit:
            return value
        return value[: limit - len("[TRUNCATED]")] + "[TRUNCATED]"

    @classmethod
    def _truncate_with_marker(cls, value: str, limit: int, marker: str = "[TRUNCATED]") -> str:
        return value if len(value) <= limit else value[: limit - len(marker)] + marker

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
            return {"summary": value.strip()} if value.strip() else {}
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
