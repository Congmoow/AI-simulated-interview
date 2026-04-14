from fastapi import APIRouter, Depends

from app.core.security import verify_internal_request
from app.schemas.document import (
    EnqueueDocumentRequest,
    EnqueueDocumentResponse,
    ProcessDocumentRequest,
    ProcessDocumentResponse,
)
from app.services.dependencies import get_provider
from app.services.document_service import DocumentService
from app.workers.tasks import process_knowledge_document_task

router = APIRouter(dependencies=[Depends(verify_internal_request)])


@router.post("/process", response_model=ProcessDocumentResponse)
async def process_document(request: ProcessDocumentRequest) -> ProcessDocumentResponse:
    service = DocumentService(get_provider())
    return service.process(request)


@router.post("/enqueue", response_model=EnqueueDocumentResponse)
async def enqueue_document(request: EnqueueDocumentRequest) -> EnqueueDocumentResponse:
    task = process_knowledge_document_task.delay(
        request.document_id,
        request.file_name,
        request.file_type,
        request.title,
    )
    return EnqueueDocumentResponse(taskId=task.id, documentId=request.document_id)
