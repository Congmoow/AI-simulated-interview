from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient

from app.core.settings import get_settings
from app.main import app
from app.providers.mock_provider import MockProvider


@pytest.fixture(autouse=True)
def clear_internal_auth_env(monkeypatch: pytest.MonkeyPatch):
    for key in (
        "AI_SERVICE_APP_ENV",
        "AI_SERVICE_API_KEY",
    ):
        monkeypatch.delenv(key, raising=False)

    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


def test_document_process_rejects_request_when_api_key_missing(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("AI_SERVICE_APP_ENV", "development")

    with patch("app.api.routes.document.get_provider", return_value=MockProvider()):
        client = TestClient(app)
        response = client.post("/document/process", json=_build_payload())

    assert response.status_code == 401


def test_document_process_allows_request_with_matching_bearer_token(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("AI_SERVICE_APP_ENV", "production")
    monkeypatch.setenv("AI_SERVICE_API_KEY", "shared-internal-api-key-1234567890")

    with patch("app.api.routes.document.get_provider", return_value=MockProvider()):
        client = TestClient(app)
        response = client.post(
            "/document/process",
            json=_build_payload(),
            headers={"Authorization": "Bearer shared-internal-api-key-1234567890"},
        )

    assert response.status_code == 200


def test_document_process_rejects_request_when_api_key_empty(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("AI_SERVICE_APP_ENV", "development")
    monkeypatch.setenv("AI_SERVICE_API_KEY", "")

    with patch("app.api.routes.document.get_provider", return_value=MockProvider()):
        client = TestClient(app)
        response = client.post("/document/process", json=_build_payload())

    assert response.status_code == 401


def _build_payload() -> dict[str, str]:
    return {
        "documentId": "550e8400-e29b-41d4-a716-446655440000",
        "fileName": "test.pdf",
        "fileType": "pdf",
        "title": "内部鉴权测试文档",
    }
