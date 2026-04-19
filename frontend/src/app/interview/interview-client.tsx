"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";

import { getPositions } from "@/services/catalog-service";
import { createInterview, finishInterview, getInterview, submitAnswer } from "@/services/interview-service";
import { createInterviewHub, stopInterviewHub } from "@/services/interview-hub";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/state-panel";
import { InterviewComposer } from "@/components/interview/interview-composer";
import { InterviewMessageList } from "@/components/interview/interview-message-list";
import { type InterviewTimelineMessage, type InterviewUserMessageStatus } from "@/components/interview/interview-message-item";
import { type InterviewSystemAction, type InterviewSystemTone } from "@/components/interview/interview-system-notice";
import { InterviewModeSwitch } from "@/components/interview/interview-mode-switch";
import { InterviewTopBar } from "@/components/interview/interview-top-bar";
import { RequireLoginState } from "@/components/auth/require-login-state";
import { useRequireAuth } from "@/hooks/use-require-auth";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import {
  buildCreateInterviewPayload,
  createSingleFlight,
  getInterviewEntryMode,
  getInterviewTargetUrl,
} from "@/features/interview/direct-start";
import {
  buildInterviewTimelineMessages,
  hasPersistedPendingAnswer,
} from "@/features/interview/message-flow";
import { shouldAdvanceElapsedTimer } from "@/features/interview/interview-timer";
import { buildRealtimeInterviewMessages } from "@/features/interview/realtime-message-flow";
import { getTagIconUrl } from "@/utils/icon-utils";
import { getRequestErrorMessage } from "@/utils/request-error";
import type { InterviewCurrentDetail, PositionSummary } from "@/types/api";

const INTERVIEW_MODE_OPTIONS = [
  { value: "friendly", label: "轻松" },
  { value: "standard", label: "标准" },
  { value: "stress", label: "高压" },
] as const;

const REPORT_STAGE_LABELS: Record<string, string> = {
  ended: "正在生成报告",
  scoring: "正在评估本场表现",
  reporting: "正在生成面试报告",
  saving: "正在保存报告结果",
  completed: "报告已生成，点击查看",
};

const DRAFT_STORAGE_PREFIX = "interview-draft:";

type LocalPendingAnswer = {
  id: string;
  roundNumber: number;
  text: string;
  timestamp: string;
  status: InterviewUserMessageStatus;
};

type SystemTimelineMessage = Extract<InterviewTimelineMessage, { kind: "system" }>;

