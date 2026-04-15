import pytest

from app.services import dependencies
from app.services.backend_ai_settings import RuntimeAiSettings


def test_get_provider_should_raise_when_runtime_settings_missing(monkeypatch):
    monkeypatch.setattr(dependencies, "fetch_runtime_ai_settings", lambda: None)

    with pytest.raises(RuntimeError, match="runtime ai settings"):
        dependencies.get_provider()


def test_get_provider_should_return_openai_provider_when_runtime_settings_present(monkeypatch):
    monkeypatch.setattr(
        dependencies,
        "fetch_runtime_ai_settings",
        lambda: RuntimeAiSettings(
            provider="qwen",
            baseUrl="https://example.com/v1",
            model="qwen-plus",
            apiKey="secret-key",
            temperature=0.3,
            maxTokens=512,
            systemPrompt="test",
        ),
    )

    provider = dependencies.get_provider()

    assert provider.__class__.__name__ == "OpenAICompatibleProvider"


def test_get_provider_should_propagate_runtime_settings_error(monkeypatch):
    def _raise() -> RuntimeError:
        raise RuntimeError("backend unavailable")

    monkeypatch.setattr(dependencies, "fetch_runtime_ai_settings", _raise)

    with pytest.raises(RuntimeError, match="backend unavailable"):
        dependencies.get_provider()
