from unittest.mock import MagicMock, patch

import pytest
from fastapi.testclient import TestClient

from app.core.settings import get_settings
from app.main import app
from app.providers.mock_provider import MockProvider
from app.workers.tasks import _send_callback_with_retry

INTERNAL_API_KEY = "test-internal-api-key-1234567890"


@pytest.fixture(autouse=True)
def configure_internal_auth(monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setenv("AI_SERVICE_API_KEY", INTERNAL_API_KEY)
    monkeypatch.delenv("AI_SERVICE_ALLOW_INSECURE_DEV_AUTH_BYPASS", raising=False)
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


def test_process_document_returns_chunks() -> None:
    with patch("app.api.routes.document.get_provider", return_value=MockProvider()):
        client = TestClient(app)
        payload = {
            "documentId": "550e8400-e29b-41d4-a716-446655440000",
            "fileName": "test.pdf",
            "fileType": "pdf",
            "title": "Java 并发知识",
        }
        response = client.post("/document/process", json=payload, headers=_internal_headers())

    assert response.status_code == 200
    data = response.json()
    assert "documentId" in data
    assert "chunks" in data
    assert len(data["chunks"]) > 0


def test_process_document_chunk_structure() -> None:
    with patch("app.api.routes.document.get_provider", return_value=MockProvider()):
        client = TestClient(app)
        payload = {
            "documentId": "550e8400-e29b-41d4-a716-446655440001",
            "fileName": "sample.txt",
            "fileType": "txt",
            "title": "测试文档标题",
        }
        response = client.post("/document/process", json=payload, headers=_internal_headers())

    assert response.status_code == 200
    chunks = response.json()["chunks"]
    for chunk in chunks:
        assert "chunkIndex" in chunk
        assert "content" in chunk
        assert "tokenCount" in chunk
        assert "metadata" in chunk
        assert isinstance(chunk["chunkIndex"], int)
        assert isinstance(chunk["content"], str)
        assert len(chunk["content"]) > 0
        assert chunk["tokenCount"] > 0


def test_send_callback_with_retry_succeeds_on_first_try() -> None:
    calls: list = []

    def fake_post(url, json, headers):
        calls.append((url, json))
        mock_resp = MagicMock()
        mock_resp.raise_for_status.return_value = None
        return mock_resp

    with patch("app.workers.tasks.httpx.Client") as mock_client_type:
        mock_client = MagicMock()
        mock_client.__enter__ = MagicMock(return_value=mock_client)
        mock_client.__exit__ = MagicMock(return_value=False)
        mock_client.post.side_effect = fake_post
        mock_client_type.return_value = mock_client

        _send_callback_with_retry(
            "http://backend/callback",
            {"status": "ready", "chunks": []},
            {},
        )

    assert len(calls) == 1


def test_send_callback_with_retry_raises_on_final_failure() -> None:
    with patch("app.workers.tasks.httpx.Client") as mock_client_type:
        mock_client = MagicMock()
        mock_client.__enter__ = MagicMock(return_value=mock_client)
        mock_client.__exit__ = MagicMock(return_value=False)
        mock_client.post.side_effect = ConnectionError("backend unavailable")
        mock_client_type.return_value = mock_client

        with patch("app.workers.tasks.time.sleep"):
            with pytest.raises(ConnectionError):
                _send_callback_with_retry(
                    "http://backend/callback",
                    {"status": "failed", "chunks": [], "error": "err"},
                    {},
                    max_retries=2,
                )


def test_enqueue_document_returns_task_id() -> None:
    mock_task = MagicMock()
    mock_task.id = "mock-task-id-abc123"

    with patch(
        "app.api.routes.document.process_knowledge_document_task.delay",
        return_value=mock_task,
    ):
        client = TestClient(app)
        payload = {
            "documentId": "550e8400-e29b-41d4-a716-446655440002",
            "fileName": "enqueue-test.pdf",
            "fileType": "pdf",
            "title": "入队测试文档",
        }
        response = client.post("/document/enqueue", json=payload, headers=_internal_headers())

    assert response.status_code == 200
    data = response.json()
    assert data["taskId"] == "mock-task-id-abc123"
    assert data["documentId"] == "550e8400-e29b-41d4-a716-446655440002"


def _internal_headers() -> dict[str, str]:
    return {"Authorization": f"Bearer {INTERNAL_API_KEY}"}
