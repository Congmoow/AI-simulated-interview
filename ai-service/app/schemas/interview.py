from typing import Any
from uuid import UUID

from pydantic import BaseModel, Field


class CandidateQuestion(BaseModel):
    question_id: UUID = Field(alias="questionId")
    title: str
    type: str
    content: str
    difficulty: str


class CurrentMainQuestion(BaseModel):
    round_number: int = Field(alias="roundNumber")
    question_id: UUID = Field(alias="questionId")
    title: str
    type: str
    asked_content: str = Field(alias="askedContent")
    follow_up_count: int = Field(alias="followUpCount")


class InterviewMessage(BaseModel):
    role: str
    message_type: str = Field(alias="messageType")
    content: str
    related_question_id: UUID | None = Field(default=None, alias="relatedQuestionId")
    sequence: int


class InterviewLimits(BaseModel):
    max_main_questions: int = Field(alias="maxMainQuestions")
    current_main_question_count: int = Field(alias="currentMainQuestionCount")
    max_messages: int = Field(alias="maxMessages")
    current_message_count: int = Field(alias="currentMessageCount")
    max_duration_minutes: int = Field(alias="maxDurationMinutes")
    current_duration_minutes: int = Field(alias="currentDurationMinutes")


class StartInterviewRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    position_code: str = Field(alias="positionCode")
    position_name: str = Field(alias="positionName")
    interview_mode: str = Field(alias="interviewMode")
    question_types: list[str] = Field(default_factory=list, alias="questionTypes")
    question_bank: list[CandidateQuestion] = Field(default_factory=list, alias="questionBank")
    asked_question_ids: list[UUID] = Field(default_factory=list, alias="askedQuestionIds")
    current_main_question: CurrentMainQuestion | None = Field(default=None, alias="currentMainQuestion")
    recent_messages: list[InterviewMessage] = Field(default_factory=list, alias="recentMessages")
    history_answer_summaries: list[str] = Field(default_factory=list, alias="historyAnswerSummaries")
    limits: InterviewLimits


class StartInterviewResponse(BaseModel):
    action: str
    message_type: str = Field(alias="messageType")
    content: str
    selected_question_id: UUID | None = Field(default=None, alias="selectedQuestionId")
    suggestions: list[str] = Field(default_factory=list)
    metadata: dict[str, Any] = Field(default_factory=dict)


class AnswerInterviewRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    position_code: str = Field(alias="positionCode")
    position_name: str = Field(alias="positionName")
    interview_mode: str = Field(alias="interviewMode")
    question_bank: list[CandidateQuestion] = Field(default_factory=list, alias="questionBank")
    asked_question_ids: list[UUID] = Field(default_factory=list, alias="askedQuestionIds")
    current_main_question: CurrentMainQuestion | None = Field(default=None, alias="currentMainQuestion")
    recent_messages: list[InterviewMessage] = Field(default_factory=list, alias="recentMessages")
    history_answer_summaries: list[str] = Field(default_factory=list, alias="historyAnswerSummaries")
    limits: InterviewLimits


class AnswerInterviewResponse(BaseModel):
    action: str
    message_type: str = Field(alias="messageType")
    content: str
    selected_question_id: UUID | None = Field(default=None, alias="selectedQuestionId")
    suggestions: list[str] = Field(default_factory=list)
    metadata: dict[str, Any] = Field(default_factory=dict)


class ScoreRound(BaseModel):
    round_number: int = Field(alias="roundNumber")
    question_type: str = Field(alias="questionType")
    question_title: str = Field(alias="questionTitle")
    question_content: str = Field(alias="questionContent")
    answer: str | None = None
    follow_ups: list[str] = Field(default_factory=list, alias="followUps")


class ScoreInterviewRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    position_code: str = Field(alias="positionCode")
    rounds: list[ScoreRound] = Field(default_factory=list)


class DimensionScore(BaseModel):
    score: float
    weight: float


class ScoreInterviewResponse(BaseModel):
    overall_score: float = Field(alias="overallScore")
    dimension_scores: dict[str, DimensionScore] = Field(default_factory=dict, alias="dimensionScores")
    dimension_details: dict[str, str] = Field(default_factory=dict, alias="dimensionDetails")
    score_breakdown: dict[str, Any] = Field(default_factory=dict, alias="scoreBreakdown")
    rank_percentile: float = Field(alias="rankPercentile")
    model_version: str = Field(alias="modelVersion")
