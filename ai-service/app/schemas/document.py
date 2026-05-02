from typing import Any

from pydantic import BaseModel, Field


class ProcessDocumentRequest(BaseModel):
    document_id: str = Field(alias="documentId", min_length=1, max_length=200)
    file_name: str = Field(alias="fileName", min_length=1, max_length=500)
    file_type: str = Field(alias="fileType", min_length=1, max_length=50)
    title: str = Field(min_length=1, max_length=500)

    model_config = {"populate_by_name": True}


class ChunkResult(BaseModel):
    chunk_index: int = Field(alias="chunkIndex", ge=0)
    content: str = Field(min_length=1, max_length=100000)
    token_count: int = Field(alias="tokenCount", ge=0)
    metadata: dict[str, Any] = Field(default_factory=dict)

    model_config = {"populate_by_name": True}


class ProcessDocumentResponse(BaseModel):
    document_id: str = Field(alias="documentId")
    chunks: list[ChunkResult] = Field(default_factory=list)

    model_config = {"populate_by_name": True}


class EnqueueDocumentRequest(BaseModel):
    document_id: str = Field(alias="documentId", min_length=1, max_length=200)
    file_name: str = Field(alias="fileName", min_length=1, max_length=500)
    file_type: str = Field(alias="fileType", min_length=1, max_length=50)
    title: str = Field(min_length=1, max_length=500)

    model_config = {"populate_by_name": True}


class EnqueueDocumentResponse(BaseModel):
    task_id: str = Field(alias="taskId")
    document_id: str = Field(alias="documentId")

    model_config = {"populate_by_name": True}
