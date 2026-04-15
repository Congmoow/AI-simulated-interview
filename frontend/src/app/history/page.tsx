"use client";

import { useEffect, useState } from "react";
import { GrowthChart } from "@/components/charts/growth-chart";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/state-panel";
import { getGrowth } from "@/services/report-service";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { RequireLoginState } from "@/components/auth/require-login-state";
import type { GrowthDetail } from "@/types/api";

export default function HistoryPage() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const openLogin = useAuthModalStore((state) => state.openLogin);
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
        const growthResponse = await getGrowth({ timeRange: "all" });
        setGrowth(growthResponse);
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "成长趋势加载失败");
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin]);

  if (!hydrated) {
    return <LoadingState label="正在加载成长趋势..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在加载成长趋势..." />;
  }

  if (error) {
    return <ErrorState description={error} />;
  }

  if (!growth) {
    return (
      <EmptyState
        description="完成至少一场面试后，这里会展示你的成长趋势。"
        title="还没有可展示的成长数据"
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
    </div>
  );
}