function formatShortTime(value: string | number | Date) {
  return new Date(value).toLocaleTimeString("zh-CN", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatElapsed(createdAt: string, now: number) {
  const diffInSeconds = Math.max(
    0,
    Math.floor((now - new Date(createdAt).getTime()) / 1000),
  );
  const hours = Math.floor(diffInSeconds / 3600);
  const minutes = Math.floor((diffInSeconds % 3600) / 60);
  const seconds = diffInSeconds % 60;

  if (hours > 0) {
    return `${hours.toString().padStart(2, "0")}:${minutes
      .toString()
      .padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
  }

  return `${minutes.toString().padStart(2, "0")}:${seconds
    .toString()
    .padStart(2, "0")}`;
}

function createSystemMessage(
  id: string,
  body: string,
  options?: {
    tone?: InterviewSystemTone;
    displayStyle?: "card" | "plain" | "inline";
    actionKey?: InterviewSystemAction;
    actionLabel?: string;
  },
): SystemTimelineMessage {
  return {
    id,
    kind: "system",
    body,
    tone: options?.tone ?? "default",
    displayStyle: options?.displayStyle ?? "card",
    actionKey: options?.actionKey,
    actionLabel: options?.actionLabel,
  };
}

function getReportProgressMessage(progress: {
  progress: number;
  stage: string;
  estimatedTime: number;
}) {
  const stageLabel = REPORT_STAGE_LABELS[progress.stage] ?? REPORT_STAGE_LABELS.reporting;
  if (progress.stage === "completed") {
    return stageLabel;
  }

  if (progress.estimatedTime > 0) {
    return `${stageLabel} · ${progress.progress}% · 预计剩余 ${progress.estimatedTime} 秒`;
  }

  return `${stageLabel} · ${progress.progress}%`;
}

function isUnauthorizedSignalRError(error: unknown) {
  return (
    error instanceof Error &&
    (error.message.toLowerCase().includes("401") ||
      error.message.toLowerCase().includes("unauthorized"))
  );
}

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
  const [detail, setDetail] = useState<InterviewCurrentDetail | null>(null);
  const [answerText, setAnswerText] = useState("");
  const [interviewMode, setInterviewMode] = useState("standard");
  const [loading, setLoading] = useState(true);
  const [startingPositionCode, setStartingPositionCode] = useState<string | null>(null);
  const [submittingAnswer, setSubmittingAnswer] = useState(false);
  const [finishingInterview, setFinishingInterview] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reportProgress, setReportProgress] = useState<{
    progress: number;
    stage: string;
    estimatedTime: number;
  } | null>(null);
  const [connectionNotices, setConnectionNotices] = useState<SystemTimelineMessage[]>([]);
  const [pendingAnswer, setPendingAnswer] = useState<LocalPendingAnswer | null>(null);
  const [assistantThinking, setAssistantThinking] = useState(false);
  const [reportReady, setReportReady] = useState(false);
  const [storedDraft, setStoredDraft] = useState<string | null>(null);
  const [draftSavedAt, setDraftSavedAt] = useState<number | null>(null);
  const [draftRecoveredAt, setDraftRecoveredAt] = useState<number | null>(null);
  const [nowTick, setNowTick] = useState(() => Date.now());

  const autoCreateAttemptRef = useRef<string | null>(null);
  const draftOwnedByEditorRef = useRef(false);
  const createInterviewOnceRef = useRef<
    ReturnType<typeof createSingleFlight<[string, string], string>> | null
  >(null);

  const entryMode = getInterviewEntryMode({
    interviewId,
    positionCode: positionFromQuery,
  });
  const showPositionCards = entryMode === "choose-position";
  const currentMainRoundNumber = detail?.currentRound ?? 0;
  const isReportGenerating = detail?.status === "generating_report";
  const isReportFailed = detail?.status === "report_failed";
  const isCompleted = detail?.status === "completed" || reportReady;
  const draftStorageKey = interviewId ? `${DRAFT_STORAGE_PREFIX}${interviewId}` : null;

  const refreshInterview = useCallback(async (targetInterviewId: string) => {
    try {
      const response = await getInterview(targetInterviewId);
      setDetail(response);
      setInterviewMode(response.interviewMode);
      setReportReady(response.status === "completed");
      setReportProgress(
        response.status === "generating_report"
          ? { progress: 10, stage: "ended", estimatedTime: 30 }
          : response.status === "completed"
            ? { progress: 100, stage: "completed", estimatedTime: 0 }
            : null,
      );
      if (response.status !== "in_progress") {
        setAssistantThinking(false);
      }
      setError(null);
    } catch (requestError) {
      setAssistantThinking(false);
      setError(getRequestErrorMessage(requestError, "面试详情加载失败"));
    }
  }, []);

  if (!createInterviewOnceRef.current) {
    createInterviewOnceRef.current = createSingleFlight(
      async (positionCode: string, mode: string) => {
        setStartingPositionCode(positionCode);
        try {
          const response = await createInterview(
            buildCreateInterviewPayload(positionCode, mode),
          );
          router.push(getInterviewTargetUrl(response.interviewId));
          return response.interviewId;
        } finally {
          setStartingPositionCode(null);
        }
      },
    );
  }

  const persistDraft = useCallback(
    (nextValue: string) => {
      if (!draftStorageKey || typeof window === "undefined") {
        return;
      }

      if (!nextValue.trim()) {
        if (draftOwnedByEditorRef.current) {
          window.localStorage.removeItem(draftStorageKey);
          setStoredDraft(null);
          setDraftSavedAt(null);
        }
        return;
      }

      window.localStorage.setItem(draftStorageKey, nextValue);
      draftOwnedByEditorRef.current = true;
      setStoredDraft(nextValue);
      setDraftSavedAt(Date.now());
    },
    [draftStorageKey],
  );

  const startPositionInterview = useCallback(
    (positionCode: string, mode = interviewMode) => {
      if (!positionCode) {
        setError("请先选择岗位");
        return;
      }

      void createInterviewOnceRef.current?.(positionCode, mode).catch(
        (requestError) => {
          setError(getRequestErrorMessage(requestError, "创建面试失败"));
        },
      );
    },
    [interviewMode],
  );

  const clearDraft = useCallback(() => {
    if (draftStorageKey && typeof window !== "undefined") {
      window.localStorage.removeItem(draftStorageKey);
    }
    draftOwnedByEditorRef.current = false;
    setStoredDraft(null);
    setDraftSavedAt(null);
    setDraftRecoveredAt(null);
  }, [draftStorageKey]);

  const appendConnectionNotice = useCallback(
    (body: string, tone: InterviewSystemTone) => {
      setConnectionNotices((current) =>
        [
          ...current,
          createSystemMessage(`connection-${Date.now()}`, body, {
            tone,
            displayStyle: "plain",
          }),
        ].slice(-6),
      );
    },
    [],
  );

  const handleSubmitAnswer = useCallback(async () => {
    if (!interviewId || !detail || !currentMainRoundNumber || !answerText.trim()) {
      return;
    }

    const answer = answerText.trim();
    const pendingId = `pending-answer-${Date.now()}`;
    setPendingAnswer({
      id: pendingId,
      roundNumber: currentMainRoundNumber,
      text: answer,
      timestamp: new Date().toISOString(),
      status: "sent",
    });
    setAssistantThinking(false);
    setAnswerText("");
    clearDraft();
    setSubmittingAnswer(true);

    try {
      const response = await submitAnswer(interviewId, {
        answer,
        inputMode: "text",
      });
      setPendingAnswer((current) =>
        current && current.id === pendingId
          ? {
              ...current,
              status:
                response.aiResponse.type === "follow_up"
                  ? "followup"
                  : "evaluating",
            }
          : current,
      );
      await refreshInterview(interviewId);
    } catch (requestError) {
      setAssistantThinking(false);
      setPendingAnswer((current) =>
        current && current.id === pendingId
          ? { ...current, status: "failed" }
          : current,
      );
      setAnswerText(answer);
      persistDraft(answer);
      setError(getRequestErrorMessage(requestError, "提交回答失败"));
    } finally {
      setSubmittingAnswer(false);
    }
  }, [
    answerText,
    clearDraft,
    currentMainRoundNumber,
    detail,
    interviewId,
    persistDraft,
    refreshInterview,
  ]);

  const handleFinishInterview = useCallback(async () => {
    if (!interviewId) {
      return;
    }

    setFinishingInterview(true);
    try {
      const response = await finishInterview(interviewId);
      setReportReady(response.status === "completed");
      setReportProgress(
        response.status === "completed"
          ? { progress: 100, stage: "completed", estimatedTime: 0 }
          : { progress: 10, stage: "ended", estimatedTime: response.estimatedTime },
      );
      await refreshInterview(interviewId);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "结束面试失败"));
    } finally {
      setFinishingInterview(false);
    }
  }, [interviewId, refreshInterview]);

  const handleMessageAction = useCallback(
    (action: InterviewSystemAction) => {
      if (!interviewId) {
        return;
      }

      if (action === "view-report") {
        router.push(`/reports/${interviewId}`);
        return;
      }

      if (action === "retry-report") {
        void handleFinishInterview();
      }
    },
    [handleFinishInterview, interviewId, router],
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

    void getPositions()
      .then(setPositions)
      .catch((requestError) => {
        setError(getRequestErrorMessage(requestError, "岗位加载失败"));
      })
      .finally(() => setLoading(false));
  }, [accessToken, hydrated, openLogin]);

  useEffect(() => {
    if (!interviewId) {
      setDetail(null);
      return;
    }
    void refreshInterview(interviewId);
  }, [interviewId, refreshInterview]);

  useEffect(() => {
    if (!interviewId || typeof window === "undefined") {
      return;
    }
    const savedDraft = window.localStorage.getItem(
      `${DRAFT_STORAGE_PREFIX}${interviewId}`,
    );
    if (savedDraft?.trim()) {
      setStoredDraft(savedDraft);
    }
  }, [interviewId]);

  useEffect(() => {
    if (!detail || !shouldAdvanceElapsedTimer(detail.status)) {
      return;
    }
    const timer = window.setInterval(() => setNowTick(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [detail]);

  useEffect(() => {
    if (!pendingAnswer || !detail) {
      return;
    }
    if (hasPersistedPendingAnswer(detail, pendingAnswer.text)) {
      setPendingAnswer(null);
    }
  }, [detail, pendingAnswer]);

  useEffect(() => {
    if (!hydrated || !accessToken || entryMode !== "auto-create" || !positionFromQuery) {
      return;
    }
    if (autoCreateAttemptRef.current === positionFromQuery) {
      return;
    }
    autoCreateAttemptRef.current = positionFromQuery;
    void createInterviewOnceRef.current?.(
      positionFromQuery,
      legacyInterviewMode,
    ).catch((requestError) => {
      setError(getRequestErrorMessage(requestError, "创建面试失败"));
    });
  }, [accessToken, entryMode, hydrated, legacyInterviewMode, positionFromQuery]);

  useEffect(() => {
    if (!hydrated || !interviewId || !accessToken) {
      return;
    }

    let active = true;
    const connection = createInterviewHub(accessToken);
    const joinInterviewRoom = async () =>
      connection.invoke("JoinInterview", { interviewId });

    connection.on("ReceiveQuestion", () => {
      if (!active) {
        return;
      }
      setAssistantThinking(false);
      void refreshInterview(interviewId);
    });
    connection.on("ReceiveFollowUp", () => {
      if (!active) {
        return;
      }
      setAssistantThinking(false);
      setPendingAnswer((current) =>
        !current || current.status === "failed"
          ? current
          : { ...current, status: "followup" },
      );
      void refreshInterview(interviewId);
    });
    connection.on("InterviewStatusChanged", () => {
      if (!active) {
        return;
      }
      setAssistantThinking(false);
      void refreshInterview(interviewId);
    });
    connection.on("ReportReady", () => {
      if (!active) {
        return;
      }
      setAssistantThinking(false);
      setReportReady(true);
      setReportProgress({ progress: 100, stage: "completed", estimatedTime: 0 });
      void refreshInterview(interviewId);
    });
    connection.on("TypingIndicator", (payload?: unknown) => {
      if (
        !active ||
        (payload &&
          typeof payload === "object" &&
          "isTyping" in payload &&
          payload.isTyping === false)
      ) {
        setAssistantThinking(false);
        return;
      }
      setAssistantThinking(true);
      setPendingAnswer((current) =>
        !current || current.status === "failed" || current.status === "followup"
          ? current
          : { ...current, status: "evaluating" },
      );
    });
    connection.on("ReportProgress", (payload?: unknown) => {
      if (
        !active ||
        !payload ||
        typeof payload !== "object" ||
        typeof (payload as { progress?: number }).progress !== "number"
      ) {
        return;
      }
      const candidate = payload as {
        progress: number;
        stage?: string;
        estimatedTime?: number;
      };
      setReportProgress({
        progress: candidate.progress,
        stage: candidate.stage ?? "ended",
        estimatedTime:
          typeof candidate.estimatedTime === "number" ? candidate.estimatedTime : 0,
      });
    });
    connection.on("ErrorOccurred", (payload?: unknown) => {
      if (!active) {
        return;
      }
      setAssistantThinking(false);
      if (
        payload &&
        typeof payload === "object" &&
        "message" in payload &&
        typeof payload.message === "string"
      ) {
        setError(payload.message);
      }
      void refreshInterview(interviewId);
    });
    connection.onreconnecting(() => {
      if (!active) {
        return;
      }
      setAssistantThinking(false);
      appendConnectionNotice("网络波动，正在重试连接。", "warning");
    });
    connection.onreconnected(() => {
      if (!active) {
        return;
      }
      setAssistantThinking(false);
      appendConnectionNotice("连接已恢复。", "success");
      void joinInterviewRoom()
        .then(() => refreshInterview(interviewId))
        .catch((error) => {
          if (isUnauthorizedSignalRError(error)) {
            useAuthStore.getState().clearSession();
            openLogin({
              type: "navigate",
              target: `/interview?interviewId=${encodeURIComponent(interviewId)}`,
            });
          }
        });
    });
    connection.onclose(() => {
      if (!active) {
        return;
      }
      setAssistantThinking(false);
      appendConnectionNotice("连接已断开，请稍后重试。", "danger");
    });
    void connection.start().then(joinInterviewRoom).catch((error) => {
      if (!active) {
        return;
      }
      if (isUnauthorizedSignalRError(error)) {
        useAuthStore.getState().clearSession();
        openLogin({
          type: "navigate",
          target: `/interview?interviewId=${encodeURIComponent(interviewId)}`,
        });
        return;
      }
      appendConnectionNotice("暂时无法建立实时连接。", "warning");
    });

    return () => {
      active = false;
      void stopInterviewHub(connection);
    };
  }, [accessToken, appendConnectionNotice, hydrated, interviewId, openLogin, refreshInterview]);

  const pendingAnswerAlreadyPersisted = useMemo(
    () =>
      !pendingAnswer || !detail
        ? false
        : hasPersistedPendingAnswer(detail, pendingAnswer.text),
    [detail, pendingAnswer],
  );
  const baseMessages = useMemo(
    () => (detail ? buildInterviewTimelineMessages(detail) : []),
    [detail],
  );
  const tailMessages = useMemo(() => {
    const messages: InterviewTimelineMessage[] = [];

    messages.push(
      ...buildRealtimeInterviewMessages({
        pendingAnswer,
        pendingAnswerAlreadyPersisted,
        assistantThinking,
      }),
    );

    if (finishingInterview && detail?.status === "in_progress") {
      messages.push(
        createSystemMessage("interview-finishing", "正在结束面试，准备生成报告。", {
          displayStyle: "plain",
        }),
      );
    }
    if (detail && detail.status !== "in_progress" && detail.status !== "generating_report") {
      messages.push(
        createSystemMessage("interview-ended", "面试已结束，本场回答已封存。", {
          displayStyle: "plain",
        }),
      );
    }
    if (detail?.status === "report_failed") {
      messages.push(
        createSystemMessage("report-failed", "报告生成失败，请重新发起生成。", {
          tone: "danger",
          actionKey: "retry-report",
          actionLabel: "重新生成报告",
        }),
      );
    } else if (isCompleted) {
      messages.push(
        createSystemMessage("report-ready", "报告已生成", {
          tone: "success",
          displayStyle: "inline",
          actionKey: "view-report",
          actionLabel: "查看报告",
        }),
      );
    } else if (detail?.status === "generating_report" && reportProgress) {
      messages.push(
        createSystemMessage(
          `report-stage-${reportProgress.stage}`,
          getReportProgressMessage(reportProgress),
          {
            tone: reportProgress.stage === "ended" ? "warning" : "default",
            displayStyle: "plain",
          },
        ),
      );
    }

    return [...messages, ...connectionNotices];
  }, [
    connectionNotices,
    detail,
    finishingInterview,
    isCompleted,
    pendingAnswer,
    pendingAnswerAlreadyPersisted,
    reportProgress,
    assistantThinking,
  ]);
  const messages = useMemo(() => [...baseMessages, ...tailMessages], [baseMessages, tailMessages]);
  const elapsedLabel = useMemo(
    () => (detail ? formatElapsed(detail.createdAt, nowTick) : "00:00"),
    [detail, nowTick],
  );
  const draftLabel = useMemo(() => {
    if (draftRecoveredAt) {
      return `已恢复草稿 · ${formatShortTime(draftRecoveredAt)}`;
    }
    if (draftSavedAt) {
      return `草稿已保存 · ${formatShortTime(draftSavedAt)}`;
    }
    if (storedDraft && !answerText) {
      return "检测到上次未发送草稿";
    }
    return undefined;
  }, [answerText, draftRecoveredAt, draftSavedAt, storedDraft]);
  const finishLabel = useMemo(() => {
    if (finishingInterview) {
      return "处理中...";
    }
    if (isReportFailed) {
      return "重新生成报告";
    }
    if (isReportGenerating) {
      return "生成中";
    }
    if (isCompleted) {
      return "已结束";
    }
    return "结束面试";
  }, [finishingInterview, isCompleted, isReportFailed, isReportGenerating]);
  const finishDisabled =
    !interviewId ||
    submittingAnswer ||
    finishingInterview ||
    isReportGenerating ||
    isCompleted;
  const canSubmit = Boolean(
    interviewId &&
      detail &&
      answerText.trim() &&
      !submittingAnswer &&
      !finishingInterview &&
      detail.status === "in_progress",
  );
  const composerPlaceholder = isCompleted
    ? "面试已结束，报告已生成。可通过消息流中的按钮查看完整报告。"
    : isReportFailed
      ? "面试已结束，报告生成失败。可点击“重新生成报告”继续处理。"
      : isReportGenerating
        ? "面试已结束，报告正在生成中。"
        : "请在这里输入你的回答，支持长段落、多点拆解与结构化表达。";
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
    return error ? (
      <div className="space-y-4">
        <ErrorState description={error} />
        <Button
          disabled={Boolean(startingPositionCode)}
          onClick={() =>
            positionFromQuery &&
            void createInterviewOnceRef.current?.(positionFromQuery, legacyInterviewMode)
          }
          type="button"
        >
          {startingPositionCode ? "正在创建..." : "重新创建面试"}
        </Button>
      </div>
    ) : (
      <LoadingState label="正在创建面试..." />
    );
  }

  if (showPositionCards) {
    return (
      <div className="space-y-6">
        <div className="space-y-3">
          <span className="section-label">模拟面试</span>
          <h2 className="display-title !text-[clamp(2rem,3vw,3.4rem)]">
            选择一个岗位，直接进入正式面试
          </h2>
          <p className="text-caption max-w-[720px] text-[length:var(--token-font-size-lg)]">
            点击岗位卡片后会立即创建面试，并进入新的聊天式面试工作台。
          </p>
          <InterviewModeSwitch
            onChange={setInterviewMode}
            options={INTERVIEW_MODE_OPTIONS.map((option) => ({
              label: option.label,
              value: option.value,
            }))}
            value={interviewMode}
          />
        </div>
        {error ? <ErrorState description={error} /> : null}
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
                  {position.tags.map((tag) => {
                    const iconUrl = getTagIconUrl(tag);
                    return (
                      <span
                        className="flex items-center gap-1.5 rounded-full bg-[rgba(17,24,39,0.05)] px-3 py-2 text-xs font-semibold"
                        key={tag}
                      >
                        {iconUrl ? (
                          <span
                            aria-hidden="true"
                            className="h-4 w-4 shrink-0 bg-contain bg-center bg-no-repeat"
                            style={{ backgroundImage: `url(${iconUrl})` }}
                          />
                        ) : null}
                        {tag}
                      </span>
                    );
                  })}
                </div>
                <div className="flex items-center justify-end">
                  <Button
                    disabled={Boolean(startingPositionCode)}
                    onClick={() =>
                      requireAuth({
                        onAuthed: () => startPositionInterview(position.code, interviewMode),
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

  if (!interviewId) {
    return (
      <EmptyState
        description="请先从岗位列表发起面试，创建成功后会直接进入聊天式面试工作台。"
        title="还没有进行中的面试"
      />
    );
  }
  if (!detail) {
    return <LoadingState label="正在同步当前面试详情..." />;
  }

  return (
    <div className="flex h-full min-h-0 flex-col overflow-hidden">
      <InterviewTopBar
        elapsedLabel={elapsedLabel}
        finishDisabled={finishDisabled}
        finishLabel={finishLabel}
        onFinish={() => void handleFinishInterview()}
        positionName={detail.positionName}
      />
      <div className="shrink-0 px-4 pt-4">{error ? <ErrorState description={error} /> : null}</div>
      <InterviewMessageList messages={messages} onAction={handleMessageAction} />
      <InterviewComposer
        canRestoreDraft={Boolean(storedDraft && !answerText)}
        canSubmit={canSubmit}
        disabled={detail.status !== "in_progress"}
        draftLabel={draftLabel}
        hintText={undefined}
        onChange={(nextValue) => {
          setAnswerText(nextValue);
          persistDraft(nextValue);
        }}
        onKeyDown={(event) => {
          if (
            event.key === "Enter" &&
            !event.shiftKey &&
            (event.metaKey || event.ctrlKey)
          ) {
            event.preventDefault();
            if (canSubmit) {
              void handleSubmitAnswer();
            }
          }
        }}
        onRestoreDraft={() => {
          if (storedDraft) {
            setAnswerText(storedDraft);
            setDraftRecoveredAt(Date.now());
            draftOwnedByEditorRef.current = true;
          }
        }}
        onSubmit={() => void handleSubmitAnswer()}
        placeholder={composerPlaceholder}
        sendLabel={submittingAnswer ? "发送中..." : "发送回答"}
        statusText={undefined}
        value={answerText}
      />
    </div>
  );
}
