import logging
import threading

from app.providers.base import ModelProvider
from app.services.backend_ai_settings import fetch_runtime_ai_settings, RuntimeAiSettings

logger = logging.getLogger(__name__)

_provider_cache_lock = threading.Lock()
_cached_provider: ModelProvider | None = None
_cached_settings_key: tuple[str, str, str] | None = None


def _settings_key(s: RuntimeAiSettings) -> tuple[str, str, str]:
    return (s.provider, s.base_url, s.model)


def get_provider() -> ModelProvider:
    global _cached_provider, _cached_settings_key

    logger.info("开始读取 runtime settings")
    try:
        runtime_settings = fetch_runtime_ai_settings()
    except Exception:
        logger.exception("读取 runtime settings 失败，回退 mock provider")
        from app.providers.mock_provider import MockProvider
        return MockProvider()

    if runtime_settings is None:
        logger.warning("runtime settings 为空，回退 mock provider")
        from app.providers.mock_provider import MockProvider
        return MockProvider()

    key = _settings_key(runtime_settings)
    with _provider_cache_lock:
        if _cached_provider is not None and _cached_settings_key == key:
            logger.info("复用缓存 provider: %s", key)
            return _cached_provider

    from app.providers.openai_compatible_provider import OpenAICompatibleProvider

    logger.info(
        "准备创建真实 provider：provider=%s base_url=%s model=%s",
        runtime_settings.provider,
        runtime_settings.base_url,
        runtime_settings.model,
    )
    provider = OpenAICompatibleProvider(runtime_settings)
    with _provider_cache_lock:
        _cached_provider = provider
        _cached_settings_key = key
    return provider
