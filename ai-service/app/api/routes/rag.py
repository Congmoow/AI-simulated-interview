from fastapi import APIRouter, Depends

from app.core.security import verify_internal_request
from app.schemas.rag import RagSearchRequest, RagSearchResponse
from app.services.dependencies import get_provider
from app.services.rag_service import RagService

router = APIRouter(dependencies=[Depends(verify_internal_request)])


@router.post("/search", response_model=RagSearchResponse)
async def search_rag(request: RagSearchRequest):
    service = RagService(get_provider())
    return service.search(request)
