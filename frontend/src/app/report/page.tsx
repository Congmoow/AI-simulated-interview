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

const STATUS_META: Record<string, { label: string; className: string }> = {
  completed: {
    label: "已完成",
    className: "status-badge status-badge--success",
  },
  generating_report: {
    label: "生成中",
    className: "status-badge status-badge--warning",
  },
  report_failed: {
    label: "生成失败",
    className: "status-badge status-badge--danger",
  },
  in_progress: {
    label: "进行中",
    className: "status-badge status-badge--info",
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
          className="surface-panel surface-panel-topline interactive-card group p-5"
          key={item.interviewId}
        >
          <div className="flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
            <div className="space-y-4">
              <div className="space-y-2">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="section-label !tracking-[0.14em]">报告记录</span>
                  <span
                    className={cn(
                      STATUS_META[item.status]?.className ?? "status-badge status-badge--neutral",
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
                <span className="chip-info chip-info--primary">
                  {INTERVIEW_MODE_LABELS[item.interviewMode] ?? item.interviewMode}
                </span>
                <span className="chip-info">共 {item.roundCount} 轮</span>
                <span className="chip-info">时长 {formatDuration(item.duration)}</span>
              </div>
            </div>
            <div className="subtle-panel flex flex-col items-start gap-3 p-4 lg:min-w-[210px] lg:items-end">
              <div className="space-y-1 lg:text-right">
                <p className="meta-label">综合得分</p>
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
