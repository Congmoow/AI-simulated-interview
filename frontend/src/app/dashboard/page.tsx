"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { useRouter } from "next/navigation";
import { getDashboardInsights } from "@/services/dashboard-service";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorState, LoadingState } from "@/components/ui/state-panel";
import { RequireLoginState } from "@/components/auth/require-login-state";
import { GrowthChart } from "@/components/charts/growth-chart";
import { AbilityRadarChart } from "@/components/charts/ability-radar-chart";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { getRequestErrorMessage } from "@/utils/request-error";
import { getReportDetailPath } from "@/features/report/report-route";
import type { DashboardInsightsDetail } from "@/types/api";

export default function DashboardPage() {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const user = useAuthStore((state) => state.user);
  const openLogin = useAuthModalStore((state) => state.openLogin);

  const [insights, setInsights] = useState<DashboardInsightsDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const heroSummary = useMemo(
    () => (insights ? buildHeroSummary(insights) : "正在根据你的历史面试记录生成能力画像。"),
    [insights],
  );

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!accessToken) {
      openLogin({ type: "navigate", target: "/dashboard" });
      return;
    }

    void (async () => {
      try {
        const dashboardInsights = await getDashboardInsights();
        setInsights(dashboardInsights);
      } catch (requestError) {
        setLoadError(getRequestErrorMessage(requestError, "个人能力概览加载失败"));
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin]);

  const scopeDescription = useMemo(
    () => (insights ? buildScopeDescription(insights) : ""),
    [insights],
  );

  if (!hydrated) {
    return <LoadingState label="正在生成个人能力概览..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在生成个人能力概览..." />;
  }

  if (loadError || !insights) {
    return <ErrorState description={loadError ?? "个人能力概览暂时不可用"} />;
  }

  if (insights.overview.totalReports === 0) {
    return (
      <DashboardEmptyState
        onStart={() => router.push("/interview")}
        scopeDescription={scopeDescription}
      />
    );
  }

  return (
    <div className="space-y-6">
      <section className="space-y-3 px-1 pt-1">
        <h1 className="display-title !text-[clamp(1.9rem,2.8vw,3rem)]">
          你好，{user?.username ?? "同学"}
        </h1>
        <p className="text-caption max-w-[920px] text-[length:var(--token-font-size-lg)]">
          {heroSummary}
        </p>
      </section>

      <section className="grid gap-6 xl:grid-cols-2">
        <Card className="space-y-5">
          <PanelTitle
            description="主图展示 6 维个人能力画像，悬停可查看映射到的原始评分维度。"
            icon={<RadarIcon className="h-5 w-5" />}
            title="能力画像"
          />
          {insights.abilityDimensions6.length > 0 ? (
            <AbilityRadarChart dimensions={insights.abilityDimensions6} />
          ) : (
            <PanelFallback description="当前报告中的能力评分还不够完整，暂时无法绘制能力画像。" />
          )}
        </Card>

        <Card className="space-y-5">
          <PanelTitle
            description="优先使用报告总分，缺失时回退到原始维度平均值。"
            icon={<TrendIcon className="h-5 w-5" />}
            title="最近表现趋势"
          />
          {insights.recentTrend.length > 0 ? (
            <GrowthChart
              points={insights.recentTrend.map((item) => ({
                date: item.date,
                score: item.score,
              }))}
            />
          ) : (
            <PanelFallback description="当前报告中缺少可用于趋势计算的分数，后续生成新报告后会自动补齐。" />
          )}
        </Card>
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.05fr_0.95fr]">
        <Card className="space-y-5">
          <PanelTitle
            description="从历次报告中高频出现的优势标签与证据摘要。"
            icon={<StrengthIcon className="h-5 w-5" />}
            title="我的强项"
          />
          <div className="space-y-4">
            {insights.strengths.length > 0 ? (
              insights.strengths.map((item) => (
                <StrengthCard item={item} key={item.key} />
              ))
            ) : (
              <PanelFallback description="还没有足够稳定的强项标签，完成更多模拟面试后这里会逐步成形。" />
            )}
          </div>
        </Card>

        <Card className="space-y-5">
          <PanelTitle
            description="当前最影响表现的短板、典型表现与下一步动作。"
            icon={<WeaknessIcon className="h-5 w-5" />}
            title="我的弱项 / 不足"
          />
          <div className="space-y-4">
            {insights.weaknesses.length > 0 ? (
              insights.weaknesses.map((item) => (
                <WeaknessCard item={item} key={item.key} />
              ))
            ) : (
              <PanelFallback description="当前报告里还没有稳定聚合出弱项标签，继续积累数据后会自动更新。" />
            )}
          </div>
        </Card>
      </section>

      <Card className="space-y-5 border-[rgba(17,24,39,0.10)] bg-[linear-gradient(180deg,rgba(255,255,255,0.95),rgba(249,250,251,0.92))]">
        <PanelTitle
          description="建议从影响最大、出现最频繁的问题开始修复。"
          icon={<ActionIcon className="h-5 w-5" />}
          title="接下来优先提升"
        />
        <div className="grid gap-4 lg:grid-cols-3">
          {insights.nextActions.map((item, index) => (
            <div
              className="surface-muted relative overflow-hidden p-4"
              key={`${item}-${index}`}
            >
              <div className="absolute inset-x-0 top-0 h-1 bg-[linear-gradient(90deg,rgba(0,102,255,0.16),rgba(6,182,212,0.24))]" />
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--token-color-text-tertiary)]">
                优先项 {index + 1}
              </p>
              <p className="mt-3 text-sm font-semibold leading-6">{item}</p>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}

function PanelTitle({
  title,
  description,
  icon,
}: {
  title: string;
  description: string;
  icon: ReactNode;
}) {
  return (
    <div className="flex items-start justify-between gap-4">
      <div className="space-y-2">
        <span className="section-label">{title}</span>
        <p className="text-caption max-w-[560px]">{description}</p>
      </div>
      <span className="inline-flex h-11 w-11 items-center justify-center rounded-full bg-[rgba(0,102,255,0.08)] text-[var(--token-color-primary)]">
        {icon}
      </span>
    </div>
  );
}

function StrengthCard({
  item,
}: {
  item: DashboardInsightsDetail["strengths"][number];
}) {
  return (
    <div className="surface-muted group space-y-4 border-[rgba(16,185,129,0.12)] p-5 transition-all duration-300 ease-out hover:-translate-y-0.5 hover:shadow-[0_16px_34px_rgba(16,185,129,0.12)]">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <span className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-[rgba(16,185,129,0.10)] text-[var(--token-color-success)]">
              <StrengthIcon className="h-5 w-5" />
            </span>
            <div>
              <h3 className="text-lg font-semibold">{item.title}</h3>
              <p className="text-caption">频次 {item.evidenceCount} 次</p>
            </div>
          </div>
        </div>
        <span className="rounded-full border border-[rgba(16,185,129,0.18)] bg-[rgba(16,185,129,0.08)] px-2.5 py-1 text-[11px] font-semibold text-[var(--token-color-success)]">
          最近出现 {formatDate(item.lastSeenAt)}
        </span>
      </div>
      <p className="text-sm leading-6 text-[var(--token-color-text-secondary)]">
        {item.description}
      </p>
      {item.evidenceSamples.length > 0 ? (
        <div className="rounded-[var(--token-radius-xl)] border border-[rgba(16,185,129,0.14)] bg-white/70 p-4">
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--token-color-text-tertiary)]">
            证据摘要
          </p>
          <div className="mt-3 space-y-2">
            {item.evidenceSamples.map((sample) => (
              <p className="text-caption leading-6" key={sample}>
                {sample}
              </p>
            ))}
          </div>
        </div>
      ) : null}
      <SourceLinks sources={item.sources} />
    </div>
  );
}

