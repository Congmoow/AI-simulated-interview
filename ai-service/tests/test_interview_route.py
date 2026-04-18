from unittest.mock import patch

from fastapi.testclient import TestClient

from app.main import app
from app.providers.mock_provider import MockProvider


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


def test_answer_interview_should_ask_user_to_return_to_current_question_when_input_is_greeting() -> None:
    payload = {
        "interviewId": "550e8400-e29b-41d4-a716-446655440120",
        "positionCode": "java-backend",
        "positionName": "Java 后端工程师",
        "interviewMode": "standard",
        "questionBank": [
            {
                "questionId": "550e8400-e29b-41d4-a716-446655440121",
                "title": "介绍订单系统项目",
                "type": "project",
                "content": "请介绍你做过的订单系统项目",
                "difficulty": "medium",
            },
            {
                "questionId": "550e8400-e29b-41d4-a716-446655440122",
                "title": "事务传播行为",
                "type": "technical",
                "content": "你通常如何选择 Spring 事务传播行为？",
                "difficulty": "medium",
            },
        ],
        "askedQuestionIds": ["550e8400-e29b-41d4-a716-446655440121"],
        "currentMainQuestion": {
            "roundNumber": 1,
            "questionId": "550e8400-e29b-41d4-a716-446655440121",
            "title": "介绍订单系统项目",
            "type": "project",
            "askedContent": "请结合你的真实项目，说明系统规模、瓶颈点和你做过的优化。",
            "followUpCount": 0,
        },
        "recentMessages": [
            {
                "role": "assistant",
                "messageType": "opening",
                "content": "请结合你的真实项目，说明系统规模、瓶颈点和你做过的优化。",
                "relatedQuestionId": "550e8400-e29b-41d4-a716-446655440121",
                "sequence": 1,
            },
            {
                "role": "user",
                "messageType": "answer",
                "content": "你好",
                "relatedQuestionId": "550e8400-e29b-41d4-a716-446655440121",
                "sequence": 2,
            },
        ],
        "historyAnswerSummaries": [],
        "limits": {
            "maxMainQuestions": 5,
            "currentMainQuestionCount": 1,
            "maxMessages": 30,
            "currentMessageCount": 2,
            "maxDurationMinutes": 30,
            "currentDurationMinutes": 2,
        },
    }

    with patch("app.api.routes.interview.get_provider", return_value=MockProvider()):
        client = TestClient(app)
        response = client.post("/interview/answer", json=payload)

    assert response.status_code == 200
    data = response.json()
    assert data["action"] == "follow_up"
    assert "当前问题" in data["content"]
    assert "真实项目" in data["content"]
