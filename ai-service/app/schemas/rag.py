from typing import Any

from pydantic import BaseModel, Field


class RagSearchRequest(BaseModel):
    query: str = Field(min_length=1, max_length=2000)
    position_code: str | None = Field(default=None, alias="positionCode")
    top_k: int = Field(default=5, alias="topK", ge=1, le=100)


class RagSearchItem(BaseModel):
    chunk_id: str = Field(alias="chunkId", min_length=1, max_length=200)
    title: str = Field(min_length=1, max_length=500)
    content: str = Field(min_length=1, max_length=100000)
    score: float = Field(ge=0, le=1)
    metadata: dict[str, Any] = Field(default_factory=dict)


class RagSearchResponse(BaseModel):
    items: list[RagSearchItem] = Field(default_factory=list)
