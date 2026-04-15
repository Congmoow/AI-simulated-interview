import logging

from app.providers.base import ModelProvider
from app.services.backend_ai_settings import fetch_runtime_ai_settings

logger = logging.getLogger(__name__)


def get_provider() -> ModelProvider:
    logger.info("开始读取 runtime settings")
    try:
        runtime_settings = fetch_runtime_ai_settings()
    except Exception:
        logger.exception("读取 runtime settings 失败，无法创建真实 provider")
        raise

    if runtime_settings is None:
        logger.error("runtime settings 为空，拒绝回退 mock provider")
        raise RuntimeError("runtime ai settings is empty")

    from app.providers.openai_compatible_provider import OpenAICompatibleProvider

    logger.info(
        "准备创建真实 provider：runtime_settings_loaded=%s provider=%s base_url=%s model=%s",
        True,
        runtime_settings.provider,
        runtime_settings.base_url,
        runtime_settings.model,
    )
    return OpenAICompatibleProvider(runtime_settings)
