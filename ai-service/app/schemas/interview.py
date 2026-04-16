from typing import Any
from uuid import UUID

from pydantic import BaseModel, Field


class CandidateQuestion(BaseModel):
    question_id: UUID = Field(alias="questionId")
    title: str
    type: str
    content: str
    difficulty: str


class StartInterviewRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    position_code: str = Field(alias="positionCode")
    interview_mode: str = Field(alias="interviewMode")
    round_number: int = Field(alias="roundNumber")
    question_types: list[str] = Field(default_factory=list, alias="questionTypes")
    source_question: CandidateQuestion = Field(alias="sourceQuestion")


class StartInterviewResponse(BaseModel):
    question_id: UUID = Field(alias="questionId")
    title: str
    type: str
    content: str
    suggestions: list[str] = Field(default_factory=list)


class AnswerInterviewRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    round_number: int = Field(alias="roundNumber")
    interview_mode: str = Field(alias="interviewMode")
    position_code: str = Field(alias="positionCode")
    question_title: str = Field(alias="questionTitle")
    question_content: str = Field(alias="questionContent")
    answer: str
    follow_up_count: int = Field(alias="followUpCount")
    current_round: int = Field(alias="currentRound")
    total_rounds: int = Field(alias="totalRounds")
    next_question_candidate: CandidateQuestion | None = Field(default=None, alias="nextQuestionCandidate")


class AnswerInterviewResponse(BaseModel):
    type: str
    content: str
    suggestions: list[str] = Field(default_factory=list)
    next_question: CandidateQuestion | None = Field(default=None, alias="nextQuestion")


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
