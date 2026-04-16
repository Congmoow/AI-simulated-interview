"use client";

import { Button } from "@/components/ui/button";

interface InterviewTopBarProps {
  positionName: string;
  currentRound: number;
  totalRounds: number;
  elapsedLabel: string;
  finishLabel: string;
  finishDisabled: boolean;
  onFinish: () => void;
}

export function InterviewTopBar({
  positionName,
  currentRound,
  totalRounds,
  elapsedLabel,
  finishLabel,
  finishDisabled,
  onFinish,
}: InterviewTopBarProps) {
  return (
    <div className="sticky top-0 z-20 bg-[rgba(255,255,255,0.94)] px-1 py-3 backdrop-blur">
      <div className="flex h-14 items-center justify-between gap-4 px-3">
        <div className="flex min-w-0 items-center gap-3">
          <span className="section-label !tracking-[0.12em]">当前面试岗位</span>
          <p className="truncate text-[15px] font-semibold text-[var(--token-color-text-primary)]">
            {positionName}
          </p>
        </div>

        <div className="flex flex-col items-end gap-1">
          <div className="hidden items-center justify-end gap-2 md:flex">
            <span className="px-1 py-1 text-[12px] font-medium text-[var(--token-color-text-secondary)]">
              第 {currentRound} / {totalRounds} 轮
            </span>
            <span className="px-1 py-1 text-[12px] font-medium text-[var(--token-color-text-secondary)]">
              计时 {elapsedLabel}
            </span>
          </div>
          <Button
            className="!px-4 !py-2.5 text-[13px] transform-gpu transition-all duration-200 hover:-translate-y-0.5 hover:shadow-[0_12px_24px_rgba(15,23,42,0.12)] active:translate-y-0 active:shadow-[0_6px_14px_rgba(15,23,42,0.10)]"
            disabled={finishDisabled}
            onClick={onFinish}
            type="button"
            variant="secondary"
          >
            {finishLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
