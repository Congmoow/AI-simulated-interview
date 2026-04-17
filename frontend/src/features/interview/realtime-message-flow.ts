import type {
  InterviewTimelineMessage,
  InterviewUserMessageStatus,
} from "@/components/interview/interview-message-item";

export type RealtimePendingAnswer = {
  id: string;
  text: string;
  timestamp: string;
  status: InterviewUserMessageStatus;
};

function formatShortTime(value: string) {
  return new Date(value).toLocaleTimeString("zh-CN", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function buildRealtimeInterviewMessages({
  pendingAnswer,
  pendingAnswerAlreadyPersisted,
  assistantThinking,
}: {
  pendingAnswer: RealtimePendingAnswer | null;
  pendingAnswerAlreadyPersisted: boolean;
  assistantThinking: boolean;
}): InterviewTimelineMessage[] {
  const messages: InterviewTimelineMessage[] = [];

  if (pendingAnswer && !pendingAnswerAlreadyPersisted) {
    messages.push({
      id: pendingAnswer.id,
      kind: "user",
      body: pendingAnswer.text,
      timestamp: formatShortTime(pendingAnswer.timestamp),
      status: pendingAnswer.status,
    });
  }

  if (assistantThinking) {
    messages.push({
      id: "assistant-thinking",
      kind: "assistant",
      body: "分析中",
      isThinking: true,
    });
  }

  return messages;
}
