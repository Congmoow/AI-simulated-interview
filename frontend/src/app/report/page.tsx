"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { RequireLoginState } from "@/components/auth/require-login-state";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/state-panel";
import { getInterviewHistory } from "@/services/interview-service";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { useAuthStore } from "@/stores/auth-store";
import { cn } from "@/lib/cn";
import type { InterviewHistoryItem } from "@/types/api";

const INTERVIEW_MODE_LABELS: Record<string, string> = {
  friendly: "轻松模式",
  standard: "标准模式",
  stress: "高压模式",
};

const STATUS_META: Record<
  string,
  { label: string; className: string }
> = {
  completed: {
    label: "已完成",
    className:
      "border-[rgba(16,185,129,0.16)] bg-[rgba(16,185,129,0.08)] text-[var(--token-color-success)]",
  },
  generating_report: {
    label: "生成中",
    className:
      "border-[rgba(245,158,11,0.18)] bg-[rgba(245,158,11,0.08)] text-[var(--token-color-warning)]",
  },
  report_failed: {
    label: "生成失败",
    className:
      "border-[rgba(239,68,68,0.18)] bg-[rgba(239,68,68,0.08)] text-[var(--token-color-danger)]",
  },
  in_progress: {
    label: "进行中",
    className:
      "border-[rgba(0,102,255,0.16)] bg-[rgba(0,102,255,0.08)] text-[var(--token-color-primary)]",
  },
};

function formatDateTime(value?: string | null) {
  if (!value) {
    return "时间待同步";
  }

  return new Intl.DateTimeFormat("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}

function formatDuration(duration: number) {
  if (!duration || duration <= 0) {
    return "未记录时长";
  }

  const minutes = Math.max(1, Math.round(duration / 60));
  return `${minutes} 分钟`;
}

export default function ReportIndexPage() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const [history, setHistory] = useState<InterviewHistoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!accessToken) {
      openLogin({ type: "navigate", target: "/report" });
      return;
    }

    void (async () => {
      try {
        const historyResponse = await getInterviewHistory({ page: 1, pageSize: 10 });
        setHistory(historyResponse.items);
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "历史面试加载失败");
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin]);

  if (!hydrated) {
    return <LoadingState label="正在加载历史面试..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在加载历史面试..." />;
  }

  if (error) {
    return <ErrorState description={error} />;
  }

  if (history.length === 0) {
    return (
      <EmptyState
        description="完成至少一场面试后，这里会展示你的历史面试记录。"
        title="还没有可展示的面试记录"
      />
    );
  }

  return (
    <div className="space-y-4">
      {history.map((item) => (
        <div
          className="group relative overflow-hidden rounded-[var(--token-radius-2xl)] border border-[var(--token-color-border-default)] bg-[linear-gradient(180deg,rgba(255,255,255,0.96),rgba(246,248,252,0.92))] p-5 shadow-[0_10px_26px_rgba(17,24,39,0.06)] transition-all duration-300 ease-out hover:-translate-y-0.5 hover:shadow-[0_18px_38px_rgba(17,24,39,0.10)]"
          key={item.interviewId}
        >
          <div className="absolute inset-x-5 top-0 h-px bg-[linear-gradient(90deg,transparent,rgba(0,102,255,0.22),transparent)]" />
          <div className="flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
            <div className="space-y-4">
              <div className="space-y-2">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="section-label !tracking-[0.14em]">报告记录</span>
                  <span
                    className={cn(
                      "inline-flex items-center rounded-full border px-2.5 py-1 text-[11px] font-semibold",
                      STATUS_META[item.status]?.className ??
                        "border-[rgba(17,24,39,0.10)] bg-[rgba(17,24,39,0.05)] text-[var(--token-color-text-secondary)]",
                    )}
                  >
                    {STATUS_META[item.status]?.label ?? item.status}
                  </span>
                </div>
                <h3 className="section-title !text-[clamp(1.5rem,2vw,2rem)]">
                  {item.positionName}
                </h3>
                <p className="text-caption">
                  创建于 {formatDateTime(item.createdAt)}
                  {item.completedAt ? ` · 完成于 ${formatDateTime(item.completedAt)}` : ""}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                <span className="inline-flex items-center rounded-full border border-[rgba(0,102,255,0.10)] bg-[rgba(0,102,255,0.06)] px-3 py-1.5 text-[12px] font-semibold text-[var(--token-color-primary)]">
                  {INTERVIEW_MODE_LABELS[item.interviewMode] ?? item.interviewMode}
                </span>
                <span className="inline-flex items-center rounded-full border border-[rgba(17,24,39,0.08)] bg-white px-3 py-1.5 text-[12px] font-semibold text-[var(--token-color-text-secondary)]">
                  共 {item.roundCount} 轮
                </span>
                <span className="inline-flex items-center rounded-full border border-[rgba(17,24,39,0.08)] bg-white px-3 py-1.5 text-[12px] font-semibold text-[var(--token-color-text-secondary)]">
                  时长 {formatDuration(item.duration)}
                </span>
              </div>
            </div>
            <div className="flex flex-col items-start gap-3 rounded-[var(--token-radius-xl)] border border-[rgba(17,24,39,0.06)] bg-white/80 p-4 lg:min-w-[210px] lg:items-end">
              <div className="space-y-1 lg:text-right">
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--token-color-text-tertiary)]">
                  综合得分
                </p>
                <p className="text-3xl font-semibold text-[var(--token-color-text-primary)]">
                  {item.totalScore ?? "--"}
                </p>
              </div>
              <Link
                className="primary-button !px-4 !py-2.5 !text-[13px] group-hover:translate-x-0.5"
                href={`/report/${item.interviewId}`}
              >
                查看报告
              </Link>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
