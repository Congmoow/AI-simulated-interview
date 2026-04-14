from typing import Any

from pydantic import BaseModel, Field


class ProcessDocumentRequest(BaseModel):
    document_id: str = Field(alias="documentId")
    file_name: str = Field(alias="fileName")
    file_type: str = Field(alias="fileType")
    title: str

    model_config = {"populate_by_name": True}


class ChunkResult(BaseModel):
    chunk_index: int = Field(alias="chunkIndex")
    content: str
    token_count: int = Field(alias="tokenCount")
    metadata: dict[str, Any] = Field(default_factory=dict)

    model_config = {"populate_by_name": True}


class ProcessDocumentResponse(BaseModel):
    document_id: str = Field(alias="documentId")
    chunks: list[ChunkResult] = Field(default_factory=list)

    model_config = {"populate_by_name": True}


class EnqueueDocumentRequest(BaseModel):
    document_id: str = Field(alias="documentId")
    file_name: str = Field(alias="fileName")
    file_type: str = Field(alias="fileType")
    title: str

    model_config = {"populate_by_name": True}


class EnqueueDocumentResponse(BaseModel):
    task_id: str = Field(alias="taskId")
    document_id: str = Field(alias="documentId")

    model_config = {"populate_by_name": True}
