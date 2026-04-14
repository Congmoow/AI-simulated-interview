from typing import Any
from uuid import UUID

from pydantic import BaseModel, Field


class ResourceRecommendationRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    position_code: str = Field(alias="positionCode")
    weaknesses: list[str] = Field(default_factory=list)


class ResourceRecommendationResponse(BaseModel):
    target_dimensions: list[str] = Field(default_factory=list, alias="targetDimensions")
    match_scores: dict[str, float] = Field(default_factory=dict, alias="matchScores")
    reason: str


class TrainingPlanRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    position_code: str = Field(alias="positionCode")
    weaknesses: list[str] = Field(default_factory=list)


class TrainingPlanResponse(BaseModel):
    weeks: int = 4
    daily_commitment: str = Field(alias="dailyCommitment")
    goals: list[str] = Field(default_factory=list)
    schedule: list[Any] = Field(default_factory=list)
    milestones: list[Any] = Field(default_factory=list)
