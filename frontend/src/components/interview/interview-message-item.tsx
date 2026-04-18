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
  isThinking?: boolean;
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
  displayStyle?: "card" | "plain" | "inline";
  actionLabel?: string;
  actionKey?: InterviewSystemAction;
};

export type InterviewTimelineMessage =
  | InterviewAssistantMessage
  | InterviewUserMessage
  | InterviewSystemMessage;

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
    const isThinking = message.isThinking === true;

    return (
      <div className="flex justify-start">
        <div className="flex max-w-[48%] min-w-0 flex-col items-start gap-2">
          <div className="flex items-center gap-2 px-1 text-[12px] font-medium text-[var(--token-color-text-secondary)]">
            <div className="flex size-8 shrink-0 items-center justify-center rounded-full bg-[rgba(15,23,42,0.08)] text-[11px] font-semibold text-[var(--token-color-text-secondary)]">
              HR
            </div>
            <span>面试官 HR</span>
          </div>
          <div className="interview-bubble-shell interview-bubble-shell--assistant">
            <span
              aria-hidden="true"
              className={cn(
                "interview-bubble-tail interview-bubble-tail--assistant interview-bubble-tail--assistant-top",
                isThinking && "interview-bubble-tail--assistant-thinking",
              )}
            />
            <article
              className={cn(
                "inline-block w-fit max-w-full rounded-[20px] rounded-bl-md px-4 py-3 shadow-[0_8px_20px_rgba(15,23,42,0.06)] transition-colors duration-200",
                isThinking
                  ? "border border-[rgba(148,163,184,0.2)] bg-[rgba(248,250,252,0.98)]"
                  : message.isCurrent
                    ? "bg-[rgba(255,255,255,0.98)]"
                    : "bg-[rgba(255,255,255,0.92)]",
              )}
            >
              {isThinking ? (
                <div
                  aria-live="polite"
                  className="flex items-center gap-3 text-[15px] leading-7 text-[var(--token-color-text-primary)]"
                >
                  <span>{message.body}</span>
                  <span aria-hidden="true" className="flex items-center gap-1.5">
                    <span className="interview-thinking-dot" />
                    <span
                      className="interview-thinking-dot"
                      style={{ animationDelay: "160ms" }}
                    />
                    <span
                      className="interview-thinking-dot"
                      style={{ animationDelay: "320ms" }}
                    />
                  </span>
                </div>
              ) : (
                <p className="whitespace-pre-wrap text-[15px] leading-7 text-[var(--token-color-text-primary)]">
                  {message.body}
                </p>
              )}
            </article>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-end gap-2">
      <div className="self-center text-[12px] text-[var(--token-color-text-tertiary)]">
        {message.timestamp}
      </div>
      <div className="interview-bubble-shell interview-bubble-shell--user max-w-[48%] self-end">
        <article className="inline-block w-fit max-w-full rounded-[20px] rounded-br-md bg-[linear-gradient(180deg,#2f7df6,#2369d4)] px-4 py-3 text-white shadow-[0_14px_32px_rgba(35,105,212,0.22)]">
          <p className="whitespace-pre-wrap text-[15px] leading-7">{message.body}</p>
        </article>
      </div>
    </div>
  );
}
