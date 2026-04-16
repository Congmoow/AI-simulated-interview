import type { InterviewTimelineMessage } from "@/components/interview/interview-message-item";
import type { InterviewCurrentDetail } from "@/types/api";

const ASSISTANT_TAG_LABELS: Record<string, string> = {
  opening: "开场主问题",
  question: "主问题",
  follow_up: "追问",
  closing: "结束语",
  hint: "提示",
  system: "系统消息",
};

function formatShortTime(value: string) {
  return new Date(value).toLocaleTimeString("zh-CN", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

function buildAssistantTitle(messageType: string, mainQuestionIndex: number) {
  if (messageType === "follow_up") {
    return mainQuestionIndex > 0 ? `第 ${mainQuestionIndex} 题 · 追问` : "追问";
  }

  if (messageType === "closing") {
    return "结束语";
  }

  if (messageType === "hint") {
    return "提示";
  }

  return `第 ${mainQuestionIndex} 题`;
}

export function buildInterviewTimelineMessages(
  detail: InterviewCurrentDetail,
): InterviewTimelineMessage[] {
  const sortedMessages = [...detail.messages].sort(
    (left, right) => left.sequence - right.sequence,
  );

  let mainQuestionIndex = 0;
  const timelineMessages = sortedMessages.map<InterviewTimelineMessage>((message) => {
    if (message.role === "user") {
      return {
        id: message.id,
        kind: "user",
        body: message.content,
        timestamp: formatShortTime(message.createdAt),
        status: "recorded",
      };
    }

    if (message.role === "system") {
      return {
        id: message.id,
        kind: "system",
        body: message.content,
      };
    }

    if (message.messageType === "opening" || message.messageType === "question") {
      mainQuestionIndex += 1;
    }

    return {
      id: message.id,
      kind: "assistant",
      title: buildAssistantTitle(message.messageType, mainQuestionIndex),
      body: message.content,
      tag: ASSISTANT_TAG_LABELS[message.messageType] ?? "面试官",
      isCurrent: false,
    };
  });

  const currentAssistantId =
    detail.status === "in_progress"
      ? [...timelineMessages]
          .reverse()
          .find((message) => message.kind === "assistant")?.id ?? null
      : null;

  return timelineMessages.map((message) => {
    if (message.kind !== "assistant") {
      return message;
    }

    return {
      ...message,
      isCurrent: message.id === currentAssistantId,
    };
  });
}

export function hasPersistedPendingAnswer(
  detail: InterviewCurrentDetail,
  pendingText: string,
) {
  const normalizedText = pendingText.trim();
  if (!normalizedText) {
    return false;
  }

  return detail.messages
    .filter((message) => message.role === "user")
    .some((message) => message.content.trim() === normalizedText);
}
