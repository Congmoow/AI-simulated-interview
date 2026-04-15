"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { getPositions } from "@/services/catalog-service";
import { getCurrentUser } from "@/services/auth-service";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorState, LoadingState } from "@/components/ui/state-panel";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { RequireLoginState } from "@/components/auth/require-login-state";
import { writeStoredAuth } from "@/utils/storage";
import type { CurrentUser, PositionSummary } from "@/types/api";

export default function DashboardPage() {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const user = useAuthStore((state) => state.user);
  const setSession = useAuthStore((state) => state.setSession);
  const [profile, setProfile] = useState<CurrentUser | null>(user);
  const [positions, setPositions] = useState<PositionSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
        const [currentUser, positionList] = await Promise.all([
          getCurrentUser(),
          getPositions(),
        ]);
        setProfile(currentUser);
        setPositions(positionList);
        writeStoredAuth({
          accessToken,
          refreshToken: useAuthStore.getState().refreshToken ?? "",
          expiresIn: useAuthStore.getState().expiresIn ?? 0,
          user: currentUser,
        });
        setSession({
          accessToken,
          refreshToken: useAuthStore.getState().refreshToken ?? "",
          expiresIn: useAuthStore.getState().expiresIn ?? 0,
          user: currentUser,
        });
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "仪表盘加载失败");
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin, router, setSession, user]);

  if (!hydrated) {
    return <LoadingState label="正在准备仪表盘..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在准备仪表盘..." />;
  }

  if (error) {
    return <ErrorState description={error} />;
  }

  return (
    <div className="space-y-6">
      <section className="grid gap-6 xl:grid-cols-[1.3fr_0.7fr]">
        <Card className="space-y-5">
          <span className="section-label">欢迎回来</span>
          <div className="space-y-3">
            <h2 className="display-title !text-[clamp(2rem,3vw,3.4rem)]">
              {profile?.username}，今天继续把岗位准备推进一格。
            </h2>
            <p className="text-caption max-w-[720px] text-[length:var(--token-font-size-lg)]">
              你可以直接选择岗位开始新一场模拟面试，也可以进入历史趋势查看最近表现变化。
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            <Button onClick={() => router.push("/interview")}>开始模拟面试</Button>
            <Button onClick={() => router.push("/history")} variant="secondary">
              查看历史趋势
            </Button>
          </div>
        </Card>
        <Card className="space-y-4">
          <span className="section-label">当前档案</span>
          <div>
            <p className="text-caption">目标岗位</p>
            <p className="mt-2 text-xl font-semibold">
              {profile?.targetPosition?.name ?? "尚未设置"}
            </p>
          </div>
          <div>
            <p className="text-caption">账号身份</p>
            <p className="mt-2 text-xl font-semibold">{profile?.role ?? "user"}</p>
          </div>
          <div>
            <p className="text-caption">邮箱</p>
            <p className="mt-2 text-base font-semibold">{profile?.email}</p>
          </div>
        </Card>
      </section>
      <section className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
        {positions.map((position) => (
          <Card className="flex h-full flex-col justify-between gap-5" key={position.code}>
            <div className="space-y-3">
              <span className="section-label">{position.code}</span>
              <h3 className="section-title">{position.name}</h3>
              <p className="text-caption">{position.description}</p>
            </div>
            <div className="space-y-4">
              <div className="flex flex-wrap gap-2">
                {position.tags.map((tag) => (
                  <span
                    className="rounded-full bg-[rgba(17,24,39,0.05)] px-3 py-2 text-xs font-semibold"
                    key={tag}
                  >
                    {tag}
                  </span>
                ))}
              </div>
              <div className="flex items-center justify-between">
                <p className="text-caption">题量 {position.questionCount}</p>
                <Button onClick={() => router.push(`/interview?positionCode=${position.code}`)}>
                  进入该岗位
                </Button>
              </div>
            </div>
          </Card>
        ))}
      </section>
    </div>
  );
}
