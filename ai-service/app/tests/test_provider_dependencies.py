from app.providers.mock_provider import MockProvider
from app.services import dependencies
from app.services.backend_ai_settings import RuntimeAiSettings


def test_get_provider_should_return_mock_when_runtime_settings_missing(monkeypatch):
    monkeypatch.setattr(dependencies, "fetch_runtime_ai_settings", lambda: None)

    provider = dependencies.get_provider()

    assert isinstance(provider, MockProvider)


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
