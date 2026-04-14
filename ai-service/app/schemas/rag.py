from typing import Any

from pydantic import BaseModel, Field


class RagSearchRequest(BaseModel):
    query: str
    position_code: str | None = Field(default=None, alias="positionCode")
    top_k: int = Field(default=5, alias="topK")


class RagSearchItem(BaseModel):
    chunk_id: str = Field(alias="chunkId")
    title: str
    content: str
    score: float
    metadata: dict[str, Any] = Field(default_factory=dict)


class RagSearchResponse(BaseModel):
    items: list[RagSearchItem] = Field(default_factory=list)
