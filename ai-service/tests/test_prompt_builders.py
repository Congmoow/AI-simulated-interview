from uuid import uuid4

from app.providers.openai_compatible_provider import (
    INTERVIEWER_PERSONA_BASE,
    MODE_DIRECTIVES,
    OUTPUT_RULES,
    OpenAICompatibleProvider,
    _mode_label,
    _select_mode_directive,
    _select_position_focus,
    build_interview_system_prompt,
)
from app.schemas.interview import (
    AnswerInterviewRequest,
    CandidateQuestion,
    CurrentMainQuestion,
    InterviewLimits,
    InterviewMessage,
)
from app.services.backend_ai_settings import RuntimeAiSettings


def _build_provider() -> OpenAICompatibleProvider:
    return OpenAICompatibleProvider(
        RuntimeAiSettings(
            provider="qwen",
            baseUrl="https://example.com/v1",
            model="qwen-plus",
            apiKey="secret-key",
            temperature=0.3,
            maxTokens=512,
            systemPrompt="test",
        )
    )


def _build_candidate_question(title: str = "介绍订单系统项目") -> CandidateQuestion:
    return CandidateQuestion(
        questionId=uuid4(),
        title=title,
        type="project",
        content=f"{title} 的具体背景与职责",
        difficulty="medium",
    )


def _build_answer_request(mode: str = "standard") -> AnswerInterviewRequest:
    current = _build_candidate_question()
    return AnswerInterviewRequest(
        interviewId=uuid4(),
        positionCode="java-backend",
        positionName="Java 后端工程师",
        interviewMode=mode,
        questionBank=[current, _build_candidate_question("事务传播行为")],
        askedQuestionIds=[current.question_id],
        currentMainQuestion=CurrentMainQuestion(
            roundNumber=1,
            questionId=current.question_id,
            title=current.title,
            type=current.type,
            askedContent="请结合真实项目讲一下你做过的订单系统",
            followUpCount=1,
        ),
        recentMessages=[
            InterviewMessage(
                role="assistant",
                messageType="opening",
                content="请结合真实项目讲一下你做过的订单系统",
                relatedQuestionId=current.question_id,
                sequence=1,
            ),
            InterviewMessage(
                role="user",
                messageType="answer",
                content="我负责了订单和库存一致性，用了 Redis 做缓存。",
                relatedQuestionId=current.question_id,
                sequence=2,
            ),
        ],
        historyAnswerSummaries=[
            "第1题：介绍订单系统项目；回答：我负责了订单和库存一致性，用了 Redis 做缓存。",
        ],
        limits=InterviewLimits(
            maxMainQuestions=5,
            currentMainQuestionCount=1,
            maxMessages=30,
            currentMessageCount=2,
            maxDurationMinutes=30,
            currentDurationMinutes=8,
        ),
    )


def test_build_interview_system_prompt_includes_persona_and_output_rules() -> None:
    prompt = build_interview_system_prompt("standard", "java-backend")

    assert INTERVIEWER_PERSONA_BASE in prompt
    assert OUTPUT_RULES in prompt
    assert "陈航" in prompt
    assert "JSON 键顺序" in prompt


def test_build_interview_system_prompt_switches_by_mode() -> None:
    friendly = build_interview_system_prompt("friendly", "java-backend")
    standard = build_interview_system_prompt("standard", "java-backend")
    stress = build_interview_system_prompt("stress", "java-backend")

    assert MODE_DIRECTIVES["friendly"] in friendly
    assert MODE_DIRECTIVES["standard"] in standard
    assert MODE_DIRECTIVES["stress"] in stress
    assert MODE_DIRECTIVES["friendly"] not in standard
    assert MODE_DIRECTIVES["stress"] not in friendly


def test_build_interview_system_prompt_injects_position_focus_by_keyword() -> None:
    java_prompt = build_interview_system_prompt("standard", "java-backend")
    react_prompt = build_interview_system_prompt("standard", "web-react")
    unknown_prompt = build_interview_system_prompt("standard", "mystery-role")

    assert "JVM" in java_prompt
    assert "Fiber" in react_prompt
    assert "基于候选人回答中出现的技术栈" in unknown_prompt


def test_select_mode_directive_falls_back_to_standard_for_unknown_mode() -> None:
    assert _select_mode_directive("unknown") == MODE_DIRECTIVES["standard"]
    assert _select_mode_directive("") == MODE_DIRECTIVES["standard"]
    assert _select_mode_directive("FRIENDLY") == MODE_DIRECTIVES["friendly"]


def test_mode_label_falls_back_to_standard_label() -> None:
    assert _mode_label("friendly") == "轻松"
    assert _mode_label("standard") == "标准"
    assert _mode_label("stress") == "高压"
    assert _mode_label("unknown") == "标准"


def test_select_position_focus_picks_first_matching_keyword() -> None:
    assert "JVM" in _select_position_focus("java-backend")
    assert "React" in _select_position_focus("web-react")
    assert "浏览器渲染" in _select_position_focus("web-frontend")


def test_compact_answer_prompt_injects_mode_recent_and_history() -> None:
    provider = _build_provider()
    request = _build_answer_request(mode="stress")

    prompt = provider._build_compact_answer_prompt(request)

    assert "面试模式：高压" in prompt
    assert "本场最近对话" in prompt
    assert "我负责了订单和库存一致性" in prompt
    assert "历史已答摘要" in prompt
    assert "第1题：介绍订单系统项目" in prompt
    assert "当前主问题原话：请结合真实项目讲一下你做过的订单系统" in prompt
    assert "当前主问题已追问次数：1" in prompt


def test_compact_answer_prompt_uses_standard_label_when_mode_unknown() -> None:
    provider = _build_provider()
    request = _build_answer_request(mode="unknown")

    prompt = provider._build_compact_answer_prompt(request)

    assert "面试模式：标准" in prompt
