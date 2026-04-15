"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { getReport, getResources, getTrainingPlan } from "@/services/report-service";
import { Card } from "@/components/ui/card";
import { ErrorState, LoadingState } from "@/components/ui/state-panel";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { RequireLoginState } from "@/components/auth/require-login-state";
import type { ReportDetail, ResourceRecommendation, TrainingPlan } from "@/types/api";

export function ReportClient({ interviewId }: { interviewId: string }) {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const [report, setReport] = useState<ReportDetail | null>(null);
  const [resources, setResources] = useState<ResourceRecommendation[]>([]);
  const [trainingPlan, setTrainingPlan] = useState<TrainingPlan | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!accessToken) {
      openLogin({ type: "navigate", target: `/report/${interviewId}` });
      return;
    }

    void (async () => {
      try {
        const reportResponse = await getReport(interviewId);
        const [resourceResponse, trainingResponse] = await Promise.all([
          getResources({ limit: 4 }),
          getTrainingPlan({ interviewId }),
        ]);
        setReport(reportResponse);
        setResources(resourceResponse);
        setTrainingPlan(trainingResponse);
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "报告加载失败");
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, interviewId, openLogin, router]);

  if (!hydrated) {
    return <LoadingState label="正在生成并加载报告..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在生成并加载报告..." />;
  }

  if (error || !report) {
    return <ErrorState description={error ?? "未找到报告数据"} />;
  }

  return (
    <div className="space-y-6">
      <section className="grid gap-6 xl:grid-cols-[0.8fr_1.2fr]">
        <Card className="space-y-4">
          <span className="section-label">综合结论</span>
          <p className="display-title !text-[clamp(2.4rem,3vw,4rem)]">
            {report.overallScore.toFixed(0)}
          </p>
          <p className="text-caption">岗位：{report.positionName}</p>
          <p className="text-caption">生成时间：{new Date(report.generatedAt).toLocaleString("zh-CN")}</p>
        </Card>
        <Card className="space-y-5">
          <span className="section-label">能力维度</span>
          <div className="grid gap-3 md:grid-cols-2">
            {Object.entries(report.dimensionScores).map(([key, value]) => (
              <div className="surface-muted p-4" key={key}>
                <p className="text-caption">{key}</p>
                <p className="mt-2 text-xl font-semibold">{value.score.toFixed(1)}</p>
                <p className="text-caption mt-1">权重 {Math.round(value.weight * 100)}%</p>
              </div>
            ))}
          </div>
        </Card>
      </section>
      <section className="grid gap-6 lg:grid-cols-3">
        <Card className="space-y-4">
          <span className="section-label">优势</span>
          <div className="space-y-3">
            {report.strengths.map((item) => (
              <p className="text-caption" key={item}>
                {item}
              </p>
            ))}
          </div>
        </Card>
        <Card className="space-y-4">
          <span className="section-label">短板</span>
          <div className="space-y-3">
            {report.weaknesses.map((item) => (
              <p className="text-caption" key={item}>
                {item}
              </p>
            ))}
          </div>
        </Card>
        <Card className="space-y-4">
          <span className="section-label">学习建议</span>
          <div className="space-y-3">
            {report.learningSuggestions.map((item) => (
              <p className="text-caption" key={item}>
                {item}
              </p>
            ))}
          </div>
        </Card>
      </section>
      <section className="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
        <Card className="space-y-4">
          <span className="section-label">训练计划</span>
          <div className="space-y-4">
            {(trainingPlan?.schedule ?? report.trainingPlan).map((item, index) => (
              <div className="surface-muted space-y-2 p-4" key={index}>
                <p className="font-semibold">
                  {String((item.focus as string) ?? (item.topic as string) ?? `训练阶段 ${index + 1}`)}
                </p>
                <p className="text-caption">{JSON.stringify(item)}</p>
              </div>
            ))}
          </div>
        </Card>
        <Card className="space-y-4">
          <span className="section-label">推荐资源</span>
          <div className="space-y-4">
            {resources.map((resource) => (
              <div className="surface-muted space-y-2 p-4" key={resource.resourceId}>
                <p className="font-semibold">{resource.title}</p>
                <p className="text-caption">
                  {resource.type} · 匹配度 {resource.matchScore}
                </p>
                <p className="text-caption">{resource.provider ?? "平台资源"}</p>
              </div>
            ))}
          </div>
        </Card>
      </section>
    </div>
  );
}
