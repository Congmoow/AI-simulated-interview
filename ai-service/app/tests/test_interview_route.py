from unittest.mock import patch

from fastapi.testclient import TestClient

from app.main import app


def test_start_interview_should_return_fallback_question_when_provider_creation_fails() -> None:
    payload = {
        "interviewId": "550e8400-e29b-41d4-a716-446655440020",
        "positionCode": "java-backend",
        "positionName": "Java 后端工程师",
        "interviewMode": "standard",
        "questionTypes": ["project", "technical"],
        "questionBank": [
            {
                "questionId": "550e8400-e29b-41d4-a716-446655440021",
                "title": "介绍订单系统项目",
                "type": "project",
                "content": "请介绍你做过的订单系统项目",
                "difficulty": "medium",
            }
        ],
        "askedQuestionIds": [],
        "currentMainQuestion": None,
        "recentMessages": [],
        "historyAnswerSummaries": [],
        "limits": {
            "maxMainQuestions": 5,
            "currentMainQuestionCount": 0,
            "maxMessages": 30,
            "currentMessageCount": 0,
            "maxDurationMinutes": 30,
            "currentDurationMinutes": 0,
        },
    }

    with patch("app.api.routes.interview.get_provider", side_effect=RuntimeError("provider unavailable")):
        client = TestClient(app)
        response = client.post("/interview/start", json=payload)

    assert response.status_code == 200
    data = response.json()
    assert data["action"] == "question"
    assert data["messageType"] == "opening"
    assert data["selectedQuestionId"] == payload["questionBank"][0]["questionId"]
    assert data["content"]
    assert isinstance(data["suggestions"], list)
