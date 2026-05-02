from app.providers.base import ModelProvider
from app.schemas.report import GenerateReportRequest, GenerateReportResponse


class ReportService:
    def __init__(self, provider: ModelProvider) -> None:
        self.provider = provider

    async def generate(self, request: GenerateReportRequest) -> GenerateReportResponse:
        return await self.provider.generate_report(request)
