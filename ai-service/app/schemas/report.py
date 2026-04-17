from typing import Any
from uuid import UUID

from pydantic import BaseModel, Field

from app.schemas.interview import DimensionScore, ScoreRound


class GenerateReportRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    position_code: str = Field(alias="positionCode")
    overall_score: float = Field(alias="overallScore")
    dimension_scores: dict[str, DimensionScore] = Field(default_factory=dict, alias="dimensionScores")
    dimension_details: dict[str, str] = Field(default_factory=dict, alias="dimensionDetails")
    rounds: list[ScoreRound] = Field(default_factory=list, alias="rounds")


class GenerateReportResponse(BaseModel):
    executive_summary: str = Field(alias="executiveSummary")
    strengths: list[str] = Field(default_factory=list)
    weaknesses: list[str] = Field(default_factory=list)
    detailed_analysis: dict[str, Any] = Field(default_factory=dict, alias="detailedAnalysis")
    learning_suggestions: list[str] = Field(default_factory=list, alias="learningSuggestions")
    training_plan: list[Any] = Field(default_factory=list, alias="trainingPlan")
    next_interview_focus: list[str] = Field(default_factory=list, alias="nextInterviewFocus")
    model_version: str = Field(alias="modelVersion")
