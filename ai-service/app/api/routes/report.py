from fastapi import APIRouter, Depends

from app.core.security import verify_internal_request
from app.schemas.report import GenerateReportRequest, GenerateReportResponse
from app.services.dependencies import get_provider
from app.services.report_service import ReportService

router = APIRouter(dependencies=[Depends(verify_internal_request)])


@router.post("/generate", response_model=GenerateReportResponse)
async def generate_report(request: GenerateReportRequest):
    service = ReportService(get_provider())
    return service.generate(request)
