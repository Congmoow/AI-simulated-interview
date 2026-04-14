from uuid import uuid4

from app.providers.base import ModelProvider
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
from app.schemas.recommendation import (
    ResourceRecommendationRequest,
    ResourceRecommendationResponse,
    TrainingPlanRequest,
    TrainingPlanResponse,
)
from app.schemas.report import GenerateReportRequest, GenerateReportResponse
from app.schemas.rag import RagSearchItem, RagSearchRequest, RagSearchResponse
from app.schemas.document import ChunkResult, ProcessDocumentRequest, ProcessDocumentResponse


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
                "先给出核心结论",
                "补充一段真实项目背景",
                "最后说明你的取舍依据",
            ],
        )

    def answer_interview(self, request: AnswerInterviewRequest) -> AnswerInterviewResponse:
        normalized_answer = request.answer.strip()
        if request.follow_up_count == 0 and len(normalized_answer) < 120:
            return AnswerInterviewResponse(
                type="follow_up",
                content="你的回答已经抓到主线了，但还不够具体。请补充一个你亲自做过的实现细节，并说明为什么这么做。",
                suggestions=[
                    "补充关键配置或核心代码点",
                    "说明性能或稳定性收益",
                    "讲清楚你的个人贡献",
                ],
                nextQuestion=None,
            )

        if request.current_round >= request.total_rounds or request.next_question_candidate is None:
            return AnswerInterviewResponse(
                type="follow_up",
                content="当前轮次已经可以结束，你可以主动结束面试并生成报告。",
                suggestions=["结束面试", "复盘本题", "查看当前表现"],
                nextQuestion=None,
            )

        return AnswerInterviewResponse(
            type="next_question",
            content=request.next_question_candidate.title,
            suggestions=[
                "先搭结构，再补案例",
                "优先讲取舍和风险",
                "注意回答节奏",
            ],
            nextQuestion=request.next_question_candidate,
        )

    def finish_interview(self, request: FinishInterviewRequest) -> FinishInterviewResponse:
        return FinishInterviewResponse(summary=f"{request.position_code} 面试已结束，共完成 {request.total_rounds} 轮。")

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
                "technicalAccuracy": "基础原理掌握较稳，但还可以再补细节。",
                "knowledgeDepth": "回答已覆盖主线，建议增加底层机制说明。",
                "logicalThinking": "结构化表达较好，结论和案例衔接自然。",
            },
            scoreBreakdown=score_breakdown,
            rankPercentile=min(88.0, overall_score),
            modelVersion=self.model_version,
        )

    def generate_report(self, request: GenerateReportRequest) -> GenerateReportResponse:
        overall = request.overall_score
        return GenerateReportResponse(
            executiveSummary=f"本次模拟面试整体表现稳定，综合得分 {overall:.0f} 分，已经具备较好的岗位匹配度。",
            strengths=[
                "回答结构清晰，能先给结论再展开说明。",
                "能够把技术点和项目背景联系起来。",
                "表达节奏稳定，面试沟通感较好。",
            ],
            weaknesses=[
                "底层原理解释不够深入。",
                "关键取舍点可以再更明确。",
            ],
            detailedAnalysis={
                "technicalAccuracy": "回答准确度较高，但少量细节仍偏概述。",
                "projectAuthenticity": "项目案例有可信度，建议补更多业务约束。",
            },
            learningSuggestions=[
                "针对薄弱点补一轮底层原理梳理。",
                "把最近项目拆成 3 个 STAR 叙事模板。",
                "每周做 2 次限时口述训练。",
            ],
            trainingPlan=[
                {
                    "week": 1,
                    "topic": "核心原理补强",
                    "tasks": ["整理 5 个高频原理题", "用自己的话重讲一遍"],
                },
                {
                    "week": 2,
                    "topic": "项目叙事演练",
                    "tasks": ["沉淀 3 个项目案例", "补齐指标和结果"],
                },
            ],
            nextInterviewFocus=["底层机制", "项目取舍", "压力追问"],
            modelVersion=self.model_version,
        )

    def recommend_resources(self, request: ResourceRecommendationRequest) -> ResourceRecommendationResponse:
        target_dimensions = ["technicalAccuracy", "knowledgeDepth", "clarity"]
        return ResourceRecommendationResponse(
            targetDimensions=target_dimensions,
            matchScores={
                "technicalAccuracy": 0.95,
                "knowledgeDepth": 0.91,
                "clarity": 0.88,
            },
            reason="根据本次面试中的薄弱项，优先推荐基础原理和结构化表达相关资源。",
        )

    def generate_training_plan(self, request: TrainingPlanRequest) -> TrainingPlanResponse:
        return TrainingPlanResponse(
            weeks=4,
            dailyCommitment="2小时",
            goals=["补强薄弱维度", "提升高频题表达效率"],
            schedule=[
                {
                    "week": 1,
                    "focus": "原理梳理",
                    "dailyPlan": [
                        {"day": 1, "topic": "高频原理题", "tasks": ["知识点归纳", "5 分钟复述"]},
                        {"day": 2, "topic": "项目核心链路", "tasks": ["梳理架构图", "准备口述稿"]},
                    ],
                    "practiceTask": {
                        "type": "simulation",
                        "title": "完成一次 20 分钟技术口述",
                        "expectedDuration": 20,
                    },
                }
            ],
            milestones=[
                {"week": 2, "target": "完成一轮高频题复盘"},
                {"week": 4, "target": "能稳定完成两轮追问回答"},
            ],
        )

    def search_rag(self, request: RagSearchRequest) -> RagSearchResponse:
        top_k = max(1, min(request.top_k, 10))
        items = [
            RagSearchItem(
                chunkId=str(uuid4()),
                title=f"{request.position_code or '通用'} 知识片段 {index + 1}",
                content=f"这是关于“{request.query}”的占位检索结果，可在后续阶段替换为真实向量召回。",
                score=round(0.92 - index * 0.05, 2),
                metadata={"source": "mock-provider", "rank": index + 1},
            )
            for index in range(top_k)
        ]
        return RagSearchResponse(items=items)

    def process_document(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        chunk_count = max(1, len(request.title) // 10 + 3)
        chunks = [
            ChunkResult(
                chunkIndex=i,
                content=f"[{request.title}] 知识片段 {i + 1}：这是占位内容，后续替换为真实文本抽取结果。",
                tokenCount=max(10, len(request.title) + 20),
                metadata={
                    "source": "mock-provider",
                    "fileName": request.file_name,
                    "fileType": request.file_type,
                    "chunkIndex": i,
                },
            )
            for i in range(chunk_count)
        ]
        return ProcessDocumentResponse(documentId=request.document_id, chunks=chunks)
