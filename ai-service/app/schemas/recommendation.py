from typing import Any
from uuid import UUID

from pydantic import BaseModel, Field


class ResourceRecommendationRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    position_code: str = Field(alias="positionCode", min_length=1, max_length=100)
    weaknesses: list[str] = Field(default_factory=list)


class ResourceRecommendationResponse(BaseModel):
    target_dimensions: list[str] = Field(default_factory=list, alias="targetDimensions")
    match_scores: dict[str, float] = Field(default_factory=dict, alias="matchScores")
    reason: str = Field(min_length=1, max_length=5000)


class TrainingPlanRequest(BaseModel):
    interview_id: UUID = Field(alias="interviewId")
    position_code: str = Field(alias="positionCode", min_length=1, max_length=100)
    weaknesses: list[str] = Field(default_factory=list)


class TrainingPlanResponse(BaseModel):
    weeks: int = Field(default=4, ge=1, le=52)
    daily_commitment: str = Field(alias="dailyCommitment", min_length=1, max_length=200)
    goals: list[str] = Field(default_factory=list)
    schedule: list[Any] = Field(default_factory=list)
    milestones: list[Any] = Field(default_factory=list)
