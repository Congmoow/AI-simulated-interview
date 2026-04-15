import logging

from app.providers.base import ModelProvider
from app.providers.mock_provider import MockProvider
from app.services.backend_ai_settings import fetch_runtime_ai_settings

logger = logging.getLogger(__name__)


def get_provider() -> ModelProvider:
    try:
        runtime_settings = fetch_runtime_ai_settings()
        if runtime_settings is not None:
            from app.providers.openai_compatible_provider import OpenAICompatibleProvider

            logger.info(
                "ai-service 使用真实 provider，provider=%s，base_url=%s，model=%s",
                runtime_settings.provider,
                runtime_settings.base_url,
                runtime_settings.model,
            )
            return OpenAICompatibleProvider(runtime_settings)
    except Exception:
        logger.exception("读取 backend 运行时 AI 配置失败，回退到 mock provider")
        return MockProvider()

    logger.info("未读取到启用的真实 provider 配置，回退到 mock provider")
    return MockProvider()
