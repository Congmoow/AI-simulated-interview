"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { getPositions } from "@/services/catalog-service";
import {
  createInterview,
  finishInterview,
  getInterview,
  submitAnswer,
} from "@/services/interview-service";
import { createInterviewHub, stopInterviewHub } from "@/services/interview-hub";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/state-panel";
import { cn } from "@/lib/cn";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { RequireLoginState } from "@/components/auth/require-login-state";
import { useRequireAuth } from "@/hooks/use-require-auth";
import {
  buildCreateInterviewPayload,
  createSingleFlight,
  getInterviewEntryMode,
  getInterviewTargetUrl,
} from "@/features/interview/direct-start";
import { getTagIconUrl } from "@/utils/icon-utils";
import { getRequestErrorMessage } from "@/utils/request-error";
import type { InterviewCurrentDetail, PositionSummary } from "@/types/api";

const INTERVIEW_MODE_OPTIONS = [
  {
    value: "friendly",
    label: "轻松",
    dotClassName: "bg-emerald-400",
  },
  {
    value: "standard",
    label: "标准",
    dotClassName: "bg-slate-400",
  },
  {
    value: "stress",
    label: "高压",
    dotClassName: "bg-rose-400",
  },
] as const;

export function InterviewClient() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const requireAuth = useRequireAuth();
  const interviewId = searchParams.get("interviewId");
  const positionFromQuery = searchParams.get("positionCode");
  const legacyInterviewMode = searchParams.get("interviewMode") ?? "standard";

  const [positions, setPositions] = useState<PositionSummary[]>([]);
  const [selectedPosition, setSelectedPosition] = useState(positionFromQuery ?? "");
  const [detail, setDetail] = useState<InterviewCurrentDetail | null>(null);
  const [answerText, setAnswerText] = useState("");
  const [interviewMode, setInterviewMode] = useState("standard");
  const [loading, setLoading] = useState(true);
  const [startingPositionCode, setStartingPositionCode] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const interviewModeIndex = Math.max(
    0,
    INTERVIEW_MODE_OPTIONS.findIndex((option) => option.value === interviewMode),
  );
  const interviewModeSliderStyle = {
    width: `calc((100% - 0.5rem) / ${INTERVIEW_MODE_OPTIONS.length})`,
    transform: `translateX(${interviewModeIndex * 100}%)`,
  };

  const startInterviewButtonClassName =
    "relative overflow-hidden transition-all duration-300 ease-out transform-gpu hover:-translate-y-0.5 hover:shadow-[0_16px_30px_rgba(17,24,39,0.18)] active:translate-y-0 active:scale-[0.97] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[rgba(17,24,39,0.28)] focus-visible:ring-offset-2 focus-visible:ring-offset-white motion-reduce:transform-none motion-reduce:transition-none before:pointer-events-none before:absolute before:inset-0 before:content-[''] before:bg-[linear-gradient(120deg,transparent,rgba(255,255,255,0.28),transparent)] before:translate-x-[-120%] before:opacity-0 before:transition-all before:duration-700 hover:before:translate-x-[120%] hover:before:opacity-100";

  const autoCreateAttemptRef = useRef<string | null>(null);
  const createInterviewOnceRef = useRef<
    ReturnType<typeof createSingleFlight<[string, string], string>> | null
  >(null);

  const entryMode = getInterviewEntryMode({
    interviewId,
    positionCode: positionFromQuery,
  });
  const showPositionCards = entryMode === "choose-position";

  const latestRound = useMemo(
    () => detail?.rounds[detail.rounds.length - 1] ?? null,
    [detail],
  );

  if (!createInterviewOnceRef.current) {
    createInterviewOnceRef.current = createSingleFlight(
      async (positionCode: string, mode: string) => {
        setStartingPositionCode(positionCode);
        setError(null);
        try {
          const response = await createInterview(
            buildCreateInterviewPayload(positionCode, mode),
          );
          router.push(getInterviewTargetUrl(response.interviewId));
          return response.interviewId;
        } catch (requestError) {
          setError(getRequestErrorMessage(requestError, "创建面试失败"));
          throw requestError;
        } finally {
          setStartingPositionCode(null);
        }
      },
    );
  }

  const navigateTo = useCallback(
    (targetUrl: string) => {
      if (typeof window !== "undefined") {
        window.location.assign(targetUrl);
        return;
      }

      router.replace(targetUrl);
    },
    [router],
  );

  const startPositionInterview = useCallback(
    (positionCode: string, mode = interviewMode) => {
      if (!positionCode) {
        setError("请先选择岗位");
        return;
      }

      void createInterviewOnceRef.current?.(positionCode, mode).catch(() => undefined);
    },
    [interviewMode],
  );

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!accessToken) {
      const targetUrl =
        typeof window !== "undefined"
          ? `${window.location.pathname}${window.location.search}`
          : "/interview";
      openLogin({ type: "navigate", target: targetUrl });
      return;
    }

    void (async () => {
      try {
        const positionList = await getPositions();
        setPositions(positionList);
        setSelectedPosition((current) => current || positionList[0]?.code || "");
      } catch (requestError) {
        setError(getRequestErrorMessage(requestError, "岗位加载失败"));
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin]);

  useEffect(() => {
    if (!interviewId) {
      setDetail(null);
      return;
    }

    void refreshInterview(interviewId);
  }, [interviewId]);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!interviewId || !accessToken) {
      return;
    }

    const connection = createInterviewHub(accessToken);
    connection.on("ReceiveQuestion", () => void refreshInterview(interviewId));
    connection.on("ReceiveFollowUp", () => void refreshInterview(interviewId));
    connection.on("InterviewStatusChanged", () => void refreshInterview(interviewId));
    connection.on("ReportReady", () => navigateTo(`/report/${interviewId}`));
    connection.on("TypingIndicator", () => undefined);
    connection.on("typingindicator", () => undefined);
    connection.on("ReportProgress", () => undefined);
    connection.on("reportprogress", () => undefined);

    void (async () => {
      try {
        await connection.start();
        await connection.invoke("JoinInterview", { interviewId });
      } catch {
        return;
      }
    })();

    return () => {
      void stopInterviewHub(connection);
    };
  }, [accessToken, hydrated, interviewId, navigateTo]);

  useEffect(() => {
    if (!hydrated || !accessToken) {
      return;
    }

    if (entryMode !== "auto-create" || !positionFromQuery) {
      autoCreateAttemptRef.current = null;
      return;
    }

    if (autoCreateAttemptRef.current === positionFromQuery) {
      return;
    }

    autoCreateAttemptRef.current = positionFromQuery;
    startPositionInterview(positionFromQuery, legacyInterviewMode);
  }, [
    accessToken,
    entryMode,
    hydrated,
    legacyInterviewMode,
    positionFromQuery,
    startPositionInterview,
  ]);

  async function refreshInterview(targetInterviewId: string) {
    try {
      const response = await getInterview(targetInterviewId);
      setDetail(response);
      setSelectedPosition(response.positionCode);
      setInterviewMode(response.interviewMode);
      setError(null);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "面试详情加载失败"));
    }
  }

  async function handleSubmitAnswer() {
    if (!interviewId || !answerText.trim()) {
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await submitAnswer(interviewId, {
        answer: answerText,
        inputMode: "text",
      });
      setAnswerText("");
      await refreshInterview(interviewId);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "提交回答失败"));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleFinishInterview() {
    if (!interviewId) {
      return;
    }

    setSubmitting(true);
    try {
      const response = await finishInterview(interviewId);
      navigateTo(`/report/${response.interviewId}`);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "结束面试失败"));
    } finally {
      setSubmitting(false);
    }
  }

  if (!hydrated) {
    return <LoadingState label="正在初始化面试环境..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在初始化面试环境..." />;
  }

  if (entryMode === "auto-create" && !interviewId) {
    return (
      <div className="space-y-4">
        {error ? (
          <>
            <ErrorState description={error} />
            <Button
              disabled={Boolean(startingPositionCode)}
              onClick={() => {
                if (!positionFromQuery) {
                  return;
                }
                autoCreateAttemptRef.current = null;
                startPositionInterview(positionFromQuery, legacyInterviewMode);
              }}
              type="button"
            >
              {startingPositionCode ? "正在创建..." : "重试创建面试"}
            </Button>
          </>
        ) : (
          <LoadingState label="正在创建面试..." />
        )}
      </div>
    );
  }

  if (showPositionCards) {
    return (
      <div className="space-y-6">
        <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between md:gap-6">
          <div className="space-y-3">
            <span className="section-label">模拟面试</span>
            <h2 className="display-title !text-[clamp(2rem,3vw,3.4rem)]">
              选择一个岗位，直接进入正式面试
            </h2>
            <p className="text-caption max-w-[720px] text-[length:var(--token-font-size-lg)]">
              点击岗位卡片后会直接创建面试并进入正式问答页，不再经过旧的确认中间页。
            </p>
          </div>
          <div className="w-full shrink-0 md:w-[320px]">
            <div className="mb-2 flex flex-col items-end gap-0.5 text-right">
              <p className="text-[10px] font-semibold uppercase tracking-[0.24em] text-[var(--token-color-primary)]">
                面试难度
              </p>
              <p className="text-[11px] text-[var(--token-color-text-secondary)]">节奏与压力</p>
            </div>
            <div className="relative overflow-hidden rounded-full bg-[rgba(17,24,39,0.07)] p-1">
              <div
                aria-hidden="true"
                className="absolute inset-y-1 left-1 rounded-full transition-transform duration-300 ease-out motion-reduce:transition-none before:absolute before:inset-y-0 before:left-[8px] before:right-[8px] before:rounded-full before:border before:border-[var(--token-color-border-default)] before:bg-white before:shadow-[0_4px_10px_rgba(17,24,39,0.06)] before:content-['']"
                style={interviewModeSliderStyle}
              />
              <div className="relative grid grid-cols-3">
                {INTERVIEW_MODE_OPTIONS.map((option) => {
                  const active = interviewMode === option.value;
                  return (
                    <button
                      aria-pressed={active}
                      className={cn(
                        "group relative z-10 inline-flex min-h-[30px] w-full items-center justify-center gap-1.5 rounded-full px-2 py-1 text-left transition-colors duration-300 ease-out focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[rgba(17,24,39,0.16)] focus-visible:ring-offset-2 focus-visible:ring-offset-white motion-reduce:transition-none",
                        active
                          ? "text-[var(--token-color-text-primary)]"
                          : "text-[var(--token-color-text-secondary)] hover:text-[var(--token-color-text-primary)]",
                      )}
                      key={option.value}
                      onClick={() => setInterviewMode(option.value)}
                      type="button"
                    >
                      <span
                        className={cn(
                          "h-2 w-2 rounded-full transition-all duration-300",
                          active ? "bg-[var(--token-color-primary)]" : option.dotClassName,
                        )}
                      />
                      <span className="text-[12px] font-semibold leading-tight">{option.label}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          </div>
        </div>
        {error ? <ErrorState description={error} /> : null}
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
                        onAuthed: () =>
                          startPositionInterview(position.code, interviewMode),
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

  return (
    <div className="grid gap-6 xl:grid-cols-[0.85fr_1.15fr]">
      <Card className="space-y-5">
        <span className="section-label">面试配置</span>
        <div className="space-y-4">
          <div>
            <label className="mb-2 block text-sm font-semibold">岗位</label>
            <select
              className="input-shell"
              disabled={Boolean(interviewId)}
              onChange={(event) => setSelectedPosition(event.target.value)}
              value={selectedPosition}
            >
              {positions.map((position) => (
                <option key={position.code} value={position.code}>
                  {position.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-2 block text-sm font-semibold">模式</label>
            <select
              className="input-shell"
              disabled={Boolean(interviewId)}
              onChange={(event) => setInterviewMode(event.target.value)}
              value={interviewMode}
            >
              <option value="friendly">friendly</option>
              <option value="standard">standard</option>
              <option value="stress">stress</option>
            </select>
          </div>
          {interviewId ? (
            <Button
              disabled={submitting}
              onClick={handleFinishInterview}
              type="button"
              variant="secondary"
            >
              主动结束面试
            </Button>
          ) : null}
        </div>
        {detail ? (
          <div className="surface-muted space-y-3 p-4">
            <p className="section-label">当前状态</p>
            <p className="text-lg font-semibold">
              第 {detail.currentRound} / {detail.totalRounds} 轮
            </p>
            <p className="text-caption">状态：{detail.status}</p>
          </div>
        ) : null}
      </Card>
      <div className="space-y-6">
        {error ? <ErrorState description={error} /> : null}
        {!interviewId ? (
          <EmptyState
            description="请先从岗位列表发起面试，创建成功后会直接进入正式问答页。"
            title="还没有进行中的面试"
          />
        ) : detail && latestRound ? (
          <>
            <Card className="space-y-5">
              <span className="section-label">当前题目</span>
              <div className="space-y-3">
                <h2 className="section-title">{latestRound.question.title}</h2>
                <p className="text-caption">
                  题型：{latestRound.question.type} · 轮次 {latestRound.roundNumber}
                </p>
                {latestRound.aiFollowUp ? (
                  <div className="rounded-[var(--token-radius-xl)] bg-[rgba(139,92,246,0.1)] p-4 text-sm">
                    追问：{latestRound.aiFollowUp}
                  </div>
                ) : null}
              </div>
              <textarea
                className="input-shell min-h-[180px]"
                onChange={(event) => setAnswerText(event.target.value)}
                placeholder="请输入你的回答。当前版本先走文本回答链路。"
                value={answerText}
              />
              <div className="flex flex-wrap gap-3">
                <Button
                  disabled={submitting || !answerText.trim()}
                  onClick={handleSubmitAnswer}
                  type="button"
                >
                  {submitting ? "提交中..." : "提交回答"}
                </Button>
                <Button
                  disabled={submitting}
                  onClick={handleFinishInterview}
                  type="button"
                  variant="secondary"
                >
                  结束并生成报告
                </Button>
              </div>
            </Card>
            <Card className="space-y-5">
              <span className="section-label">问答记录</span>
              <div className="space-y-4">
                {detail.rounds.map((round) => (
                  <div className="surface-muted space-y-3 p-4" key={round.roundNumber}>
                    <div className="flex items-center justify-between">
                      <p className="font-semibold">
                        第 {round.roundNumber} 轮 · {round.question.type}
                      </p>
                      <span className="text-caption">
                        {round.answeredAt ? "已回答" : "待回答"}
                      </span>
                    </div>
                    <p>{round.question.title}</p>
                    <p className="text-caption">
                      {round.userAnswer ?? "尚未提交回答"}
                    </p>
                    {round.aiFollowUp ? (
                      <p className="rounded-[var(--token-radius-lg)] bg-[rgba(139,92,246,0.08)] px-4 py-3 text-sm">
                        AI 追问：{round.aiFollowUp}
                      </p>
                    ) : null}
                  </div>
                ))}
              </div>
            </Card>
          </>
        ) : (
          <LoadingState label="正在同步当前面试详情..." />
        )}
      </div>
    </div>
  );
}
