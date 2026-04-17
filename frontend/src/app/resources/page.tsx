"use client";

import { useEffect, useState } from "react";

import { RequireLoginState } from "@/components/auth/require-login-state";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/state-panel";
import { getResources } from "@/services/report-service";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { useAuthStore } from "@/stores/auth-store";
import type { ResourceRecommendation } from "@/types/api";
import { buildResourceMeta } from "@/features/resources/resource-view";

function openResource(url?: string | null) {
  if (!url) {
    return;
  }

  window.open(url, "_blank", "noopener,noreferrer");
}

export default function ResourcesPage() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const [resources, setResources] = useState<ResourceRecommendation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!accessToken) {
      openLogin({ type: "navigate", target: "/resources" });
      return;
    }

    void (async () => {
      try {
        const response = await getResources({ limit: 12 });
        setResources(response);
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "学习资源加载失败");
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin]);

  if (!hydrated) {
    return <LoadingState label="正在加载学习资源..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在加载学习资源..." />;
  }

  if (error) {
    return <ErrorState description={error} />;
  }

  if (resources.length === 0) {
    return (
      <EmptyState
        title="还没有可展示的学习资源"
        description="当前还没有可推荐的学习资源，请稍后再试。"
      />
    );
  }

  return (
    <div className="space-y-6">
      <section className="grid gap-6 xl:grid-cols-[0.75fr_1.25fr]">
        <Card className="space-y-4">
          <span className="section-label">学习资源</span>
          <div className="space-y-3">
            <p className="display-title !text-[clamp(2rem,3vw,3.2rem)]">{resources.length}</p>
            <p className="text-caption">
              结合你当前的训练场景，为你整理了一组可直接开始的推荐资源。
            </p>
          </div>
        </Card>
        <Card className="space-y-4">
          <span className="section-label">使用建议</span>
          <div className="space-y-3 text-caption">
            <p>优先从匹配度更高的资源开始，先补短板，再做系统化练习。</p>
            <p>建议每次只选 1 到 2 个资源，学完后立刻回到模拟面试里复练。</p>
            <p>如果资源类型不同，优先组合“教程 + 实战”，不要同时打开太多内容。</p>
          </div>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-2">
        {resources.map((resource) => {
          const meta = buildResourceMeta(resource);

          return (
            <Card className="space-y-4" key={resource.resourceId}>
              <div className="flex items-start justify-between gap-3">
                <div className="space-y-2">
                  <p className="section-title !text-[1.35rem]">{resource.title}</p>
                  <p className="text-caption">
                    {resource.type} · {resource.provider ?? "平台资源"}
                  </p>
                </div>
                <span className="chip-info chip-info--primary">匹配度 {resource.matchScore}</span>
              </div>

              {resource.targetDimensions.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {resource.targetDimensions.map((dimension) => (
                    <span className="chip-info" key={`${resource.resourceId}-${dimension}`}>
                      {dimension}
                    </span>
                  ))}
                </div>
              ) : null}

              {meta.length > 0 ? (
                <div className="grid gap-3 sm:grid-cols-3">
                  {meta.map((item) => (
                    <div className="surface-muted p-3" key={`${resource.resourceId}-${item.label}`}>
                      <p className="meta-label">{item.label}</p>
                      <p className="mt-2 text-sm font-semibold text-[var(--token-color-text-primary)]">
                        {item.value}
                      </p>
                    </div>
                  ))}
                </div>
              ) : null}

              <div className="flex items-center justify-between gap-3">
                <p className="text-caption">
                  {typeof resource.rating === "number"
                    ? `评分 ${resource.rating.toFixed(1)} / 5`
                    : "暂无评分信息"}
                </p>
                <button
                  className="primary-button !px-4 !py-2.5 !text-[13px]"
                  disabled={!resource.url}
                  onClick={() => openResource(resource.url)}
                  type="button"
                >
                  {resource.url ? "查看资源" : "暂无链接"}
                </button>
              </div>
            </Card>
          );
        })}
      </section>
    </div>
  );
}
