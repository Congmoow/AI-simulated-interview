import type { InterviewTimelineMessage } from "@/components/interview/interview-message-item";
import type { InterviewCurrentDetail } from "@/types/api";

function formatShortTime(value: string) {
  return new Date(value).toLocaleTimeString("zh-CN", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function buildInterviewTimelineMessages(
  detail: InterviewCurrentDetail,
): InterviewTimelineMessage[] {
  const sortedMessages = [...detail.messages].sort(
    (left, right) => left.sequence - right.sequence,
  );

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

    return {
      id: message.id,
      kind: "assistant",
      body: message.content,
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
