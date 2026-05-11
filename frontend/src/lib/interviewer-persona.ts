/**
 * Shared interviewer persona used across the interview UI.
 * Keep this single source of truth so the assistant identity stays consistent.
 */
export const INTERVIEWER_PERSONA = {
  name: "陈航",
  role: "面试官",
  initials: "陈",
} as const;

const MESSAGE_TYPE_TAGS: Record<string, string> = {
  opening: "开场",
  question: "新问题",
  follow_up: "追问",
  technical_follow_up: "技术追问",
  closing: "收尾",
};

export function getInterviewerMessageTag(messageType?: string): string | undefined {
  if (!messageType) {
    return undefined;
  }
  return MESSAGE_TYPE_TAGS[messageType];
}

export function getInterviewerMessageVariantClass(messageType?: string): string {
  switch (messageType) {
    case "opening":
      return "interview-bubble--opening";
    case "follow_up":
    case "technical_follow_up":
      return "interview-bubble--follow-up";
    case "closing":
      return "interview-bubble--closing";
    case "question":
      return "interview-bubble--question";
    default:
      return "interview-bubble--default";
  }
}
