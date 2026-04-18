"use client";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";

export type InterviewSystemAction = "view-report" | "retry-report";
export type InterviewSystemTone = "default" | "warning" | "success" | "danger";

const TONE_CLASS_NAMES: Record<InterviewSystemTone, string> = {
  default:
    "border-[rgba(15,23,42,0.08)] bg-[rgba(248,250,252,0.92)] text-[var(--token-color-text-secondary)]",
  warning:
    "border-[rgba(245,158,11,0.18)] bg-[rgba(255,251,235,0.96)] text-[rgba(146,64,14,0.92)]",
  success:
    "border-[rgba(16,185,129,0.18)] bg-[rgba(236,253,245,0.96)] text-[rgba(6,95,70,0.92)]",
  danger:
    "border-[rgba(239,68,68,0.18)] bg-[rgba(254,242,242,0.96)] text-[rgba(153,27,27,0.92)]",
};

const DOT_CLASS_NAMES: Record<InterviewSystemTone, string> = {
  default: "bg-[var(--token-color-text-tertiary)]",
  warning: "bg-[rgba(245,158,11,0.92)]",
  success: "bg-[rgba(16,185,129,0.92)]",
  danger: "bg-[rgba(239,68,68,0.92)]",
};

interface InterviewSystemNoticeProps {
  body: string;
  tone?: InterviewSystemTone;
  displayStyle?: "card" | "plain" | "inline";
  actionLabel?: string;
  actionKey?: InterviewSystemAction;
  onAction?: (action: InterviewSystemAction) => void;
}

export function InterviewSystemNotice({
  body,
  tone = "default",
  displayStyle = "card",
  actionLabel,
  actionKey,
  onAction,
}: InterviewSystemNoticeProps) {
  if (displayStyle === "plain") {
    return (
      <div className="flex justify-center py-2">
        <p className="text-center text-[13px] leading-6 text-[var(--token-color-text-tertiary)]">
          {body}
        </p>
      </div>
    );
  }

  if (displayStyle === "inline") {
    return (
      <div className="flex justify-center py-2">
        <div className="interview-system-notice-inline flex flex-wrap items-center justify-center gap-3 text-sm leading-6">
          <span className={cn("h-2.5 w-2.5 rounded-full", DOT_CLASS_NAMES[tone])} />
          <p className="text-center text-[var(--token-color-text-secondary)]">{body}</p>
          {actionLabel && actionKey ? (
            <Button
              className="!px-4 !py-2 text-xs"
              onClick={() => onAction?.(actionKey)}
              type="button"
              variant="secondary"
            >
              {actionLabel}
            </Button>
          ) : null}
        </div>
      </div>
    );
  }

  return (
    <div className="flex justify-center">
      <div
        className={cn(
          "flex w-full max-w-[760px] flex-wrap items-center justify-center gap-3 rounded-[22px] border px-4 py-3 text-sm leading-6 shadow-[0_14px_28px_rgba(15,23,42,0.05)]",
          TONE_CLASS_NAMES[tone],
        )}
      >
        <span className={cn("h-2.5 w-2.5 rounded-full", DOT_CLASS_NAMES[tone])} />
        <p className="text-center">{body}</p>
        {actionLabel && actionKey ? (
          <Button
            className="!px-4 !py-2 text-xs"
            onClick={() => onAction?.(actionKey)}
            type="button"
            variant="secondary"
          >
            {actionLabel}
          </Button>
        ) : null}
      </div>
    </div>
  );
}
