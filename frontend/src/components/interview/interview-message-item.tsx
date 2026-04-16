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
  title: string;
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
  followup: "正在生成追问",
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
        onAction={onAction}
        tone={message.tone}
      />
    );
  }

  if (message.kind === "assistant") {
    return (
      <div className="flex justify-start">
        <article
          className={cn(
            "w-full max-w-[760px] rounded-[28px] border bg-[rgba(255,255,255,0.96)] px-5 py-4 shadow-[0_16px_34px_rgba(15,23,42,0.06)] transition-colors duration-200",
            message.isCurrent
              ? "border-[rgba(0,102,255,0.18)] bg-[linear-gradient(180deg,rgba(255,255,255,0.98),rgba(239,246,255,0.92))]"
              : "border-[rgba(15,23,42,0.08)]",
          )}
        >
          <div className="mb-3 flex flex-wrap items-center gap-2">
            {message.tag ? (
              <span className="section-label !tracking-[0.14em]">{message.tag}</span>
            ) : null}
            {message.isCurrent ? (
              <span className="rounded-full bg-[rgba(0,102,255,0.08)] px-2.5 py-1 text-[11px] font-semibold text-[var(--token-color-primary)]">
                当前题目
              </span>
            ) : null}
          </div>
          <h3 className="text-[15px] font-semibold text-[var(--token-color-text-primary)]">
            {message.title}
          </h3>
          <p className="mt-3 whitespace-pre-wrap text-[15px] leading-7 text-[var(--token-color-text-primary)]">
            {message.body}
          </p>
        </article>
      </div>
    );
  }

  return (
    <div className="flex justify-end">
      <article className="w-full max-w-[720px] rounded-[28px] bg-[linear-gradient(180deg,rgba(15,23,42,0.94),rgba(30,41,59,0.96))] px-5 py-4 text-white shadow-[0_18px_38px_rgba(15,23,42,0.16)]">
        <p className="whitespace-pre-wrap text-[15px] leading-7">{message.body}</p>
        <div className="mt-4 flex flex-wrap items-center justify-end gap-3 text-[12px]">
          <span className="text-[rgba(226,232,240,0.72)]">{message.timestamp}</span>
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
