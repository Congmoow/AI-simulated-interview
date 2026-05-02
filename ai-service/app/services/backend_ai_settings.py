from __future__ import annotations

import logging
import threading
import time

import httpx
from pydantic import BaseModel, Field

from app.core.settings import get_settings

logger = logging.getLogger(__name__)

RUNTIME_AI_SETTINGS_TTL_SECONDS = 60.0


class RuntimeAiSettings(BaseModel):
    provider: str
    base_url: str = Field(alias="baseUrl")
    model: str
    api_key: str = Field(alias="apiKey")
    temperature: float = 0.7
    max_tokens: int = Field(alias="maxTokens", default=2048)
    system_prompt: str = Field(alias="systemPrompt", default="")


_cache_lock = threading.Lock()
_runtime_ai_settings_cache: tuple[float, RuntimeAiSettings | None] | None = None
_backend_client = httpx.Client(timeout=10.0, http2=False)


def clear_runtime_ai_settings_cache() -> None:
    global _runtime_ai_settings_cache
    with _cache_lock:
        _runtime_ai_settings_cache = None


def _summarize_text(text: str, limit: int = 240) -> str:
    compact = " ".join(text.split())
    return compact[:limit]


def _fetch_runtime_ai_settings_uncached() -> RuntimeAiSettings | None:
    settings = get_settings()
    headers: dict[str, str] = {}
    if settings.api_key:
        headers["Authorization"] = f"Bearer {settings.api_key}"

    url = f"{settings.backend_url.rstrip('/')}/api/v1/internal/ai/runtime-settings"
    try:
        response = _backend_client.get(url, headers=headers)
        response.raise_for_status()
        payload = response.json()
    except httpx.HTTPStatusError as exc:
        response_body = _summarize_text(exc.response.text) if exc.response is not None else ""
        logger.exception(
            "cache_refresh_failed upstream_status_code=%s exception_type=%s response_body_snippet=%s",
            exc.response.status_code if exc.response is not None else "n/a",
            exc.__class__.__name__,
            response_body,
        )
        raise
    except httpx.RequestError as exc:
        logger.exception(
            "cache_refresh_failed backend_url=%s exception_type=%s",
            settings.backend_url,
            exc.__class__.__name__,
        )
        raise
    except Exception as exc:
        logger.exception(
            "cache_refresh_failed backend_url=%s exception_type=%s",
            settings.backend_url,
            exc.__class__.__name__,
        )
        raise

    data = payload.get("data")
    if not data:
        logger.warning("runtime_settings_empty backend_url=%s", settings.backend_url)
        return None

    runtime_settings = RuntimeAiSettings.model_validate(data)
    logger.info(
        "runtime_settings_refreshed provider=%s base_url=%s model=%s",
        runtime_settings.provider,
        runtime_settings.base_url,
        runtime_settings.model,
    )
    return runtime_settings


def fetch_runtime_ai_settings(force_refresh: bool = False) -> RuntimeAiSettings | None:
    global _runtime_ai_settings_cache

    now = time.monotonic()
    with _cache_lock:
        if not force_refresh and _runtime_ai_settings_cache is not None:
            expires_at, cached_value = _runtime_ai_settings_cache
            if now < expires_at:
                logger.info("cache_hit ttl_seconds=%s", int(expires_at - now))
                return cached_value
            logger.info("cache_expired age_seconds=%s", int(now - (expires_at - RUNTIME_AI_SETTINGS_TTL_SECONDS)))
        else:
            logger.info("cache_miss")

    runtime_settings = _fetch_runtime_ai_settings_uncached()
    with _cache_lock:
        _runtime_ai_settings_cache = (now + RUNTIME_AI_SETTINGS_TTL_SECONDS, runtime_settings)
    return runtime_settings
