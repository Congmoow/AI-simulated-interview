from app.providers.base import ModelProvider
from app.schemas.rag import RagSearchRequest, RagSearchResponse


class RagService:
    def __init__(self, provider: ModelProvider) -> None:
        self.provider = provider

    def search(self, request: RagSearchRequest) -> RagSearchResponse:
        return self.provider.search_rag(request)
