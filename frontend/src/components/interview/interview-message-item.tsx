"use client";

import { cn } from "@/lib/cn";
import {
  InterviewSystemNotice,
  type InterviewSystemAction,
  type InterviewSystemTone,
} from "@/components/interview/interview-system-notice";

export type InterviewUserMessageStatus =
  | "sent"
  | "evaluating"
  | "followup"
  | "recorded"
  | "failed";

type InterviewAssistantMessage = {
  id: string;
  kind: "assistant";
  title?: string;
  body: string;
  tag?: string;
  isCurrent?: boolean;
};

type InterviewUserMessage = {
  id: string;
  kind: "user";
  body: string;
  timestamp: string;
  status: InterviewUserMessageStatus;
};

type InterviewSystemMessage = {
  id: string;
  kind: "system";
  body: string;
  tone?: InterviewSystemTone;
  displayStyle?: "card" | "plain";
  actionLabel?: string;
  actionKey?: InterviewSystemAction;
};

export type InterviewTimelineMessage =
  | InterviewAssistantMessage
  | InterviewUserMessage
  | InterviewSystemMessage;

const USER_STATUS_LABELS: Record<InterviewUserMessageStatus, string> = {
  sent: "已发送",
  evaluating: "分析中",
  followup: "生成追问中",
  recorded: "已记录",
  failed: "发送失败",
};

const USER_STATUS_CLASS_NAMES: Record<InterviewUserMessageStatus, string> = {
  sent: "text-[rgba(226,232,240,0.84)]",
  evaluating: "text-[rgba(191,219,254,0.92)]",
  followup: "text-[rgba(253,224,71,0.92)]",
  recorded: "text-[rgba(167,243,208,0.92)]",
  failed: "text-[rgba(254,202,202,0.92)]",
};

interface InterviewMessageItemProps {
  message: InterviewTimelineMessage;
  onAction?: (action: InterviewSystemAction) => void;
}

export function InterviewMessageItem({
  message,
  onAction,
}: InterviewMessageItemProps) {
  if (message.kind === "system") {
    return (
      <InterviewSystemNotice
        actionKey={message.actionKey}
        actionLabel={message.actionLabel}
        body={message.body}
        displayStyle={message.displayStyle}
        onAction={onAction}
        tone={message.tone}
      />
    );
  }

  if (message.kind === "assistant") {
    return (
      <div className="flex justify-start">
        <div className="flex w-full max-w-[min(50%,460px)] flex-col items-start gap-2">
          <div className="flex items-center gap-2 px-1 text-[12px] font-medium text-[var(--token-color-text-secondary)]">
            <div className="flex size-8 shrink-0 items-center justify-center rounded-full bg-[rgba(15,23,42,0.08)] text-[11px] font-semibold text-[var(--token-color-text-secondary)]">
              HR
            </div>
            <span>面试官 HR</span>
          </div>
          <article
            className={cn(
              "w-full rounded-[22px] rounded-bl-md border px-4 py-3 shadow-[0_10px_28px_rgba(15,23,42,0.08)] transition-colors duration-200",
              message.isCurrent
                ? "border-[rgba(0,102,255,0.16)] bg-[rgba(248,250,252,0.98)]"
                : "border-[rgba(15,23,42,0.08)] bg-white",
            )}
          >
            <p className="whitespace-pre-wrap text-[15px] leading-7 text-[var(--token-color-text-primary)]">
              {message.body}
            </p>
          </article>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-end gap-2">
      <div className="self-center text-[12px] text-[var(--token-color-text-tertiary)]">
        {message.timestamp}
      </div>
      <article className="w-full max-w-[min(50%,460px)] rounded-[22px] rounded-br-md bg-[linear-gradient(180deg,#2f7df6,#2369d4)] px-4 py-3 text-white shadow-[0_14px_32px_rgba(35,105,212,0.22)]">
        <p className="whitespace-pre-wrap text-[15px] leading-7">{message.body}</p>
        <div className="mt-2 flex flex-wrap items-center justify-end gap-3 text-[12px]">
          <span
            className={cn(
              "font-medium",
              USER_STATUS_CLASS_NAMES[message.status],
            )}
          >
            {USER_STATUS_LABELS[message.status]}
          </span>
        </div>
      </article>
    </div>
  );
}
