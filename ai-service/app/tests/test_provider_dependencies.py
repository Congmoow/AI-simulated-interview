import pytest

from app.services import backend_ai_settings, dependencies
from app.services.backend_ai_settings import RuntimeAiSettings


def _build_settings() -> RuntimeAiSettings:
    return RuntimeAiSettings(
        provider="qwen",
        baseUrl="https://example.com/v1",
        model="qwen-plus",
        apiKey="secret-key",
        temperature=0.3,
        maxTokens=512,
        systemPrompt="test",
    )


def test_get_provider_should_raise_when_runtime_settings_missing(monkeypatch):
    monkeypatch.setattr(dependencies, "fetch_runtime_ai_settings", lambda: None)

    with pytest.raises(RuntimeError, match="runtime ai settings"):
        dependencies.get_provider()


def test_get_provider_should_return_openai_provider_when_runtime_settings_present(monkeypatch):
    monkeypatch.setattr(dependencies, "fetch_runtime_ai_settings", _build_settings)

    provider = dependencies.get_provider()

    assert provider.__class__.__name__ == "OpenAICompatibleProvider"


def test_get_provider_should_propagate_runtime_settings_error(monkeypatch):
    def _raise() -> RuntimeError:
        raise RuntimeError("backend unavailable")

    monkeypatch.setattr(dependencies, "fetch_runtime_ai_settings", _raise)

    with pytest.raises(RuntimeError, match="backend unavailable"):
        dependencies.get_provider()


def test_fetch_runtime_ai_settings_should_use_cache_before_ttl_expires(monkeypatch):
    calls = {"count": 0}
    now = {"value": 100.0}

    def fake_time() -> float:
        return now["value"]

    def fake_fetch() -> RuntimeAiSettings:
        calls["count"] += 1
        return _build_settings()

    backend_ai_settings.clear_runtime_ai_settings_cache()
    monkeypatch.setattr(backend_ai_settings.time, "monotonic", fake_time)
    monkeypatch.setattr(backend_ai_settings, "_fetch_runtime_ai_settings_uncached", fake_fetch)

    first = backend_ai_settings.fetch_runtime_ai_settings()
    now["value"] += 10.0
    second = backend_ai_settings.fetch_runtime_ai_settings()

    assert first == second
    assert calls["count"] == 1


def test_fetch_runtime_ai_settings_should_refresh_after_ttl_expires(monkeypatch):
    calls = {"count": 0}
    now = {"value": 100.0}

    def fake_time() -> float:
        return now["value"]

    def fake_fetch() -> RuntimeAiSettings:
        calls["count"] += 1
        settings = _build_settings()
        settings.model = f"qwen-plus-{calls['count']}"
        return settings

    backend_ai_settings.clear_runtime_ai_settings_cache()
    monkeypatch.setattr(backend_ai_settings.time, "monotonic", fake_time)
    monkeypatch.setattr(backend_ai_settings, "_fetch_runtime_ai_settings_uncached", fake_fetch)

    first = backend_ai_settings.fetch_runtime_ai_settings()
    now["value"] += backend_ai_settings.RUNTIME_AI_SETTINGS_TTL_SECONDS + 1
    second = backend_ai_settings.fetch_runtime_ai_settings()

    assert first.model == "qwen-plus-1"
    assert second.model == "qwen-plus-2"
    assert calls["count"] == 2


def test_fetch_runtime_ai_settings_should_propagate_refresh_error(monkeypatch):
    backend_ai_settings.clear_runtime_ai_settings_cache()
    monkeypatch.setattr(
        backend_ai_settings,
        "_fetch_runtime_ai_settings_uncached",
        lambda: (_ for _ in ()).throw(RuntimeError("backend unavailable")),
    )

    with pytest.raises(RuntimeError, match="backend unavailable"):
        backend_ai_settings.fetch_runtime_ai_settings()
