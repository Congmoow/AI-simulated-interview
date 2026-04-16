from uuid import uuid4

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
from app.schemas.rag import RagSearchItem, RagSearchRequest, RagSearchResponse
from app.schemas.recommendation import (
    ResourceRecommendationRequest,
    ResourceRecommendationResponse,
    TrainingPlanRequest,
    TrainingPlanResponse,
)
from app.schemas.report import GenerateReportRequest, GenerateReportResponse


class MockProvider(ModelProvider):
    model_version = "mock-v1"

    def start_interview(self, request: StartInterviewRequest) -> StartInterviewResponse:
        question = request.source_question
        return StartInterviewResponse(
            questionId=question.question_id,
            title=question.title,
            type=question.type,
            content=question.content,
            suggestions=[
                "Start with the business context",
                "Explain your personal ownership",
                "Close with trade-offs",
            ],
        )

    def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        normalized_answer = request.answer.strip()
        if request.follow_up_count == 0 and len(normalized_answer) < 120:
            return AnswerInterviewResponse(
                type="follow_up",
                content="Your answer has the right direction. Add one implementation detail and explain why you chose it.",
                suggestions=[
                    "Mention a key config or code path",
                    "Explain a performance or stability gain",
                    "Clarify your own contribution",
                ],
                nextQuestion=None,
            )

        if request.current_round >= request.total_rounds or request.next_question_candidate is None:
            return AnswerInterviewResponse(
                type="follow_up",
                content="This round can end here. You can finish the interview and generate the report.",
                suggestions=["Finish the interview", "Review the current round", "Check your current performance"],
                nextQuestion=None,
            )

        return AnswerInterviewResponse(
            type="next_question",
            content=request.next_question_candidate.title,
            suggestions=[
                "State the conclusion first",
                "Describe trade-offs and risks",
                "Keep the pace steady",
            ],
            nextQuestion=request.next_question_candidate,
        )

    def score_interview(self, request: ScoreInterviewRequest) -> ScoreInterviewResponse:
        answered_rounds = [round_item for round_item in request.rounds if round_item.answer]
        completion_ratio = len(answered_rounds) / max(len(request.rounds), 1)
        overall_score = round(68 + completion_ratio * 18 + min(len(answered_rounds), 5) * 1.6, 2)

        dimension_scores = {
            "technicalAccuracy": DimensionScore(score=min(overall_score + 2, 95), weight=0.30),
            "knowledgeDepth": DimensionScore(score=min(overall_score - 1, 92), weight=0.20),
            "logicalThinking": DimensionScore(score=min(overall_score + 1.5, 94), weight=0.15),
            "positionMatch": DimensionScore(score=min(overall_score + 0.5, 93), weight=0.15),
            "projectAuthenticity": DimensionScore(score=min(overall_score - 2, 90), weight=0.10),
            "fluency": DimensionScore(score=min(overall_score + 3, 96), weight=0.05),
            "clarity": DimensionScore(score=min(overall_score + 2.5, 96), weight=0.03),
            "confidence": DimensionScore(score=min(overall_score + 1, 95), weight=0.02),
        }

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
            dimensionScores=dimension_scores,
            dimensionDetails={
                "technicalAccuracy": "Fundamentals are stable, but more implementation detail would help.",
                "knowledgeDepth": "The main path is covered. More low-level explanation is still needed.",
                "logicalThinking": "The answer structure is clear and conclusions connect well to examples.",
            },
            scoreBreakdown=score_breakdown,
            rankPercentile=min(88.0, overall_score),
            modelVersion=self.model_version,
        )

    def generate_report(self, request: GenerateReportRequest) -> GenerateReportResponse:
        overall = request.overall_score
        return GenerateReportResponse(
            executiveSummary=f"The mock interview was stable overall, with a score of {overall:.0f}.",
            strengths=[
                "Clear answer structure",
                "Technical topics connected back to project context",
                "Steady communication rhythm",
            ],
            weaknesses=[
                "Low-level explanations can still go deeper",
                "Trade-offs can be stated more explicitly",
            ],
            detailedAnalysis={
                "technicalAccuracy": "Answers are mostly correct, but some details stay at a high level.",
                "projectAuthenticity": "Project examples sound credible; more business constraints would help.",
            },
            learningSuggestions=[
                "Review core principles behind your weak areas",
                "Turn recent projects into STAR stories",
                "Practice timed verbal answers twice a week",
            ],
            trainingPlan=[
                {
                    "week": 1,
                    "topic": "core principles",
                    "tasks": ["整理 5 个高频原理题", "用自己的话重讲一遍"],
                },
                {
                    "week": 2,
                    "topic": "project narrative",
                    "tasks": ["沉淀 3 个项目案例", "补齐指标和结果"],
                },
            ],
            nextInterviewFocus=["low-level mechanism", "project trade-offs", "pressure follow-up"],
            modelVersion=self.model_version,
        )

    def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        return ResourceRecommendationResponse(
            targetDimensions=["technicalAccuracy", "knowledgeDepth", "clarity"],
            matchScores={
                "technicalAccuracy": 0.95,
                "knowledgeDepth": 0.91,
                "clarity": 0.88,
            },
            reason="Mock recommendation based on weaker dimensions.",
        )

    def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        return TrainingPlanResponse(
            weeks=4,
            dailyCommitment="2 hours",
            goals=["strengthen weak dimensions", "improve high-frequency answers"],
            schedule=[],
            milestones=[],
        )

    def search_rag(self, request: RagSearchRequest) -> RagSearchResponse:
        return RagSearchResponse(
            query=request.query,
            items=[
                RagSearchItem(
                    documentId=uuid4(),
                    chunkId=str(uuid4()),
                    title="Mock knowledge",
                    content="Mock RAG result",
                    score=0.88,
                    metadata={"positionCode": request.position_code},
                )
            ],
        )

    def process_document(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        return ProcessDocumentResponse(
            documentId=request.document_id,
            chunks=[
                ChunkResult(
                    chunkIndex=0,
                    content=f"Processed: {request.title}",
                    tokenCount=max(20, len(request.title)),
                    metadata={"source": "mock-provider"},
                )
            ],
        )
