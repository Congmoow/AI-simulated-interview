from __future__ import annotations

import re

from app.schemas.document import ChunkResult, ProcessDocumentRequest, ProcessDocumentResponse
from app.schemas.interview import (
    AnswerInterviewRequest,
    AnswerInterviewResponse,
    ScoreAndReportResponse,
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


_MOCK_OPENING_PREFIXES = {
    "friendly": "你好，我是今天陪你模拟面试的陈航，不用紧张，我们慢慢来。",
    "standard": "你好，我是陈航，今天由我来做这场技术面试。",
    "stress": "你好，我是陈航，时间有限，我们直接开始。",
}
_MOCK_GREETING_DEFLECT_PREFIXES = {
    "friendly": "嗯，这句还没进入话题，我们先回到当前问题。",
    "standard": "嗯，先回到当前问题。",
    "stress": "先别寒暄，回到当前问题。",
}
_MOCK_FOLLOWUP_PREFIXES = {
    "friendly": "挺好的，可以再具体一些。",
    "standard": "嗯，我想再问细一点。",
    "stress": "这样讲太笼统了，再具体些。",
}
_MOCK_NEXT_QUESTION_PREFIXES = {
    "friendly": "明白了，我们换个方向聊聊。",
    "standard": "嗯，接着问下一题。",
    "stress": "OK，换题。",
}
_MOCK_CLOSING_LINES = {
    "friendly": "好的，本次面试先到这里，感谢你的分享，接下来我会基于刚才的交流为你生成报告。",
    "standard": "本次面试先到这里，接下来我会基于刚才的交流为你生成报告。",
    "stress": "本次面试到此结束，接下来看报告。",
}


def _mock_prefix(mapping: dict[str, str], interview_mode: str) -> str:
    normalized = (interview_mode or "").strip().lower()
    return mapping.get(normalized, mapping["standard"])


class MockProvider:
    _GENERIC_ACKNOWLEDGEMENTS = {
        "hi",
        "hello",
        "hey",
        "你好",
        "您好",
        "你好啊",
        "嗨",
        "哈喽",
        "在吗",
        "在么",
    }

    async def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        question = next(
            (item for item in request.question_bank if item.question_id not in set(request.asked_question_ids)),
            request.question_bank[0],
        )
        prefix = _mock_prefix(_MOCK_OPENING_PREFIXES, request.interview_mode)
        content = f"{prefix}\n\n{question.content}"
        return StartInterviewResponse(
            action="question",
            messageType="opening",
            content=content,
            selectedQuestionId=question.question_id,
            suggestions=["先讲背景", "再讲职责与结果"],
            metadata={"selectedQuestionTitle": question.title},
        )

    async def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        current_question = request.current_main_question
        current_related_question_id = current_question.question_id if current_question is not None else None
        latest_user_message = next(
            (item for item in reversed(request.recent_messages) if item.role == "user"),
            None,
        )
        normalized_answer = latest_user_message.content.strip() if latest_user_message is not None else ""
        compact_answer = self._normalize_answer(normalized_answer)

        if current_question is not None and self._is_generic_acknowledgement(compact_answer):
            prefix = _mock_prefix(_MOCK_GREETING_DEFLECT_PREFIXES, request.interview_mode)
            return AnswerInterviewResponse(
                action="follow_up",
                messageType="follow_up",
                content=(
                    f"{prefix}我们先围绕当前问题回答："
                    f"{current_question.asked_content}。"
                    "请基于真实项目说明背景、职责、系统规模和你做过的优化。"
                ),
                suggestions=["先按背景-职责-规模-优化展开"],
                metadata={"anchorQuestionId": str(current_related_question_id) if current_related_question_id else ""},
            )

        if current_question is not None and current_question.follow_up_count == 0 and len(normalized_answer) < 120:
            prefix = _mock_prefix(_MOCK_FOLLOWUP_PREFIXES, request.interview_mode)
            return AnswerInterviewResponse(
                action="follow_up",
                messageType="follow_up",
                content=f"{prefix}能补充一下你的职责边界、关键难点和最终结果吗？",
                suggestions=["按背景-职责-难点-结果展开"],
                metadata={"anchorQuestionId": str(current_related_question_id) if current_related_question_id else ""},
            )

        unasked_questions = [
            item for item in request.question_bank if item.question_id not in set(request.asked_question_ids)
        ]
        if request.limits.current_main_question_count >= request.limits.max_main_questions or not unasked_questions:
            closing = _mock_prefix(_MOCK_CLOSING_LINES, request.interview_mode)
            return AnswerInterviewResponse(
                action="finish",
                messageType="closing",
                content=closing,
                suggestions=["查看报告"],
                metadata={"finishReason": "limit_or_exhausted"},
            )

        next_question = unasked_questions[0]
        prefix = _mock_prefix(_MOCK_NEXT_QUESTION_PREFIXES, request.interview_mode)
        return AnswerInterviewResponse(
            action="question",
            messageType="question",
            content=f"{prefix}{next_question.content}",
            selectedQuestionId=next_question.question_id,
            suggestions=["结合真实经历回答"],
            metadata={"selectedQuestionTitle": next_question.title},
        )

    async def answer_interview_streaming(self, request: AnswerInterviewRequest):
        result = await self.answer_interview(request)
        yield {"type": "chunk", "text": result.content}
        yield {"type": "done", "response": result}

    @classmethod
    def _normalize_answer(cls, answer: str) -> str:
        return re.sub(r"[\s\W_]+", "", answer.lower())

    @classmethod
    def _is_generic_acknowledgement(cls, answer: str) -> bool:
        return answer in cls._GENERIC_ACKNOWLEDGEMENTS

    async def score_interview(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        answered_rounds = [round_item for round_item in request.rounds if round_item.answer]
        completion_ratio = len(answered_rounds) / max(len(request.rounds), 1)
        overall_score = round(68 + completion_ratio * 18 + min(len(answered_rounds), 5) * 1.6, 2)
        score_breakdown: dict[str, object] = {}
        for round_item in answered_rounds:
            round_score = min(60 + len((round_item.answer or "").strip()) / 8, 92)
            score_breakdown[f"round{round_item.round_number}"] = {
                "technicalAccuracy": round(round_score, 1),
                "depth": round(round_score - 4, 1),
                "clarity": round(round_score - 2, 1),
                "overall": round(round_score, 1),
            }
            score_breakdown[f"round{round_item.round_number}Difficulty"] = "medium"

        return ScoreInterviewResponse(
            overallScore=overall_score,
            rankPercentile=round(min(99, overall_score + 8), 2),
            dimensionScores={
                "technicalAccuracy": {"score": round(min(95, overall_score + 1), 2), "weight": 0.3},
                "knowledgeDepth": {"score": round(max(55, overall_score - 2), 2), "weight": 0.2},
                "logicalThinking": {"score": round(overall_score, 2), "weight": 0.15},
                "positionMatch": {"score": round(min(94, overall_score + 3), 2), "weight": 0.15},
                "projectAuthenticity": {"score": round(max(58, overall_score - 1), 2), "weight": 0.1},
                "fluency": {"score": round(min(96, overall_score + 4), 2), "weight": 0.05},
                "clarity": {"score": round(min(96, overall_score + 2), 2), "weight": 0.03},
                "confidence": {"score": round(min(95, overall_score + 1), 2), "weight": 0.02},
            },
            dimensionDetails={
                "technicalAccuracy": "Technical topics connected back to project context",
            },
            scoreBreakdown=score_breakdown,
            modelVersion="mock-provider-v2",
        )

    async def generate_report(self, request: GenerateReportRequest) -> GenerateReportResponse:
        return GenerateReportResponse(
            executiveSummary="mock summary",
            strengths=["结构清晰"],
            weaknesses=["深度不足"],
            detailedAnalysis={"topic": "core principles"},
            learningSuggestions=["补强底层原理"],
            trainingPlan=[],
            nextInterviewFocus=["project trade-offs"],
            modelVersion="mock-provider-v2",
        )

    async def score_and_report_interview(self, request: ScoreInterviewRequest) -> ScoreAndReportResponse:
        score = await self.score_interview(request)
        return ScoreAndReportResponse(
            overallScore=score.overall_score,
            dimensionScores=score.dimension_scores,
            dimensionDetails=score.dimension_details,
            scoreBreakdown=score.score_breakdown,
            rankPercentile=score.rank_percentile,
            executiveSummary="mock summary",
            strengths=["结构清晰"],
            weaknesses=["深度不足"],
            detailedAnalysis={"topic": "core principles"},
            learningSuggestions=["补强底层原理"],
            trainingPlan=[],
            nextInterviewFocus=["project trade-offs"],
            modelVersion="mock-provider-v2",
        )

    async def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        raise NotImplementedError

    async def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        raise NotImplementedError

    async def search_rag(self, request: RagSearchRequest) -> RagSearchResponse:
        raise NotImplementedError

    async def process_document(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        chunk_count = max(1, len(request.title) // 10 + 3)
        chunks = [
            ChunkResult(
                chunkIndex=index,
                content=f"[{request.title}] chunk {index + 1}",
                tokenCount=max(10, len(request.title) + 20),
                metadata={
                    "source": "mock-provider",
                    "fileName": request.file_name,
                    "fileType": request.file_type,
                    "chunkIndex": index,
                },
            )
            for index in range(chunk_count)
        ]
        return ProcessDocumentResponse(documentId=request.document_id, chunks=chunks)
