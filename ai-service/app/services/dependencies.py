from functools import lru_cache

from app.core.settings import get_settings
from app.providers.mock_provider import MockProvider


@lru_cache
def get_provider() -> MockProvider:
    settings = get_settings()
    if settings.model_provider != "mock":
        return MockProvider()
    return MockProvider()
