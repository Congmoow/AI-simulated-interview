from app.providers.base import ModelProvider
from app.schemas.document import ProcessDocumentRequest, ProcessDocumentResponse


class DocumentService:
    def __init__(self, provider: ModelProvider) -> None:
        self.provider = provider

    def process(self, request: ProcessDocumentRequest) -> ProcessDocumentResponse:
        return self.provider.process_document(request)
