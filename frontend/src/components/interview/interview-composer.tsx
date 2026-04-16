"use client";

import type { KeyboardEventHandler } from "react";
import { Button } from "@/components/ui/button";

interface InterviewComposerProps {
  value: string;
  placeholder: string;
  disabled: boolean;
  canSubmit: boolean;
  sendLabel: string;
  statusText?: string;
  hintText?: string;
  draftLabel?: string;
  canRestoreDraft?: boolean;
  onChange: (value: string) => void;
  onSubmit: () => void;
  onRestoreDraft?: () => void;
  onKeyDown?: KeyboardEventHandler<HTMLTextAreaElement>;
}

export function InterviewComposer({
  value,
  placeholder,
  disabled,
  canSubmit,
  sendLabel,
  statusText,
  hintText,
  draftLabel,
  canRestoreDraft = false,
  onChange,
  onSubmit,
  onRestoreDraft,
  onKeyDown,
}: InterviewComposerProps) {
  return (
    <div className="sticky bottom-0 z-20 -mb-10 bg-[rgba(255,255,255,0.96)] px-1 pb-0 pt-4 backdrop-blur">
      <div className="mx-auto w-full max-w-[720px] space-y-1">
        {draftLabel || (canRestoreDraft && onRestoreDraft) || statusText || hintText ? (
          <div className="flex flex-wrap items-center justify-between gap-2 px-1 text-[12px] text-[var(--token-color-text-secondary)]">
            <div className="flex flex-wrap items-center gap-2">
              {draftLabel ? (
                <span className="rounded-full border border-[rgba(15,23,42,0.08)] bg-[rgba(255,255,255,0.88)] px-3 py-1.5">
                  {draftLabel}
                </span>
              ) : null}
              {canRestoreDraft && onRestoreDraft ? (
                <button
                  className="rounded-full border border-[rgba(0,102,255,0.14)] bg-[rgba(239,246,255,0.9)] px-3 py-1.5 font-medium text-[var(--token-color-primary)] transition-colors hover:bg-[rgba(219,234,254,0.92)]"
                  onClick={onRestoreDraft}
                  type="button"
                >
                  恢复草稿
                </button>
              ) : null}
              {statusText ? <span>{statusText}</span> : null}
            </div>
            {hintText ? <span>{hintText}</span> : null}
          </div>
        ) : null}

        <div className="surface-card relative overflow-hidden rounded-[30px] border-[rgba(15,23,42,0.08)] !bg-white p-0.5">
          <textarea
            className="input-shell min-h-[36px] resize-none !rounded-[24px] !border-transparent !bg-white !pb-10 !pr-[4.5rem] !pt-2 !shadow-none disabled:cursor-not-allowed disabled:opacity-80"
            disabled={disabled}
            onChange={(event) => onChange(event.target.value)}
            onKeyDown={onKeyDown}
            placeholder={placeholder}
            value={value}
          />

          <div className="pointer-events-none absolute inset-x-4 bottom-3 flex items-end justify-end gap-4">
            <div className="pointer-events-auto flex items-center gap-2">
              <Button
                aria-label={sendLabel}
                className="!h-9 !w-9 !rounded-full !p-0 shadow-[0_8px_18px_rgba(15,23,42,0.14)] transform-gpu transition-all duration-200 hover:-translate-y-0.5 hover:shadow-[0_14px_24px_rgba(0,102,255,0.22)] active:translate-y-0 active:shadow-[0_8px_16px_rgba(0,102,255,0.18)]"
                disabled={!canSubmit}
                onClick={onSubmit}
                title={sendLabel}
                type="button"
              >
                <svg
                  aria-hidden="true"
                  className="h-4 w-4"
                  fill="none"
                  viewBox="0 0 16 16"
                  xmlns="http://www.w3.org/2000/svg"
                >
                  <path
                    d="M8 12V4M8 4L4.75 7.25M8 4L11.25 7.25"
                    stroke="currentColor"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="1.6"
                  />
                </svg>
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
