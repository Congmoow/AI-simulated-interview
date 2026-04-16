from unittest.mock import patch

from fastapi.testclient import TestClient

from app.main import app


def test_start_interview_should_return_fallback_question_when_provider_creation_fails() -> None:
    payload = {
        "interviewId": "550e8400-e29b-41d4-a716-446655440020",
        "positionCode": "java-backend",
        "interviewMode": "standard",
        "roundNumber": 1,
        "questionTypes": ["project"],
        "sourceQuestion": {
            "questionId": "550e8400-e29b-41d4-a716-446655440021",
            "title": "Introduce your project",
            "type": "project",
            "content": "Describe the project, your role and result",
            "difficulty": "medium",
        },
    }

    with patch("app.api.routes.interview.get_provider", side_effect=RuntimeError("provider unavailable")):
        client = TestClient(app)
        response = client.post("/interview/start", json=payload)

    assert response.status_code == 200
    data = response.json()
    assert data["questionId"] == payload["sourceQuestion"]["questionId"]
    assert data["title"]
    assert data["content"]
    assert data["type"] == "project"
    assert isinstance(data["suggestions"], list)
