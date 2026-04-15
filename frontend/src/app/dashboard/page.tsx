"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { getPositions } from "@/services/catalog-service";
import { getCurrentUser } from "@/services/auth-service";
import { createInterview } from "@/services/interview-service";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorState, LoadingState } from "@/components/ui/state-panel";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { RequireLoginState } from "@/components/auth/require-login-state";
import { useRequireAuth } from "@/hooks/use-require-auth";
import {
  buildCreateInterviewPayload,
  createSingleFlight,
  getInterviewTargetUrl,
} from "@/features/interview/direct-start";
import { getTagIconUrl } from "@/utils/icon-utils";
import { writeStoredAuth } from "@/utils/storage";
import { getRequestErrorMessage } from "@/utils/request-error";
import type { CurrentUser, PositionSummary } from "@/types/api";

export default function DashboardPage() {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const user = useAuthStore((state) => state.user);
  const setSession = useAuthStore((state) => state.setSession);
  const requireAuth = useRequireAuth();

  const [profile, setProfile] = useState<CurrentUser | null>(user);
  const [positions, setPositions] = useState<PositionSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [startError, setStartError] = useState<string | null>(null);
  const [startingPositionCode, setStartingPositionCode] = useState<string | null>(null);
  const startInterviewButtonClassName =
    "relative overflow-hidden transition-all duration-300 ease-out transform-gpu hover:-translate-y-0.5 hover:shadow-[0_16px_30px_rgba(17,24,39,0.18)] active:translate-y-0 active:scale-[0.97] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[rgba(17,24,39,0.28)] focus-visible:ring-offset-2 focus-visible:ring-offset-white motion-reduce:transform-none motion-reduce:transition-none before:pointer-events-none before:absolute before:inset-0 before:content-[''] before:bg-[linear-gradient(120deg,transparent,rgba(255,255,255,0.28),transparent)] before:translate-x-[-120%] before:opacity-0 before:transition-all before:duration-700 hover:before:translate-x-[120%] hover:before:opacity-100";

  const createInterviewOnceRef = useRef<
    ReturnType<typeof createSingleFlight<[string], string>> | null
  >(null);

  if (!createInterviewOnceRef.current) {
    createInterviewOnceRef.current = createSingleFlight(async (positionCode: string) => {
      setStartingPositionCode(positionCode);
      setStartError(null);
      try {
        const response = await createInterview(
          buildCreateInterviewPayload(positionCode, "standard"),
        );
        router.push(getInterviewTargetUrl(response.interviewId));
        return response.interviewId;
      } catch (requestError) {
        setStartError(getRequestErrorMessage(requestError, "创建面试失败"));
        throw requestError;
      } finally {
        setStartingPositionCode(null);
      }
    });
  }

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
        setLoadError(getRequestErrorMessage(requestError, "仪表盘加载失败"));
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin, setSession]);

  function handleStartPosition(positionCode: string) {
    void createInterviewOnceRef.current?.(positionCode).catch(() => undefined);
  }

  if (!hydrated) {
    return <LoadingState label="正在准备仪表盘..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在准备仪表盘..." />;
  }

  if (loadError) {
    return <ErrorState description={loadError} />;
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
              你可以直接选择岗位开始一场新的模拟面试，也可以进入历史趋势查看最近表现变化。
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            <Button
              className={startInterviewButtonClassName}
              onClick={() => router.push("/interview")}
            >
              开始模拟面试
            </Button>
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

      {startError ? <ErrorState description={startError} /> : null}

      <section className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
        {positions.map((position) => (
          <Card
            className="flex h-full flex-col justify-between gap-5 transition-all duration-300 ease-out hover:-translate-y-1 hover:shadow-[0_18px_45px_rgba(17,24,39,0.12)] hover:border-[rgba(17,24,39,0.12)]"
            key={position.code}
          >
            <div className="space-y-3">
              <span className="section-label">{position.code}</span>
              <h3 className="section-title">{position.name}</h3>
              <p className="text-caption">{position.description}</p>
            </div>
            <div className="space-y-4">
              <div className="flex flex-wrap gap-2">
                {position.tags.map((tag) => {
                  const iconUrl = getTagIconUrl(tag);
                  return (
                    <span
                      className="flex items-center gap-1.5 rounded-full bg-[rgba(17,24,39,0.05)] px-3 py-2 text-xs font-semibold"
                      key={tag}
                    >
                      {iconUrl ? (
                        <img
                          alt=""
                          className="h-4 w-4 shrink-0 object-contain"
                          src={iconUrl}
                        />
                      ) : null}
                      {tag}
                    </span>
                  );
                })}
              </div>
              <div className="flex items-center justify-between">
                <p className="text-caption">题量 {position.questionCount}</p>
                <Button
                  className={startInterviewButtonClassName}
                  disabled={Boolean(startingPositionCode)}
                  onClick={() =>
                    requireAuth({
                      onAuthed: () => handleStartPosition(position.code),
                    })
                  }
                  type="button"
                >
                  {startingPositionCode === position.code ? "创建中..." : "开始面试"}
                </Button>
              </div>
            </div>
          </Card>
        ))}
      </section>
    </div>
  );
}
