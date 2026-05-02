from app.providers.base import ModelProvider
from app.schemas.document import ProcessDocumentRequest, ProcessDocumentResponse


class DocumentService:
    def __init__(self, provider: ModelProvider) -> None:
        self.provider = provider

    async def process(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        return await self.provider.process_document(request)