function WeaknessCard({
  item,
}: {
  item: DashboardInsightsDetail["weaknesses"][number];
}) {
  return (
    <div className="surface-muted group space-y-4 border-[rgba(245,158,11,0.16)] p-5 transition-all duration-300 ease-out hover:-translate-y-0.5 hover:shadow-[0_16px_34px_rgba(245,158,11,0.12)]">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <span className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-[rgba(245,158,11,0.10)] text-[var(--token-color-warning)]">
              <WeaknessIcon className="h-5 w-5" />
            </span>
            <div>
              <h3 className="text-lg font-semibold">{item.title}</h3>
              <p className="text-caption">频次 {item.evidenceCount} 次</p>
            </div>
          </div>
        </div>
        <span className="rounded-full border border-[rgba(245,158,11,0.18)] bg-[rgba(245,158,11,0.08)] px-2.5 py-1 text-[11px] font-semibold text-[var(--token-color-warning)]">
          最近出现 {formatDate(item.lastSeenAt)}
        </span>
      </div>
      <p className="text-sm leading-6 text-[var(--token-color-text-secondary)]">
        {item.description}
      </p>
      <div className="space-y-3 rounded-[var(--token-radius-xl)] border border-[rgba(245,158,11,0.14)] bg-white/74 p-4">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--token-color-text-tertiary)]">
            典型表现
          </p>
          <div className="mt-3 space-y-2">
            {item.typicalBehaviors.map((behavior) => (
              <p className="text-caption leading-6" key={behavior}>
                {behavior}
              </p>
            ))}
          </div>
        </div>
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--token-color-text-tertiary)]">
            建议动作
          </p>
          <p className="mt-3 text-sm font-semibold leading-6">{item.suggestion}</p>
        </div>
      </div>
      <SourceLinks sources={item.sources} />
    </div>
  );
}

