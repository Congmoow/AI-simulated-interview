"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { GrowthChart } from "@/components/charts/growth-chart";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/state-panel";
import { getInterviewHistory } from "@/services/interview-service";
import { getGrowth } from "@/services/report-service";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { RequireLoginState } from "@/components/auth/require-login-state";
import type { GrowthDetail, InterviewHistoryItem } from "@/types/api";

export default function HistoryPage() {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const [history, setHistory] = useState<InterviewHistoryItem[]>([]);
  const [growth, setGrowth] = useState<GrowthDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!accessToken) {
      openLogin({ type: "navigate", target: "/history" });
      return;
    }

    void (async () => {
      try {
        const [historyResponse, growthResponse] = await Promise.all([
          getInterviewHistory({ page: 1, pageSize: 10 }),
          getGrowth({ timeRange: "all" }),
        ]);
        setHistory(historyResponse.items);
        setGrowth(growthResponse);
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "历史数据加载失败");
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin, router]);

  if (!hydrated) {
    return <LoadingState label="正在加载历史记录与成长趋势..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在加载历史记录与成长趋势..." />;
  }

  if (error) {
    return <ErrorState description={error} />;
  }

  if (!growth || history.length === 0) {
    return (
      <EmptyState
        description="完成至少一场面试后，这里会展示你的趋势变化和历史记录。"
        title="还没有可展示的历史数据"
      />
    );
  }

  return (
    <div className="space-y-6">
      <section className="grid gap-6 xl:grid-cols-[0.75fr_1.25fr]">
        <Card className="space-y-4">
          <span className="section-label">趋势摘要</span>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-1">
            <div>
              <p className="text-caption">累计面试</p>
              <p className="mt-2 text-3xl font-semibold">{growth.summary.totalInterviews}</p>
            </div>
            <div>
              <p className="text-caption">平均分</p>
              <p className="mt-2 text-3xl font-semibold">{growth.summary.averageScore.toFixed(1)}</p>
            </div>
            <div>
              <p className="text-caption">分数变化</p>
              <p className="mt-2 text-3xl font-semibold">{growth.summary.scoreChange.toFixed(1)}</p>
            </div>
            <div>
              <p className="text-caption">最强维度</p>
              <p className="mt-2 text-xl font-semibold">
                {growth.summary.strongestDimension ?? "待积累"}
              </p>
            </div>
          </div>
        </Card>
        <Card className="space-y-4">
          <span className="section-label">成长趋势</span>
          <GrowthChart
            points={growth.trend.map((item) => ({
              date: item.date,
              score: item.overallScore,
            }))}
          />
        </Card>
      </section>
      <Card className="space-y-5">
        <span className="section-label">历史面试</span>
        <div className="space-y-4">
          {history.map((item) => (
            <div className="surface-muted flex flex-col gap-4 p-4 lg:flex-row lg:items-center lg:justify-between" key={item.interviewId}>
              <div className="space-y-2">
                <p className="font-semibold">{item.positionName}</p>
                <p className="text-caption">
                  {item.interviewMode} · {item.status} · 共 {item.roundCount} 轮
                </p>
              </div>
              <div className="flex flex-wrap items-center gap-3">
                <p className="text-caption">得分 {item.totalScore ?? "--"}</p>
                <Link className="primary-button" href={`/report/${item.interviewId}`}>
                  查看报告
                </Link>
              </div>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}
