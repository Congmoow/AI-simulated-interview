from __future__ import annotations

from pydantic import BaseModel, Field
import httpx

from app.core.settings import get_settings


class RuntimeAiSettings(BaseModel):
    provider: str
    base_url: str = Field(alias="baseUrl")
    model: str
    api_key: str = Field(alias="apiKey")
    temperature: float = 0.7
    max_tokens: int = Field(alias="maxTokens", default=2048)
    system_prompt: str = Field(alias="systemPrompt", default="")


def fetch_runtime_ai_settings() -> RuntimeAiSettings | None:
    settings = get_settings()
    headers: dict[str, str] = {}
    if settings.api_key:
        headers["Authorization"] = f"Bearer {settings.api_key}"

    with httpx.Client(timeout=10.0, http2=False) as client:
        response = client.get(
            f"{settings.backend_url.rstrip('/')}/api/v1/internal/ai/runtime-settings",
            headers=headers,
        )
        response.raise_for_status()
        payload = response.json()

    data = payload.get("data")
    if not data:
        return None

    return RuntimeAiSettings.model_validate(data)