function SourceLinks({
  sources,
}: {
  sources: DashboardInsightsDetail["strengths"][number]["sources"];
}) {
  if (sources.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--token-color-text-tertiary)]">
        相关报告
      </p>
      <div className="flex flex-wrap gap-2">
        {sources.map((source) => (
          <Link
            className="inline-flex items-center gap-2 rounded-full border border-[rgba(17,24,39,0.08)] bg-white px-3 py-1.5 text-[12px] font-semibold text-[var(--token-color-text-secondary)] transition-colors hover:border-[rgba(0,102,255,0.18)] hover:text-[var(--token-color-primary)]"
            href={getReportDetailPath(source.interviewId)}
            key={`${source.reportId}-${source.interviewId}`}
          >
            {source.positionName}
            <span className="text-[var(--token-color-text-tertiary)]">
              {formatDate(source.generatedAt)}
            </span>
          </Link>
        ))}
      </div>
    </div>
  );
}

function PanelFallback({ description }: { description: string }) {
  return (
    <div className="state-card !min-h-[180px]">
      <p className="text-caption max-w-[420px]">{description}</p>
    </div>
  );
}

function DashboardEmptyState({
  onStart,
  scopeDescription,
}: {
  onStart: () => void;
  scopeDescription: string;
}) {
  return (
    <Card className="state-card !min-h-[420px] overflow-hidden border-[rgba(0,102,255,0.10)] bg-[linear-gradient(180deg,rgba(255,255,255,0.96),rgba(239,246,255,0.86))] px-8 py-10">
      <span className="inline-flex h-16 w-16 items-center justify-center rounded-full bg-[rgba(0,102,255,0.08)] text-[var(--token-color-primary)]">
        <RadarIcon className="h-8 w-8" />
      </span>
      <div className="space-y-3">
        <p className="section-title">你还没有足够的面试数据</p>
        <p className="text-caption max-w-[560px]">
          完成 1~2 场模拟面试后，这里会自动生成你的能力强项、短板分析与趋势画像。
        </p>
        <p className="text-caption max-w-[560px]">{scopeDescription}</p>
      </div>
      <Button onClick={onStart} type="button">
        开始一场模拟面试
      </Button>
    </Card>
  );
}

function buildScopeDescription(insights: DashboardInsightsDetail) {
  if (insights.scope.fallbackTriggered) {
    return `当前目标岗位暂无可用报告，已改用全部历史报告生成画像（共 ${insights.scope.reportCount} 份）。`;
  }

  if (insights.scope.actualScope === "target_position") {
    return `当前画像基于目标岗位「${insights.scope.targetPositionName ?? insights.scope.targetPositionCode ?? "未设置"}」的 ${insights.scope.reportCount} 份历史报告。`;
  }

  return `当前画像基于全部历史报告生成，共纳入 ${insights.scope.reportCount} 份报告。`;
}

function buildHeroSummary(insights: DashboardInsightsDetail) {
  const strengthSummary = insights.strengths
    .slice(0, 2)
    .map((item) => item.title)
    .join("、");
  const weaknessSummary = insights.weaknesses
    .slice(0, 2)
    .map((item) => item.title)
    .join("、");

  if (strengthSummary && weaknessSummary) {
    return `根据你最近的面试记录，你当前更偏向“${strengthSummary}”，但 ${weaknessSummary} 仍需继续加强。`;
  }

  if (strengthSummary) {
    return `根据你最近的面试记录，你当前已经显现出“${strengthSummary}”等优势特征。`;
  }

  if (weaknessSummary) {
    return `根据你最近的面试记录，当前最需要优先修复的是 ${weaknessSummary}。`;
  }

  return "根据你最近的面试记录，这里会持续更新你的能力强项、短板与趋势变化。";
}

function formatDate(value?: string | null) {
  if (!value) {
    return "待生成";
  }

  return new Intl.DateTimeFormat("zh-CN", {
    month: "2-digit",
    day: "2-digit",
  }).format(new Date(value));
}

function StrengthIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="1.8"
      viewBox="0 0 24 24"
    >
      <path d="M12 3l7 3v5c0 4.8-2.7 8.8-7 10-4.3-1.2-7-5.2-7-10V6l7-3z" />
      <path d="m9.5 12 1.8 1.8L14.8 10.3" />
    </svg>
  );
}

function WeaknessIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="1.8"
      viewBox="0 0 24 24"
    >
      <path d="M12 4 3 20h18L12 4z" />
      <path d="M12 10v4" />
      <path d="M12 18h.01" />
    </svg>
  );
}

function TrendIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="1.8"
      viewBox="0 0 24 24"
    >
      <path d="M3 17 9 11l4 4 8-8" />
      <path d="M17 7h4v4" />
    </svg>
  );
}

function RadarIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="1.8"
      viewBox="0 0 24 24"
    >
      <path d="m12 3 7 4v10l-7 4-7-4V7l7-4z" />
      <path d="m12 7 3.5 2v5L12 16l-3.5-2V9L12 7z" />
      <path d="M12 3v4" />
      <path d="M19 7l-3.5 2" />
      <path d="M5 7l3.5 2" />
    </svg>
  );
}

function ActionIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="1.8"
      viewBox="0 0 24 24"
    >
      <path d="M4 19h16" />
      <path d="M8 19V8" />
      <path d="M12 19V5" />
      <path d="M16 19v-7" />
    </svg>
  );
}
