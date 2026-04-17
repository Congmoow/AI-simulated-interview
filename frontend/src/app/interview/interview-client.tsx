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
import { InterviewTopBar } from "@/components/interview/interview-top-bar";
import { cn } from "@/lib/cn";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { RequireLoginState } from "@/components/auth/require-login-state";
import { useRequireAuth } from "@/hooks/use-require-auth";
import { buildCreateInterviewPayload, createSingleFlight, getInterviewEntryMode, getInterviewTargetUrl } from "@/features/interview/direct-start";
import { getTagIconUrl } from "@/utils/icon-utils";
import { getRequestErrorMessage } from "@/utils/request-error";
import { writeStoredAuth } from "@/utils/storage";
import type { InterviewCurrentDetail, PositionSummary } from "@/types/api";

const INTERVIEW_MODE_OPTIONS = [
  { value: "friendly", label: "轻松", dotClassName: "bg-emerald-400" },
  { value: "standard", label: "标准", dotClassName: "bg-slate-400" },
  { value: "stress", label: "高压", dotClassName: "bg-rose-400" },
] as const;

const QUESTION_TYPE_LABELS: Record<string, string> = {
  technical: "技术题",
  project: "项目题",
  scenario: "场景题",
  behavioral: "行为题",
};

const REPORT_STAGE_LABELS: Record<string, string> = {
  ended: "正在生成报告",
  scoring: "正在评估本场表现",
  reporting: "正在生成面试报告",
  saving: "正在保存报告结果",
  completed: "报告已生成，点击查看",
};

const DRAFT_STORAGE_PREFIX = "interview-draft:";

type InterviewConnectionStatus = "connected" | "reconnecting" | "disconnected";

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
  const diffInSeconds = Math.max(0, Math.floor((now - new Date(createdAt).getTime()) / 1000));
  const hours = Math.floor(diffInSeconds / 3600);
  const minutes = Math.floor((diffInSeconds % 3600) / 60);
  const seconds = diffInSeconds % 60;

  if (hours > 0) {
    return `${hours.toString().padStart(2, "0")}:${minutes.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
  }

  return `${minutes.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
}

/* function getConnectionLabel(status: InterviewConnectionStatus) {
  if (status === "connected") {
    return "连接稳定";
  }

  if (status === "reconnecting") {
    return "重连中";
  }

  return "已断开";
}
*/

function createSystemMessage(
  id: string,
  body: string,
  options?: {
    tone?: InterviewSystemTone;
    actionLabel?: string;
    actionKey?: InterviewSystemAction;
  },
): SystemTimelineMessage {
  return {
    id,
    kind: "system",
    body,
    tone: options?.tone ?? "default",
    actionLabel: options?.actionLabel,
    actionKey: options?.actionKey,
  };
}

function buildRoundMessages(detail: InterviewCurrentDetail): InterviewTimelineMessage[] {
  const rounds = [...detail.rounds].sort((left, right) => left.roundNumber - right.roundNumber);
  const messages: InterviewTimelineMessage[] = [];
  let hasNonProjectRound = false;
  let projectPhaseInserted = false;

  for (const round of rounds) {
    const questionLabel = QUESTION_TYPE_LABELS[round.question.type] ?? "综合题";

    if (!projectPhaseInserted && round.question.type === "project" && hasNonProjectRound) {
      messages.push(createSystemMessage("phase-project", "基础能力评估已完成，进入项目深挖。"));
      projectPhaseInserted = true;
    }

    if (round.roundNumber === detail.totalRounds) {
      messages.push(createSystemMessage("phase-final", "最后一题：综合总结。", { tone: "warning" }));
    }

    messages.push({
      id: `assistant-question-${round.roundNumber}`,
      kind: "assistant",
      title: `第 ${round.roundNumber} 题 · ${questionLabel}`,
      body: round.question.title,
      tag: questionLabel,
      isCurrent: false,
    });

    if (round.userAnswer?.trim()) {
      messages.push({
        id: `user-answer-${round.roundNumber}`,
        kind: "user",
        body: round.userAnswer,
        timestamp: formatShortTime(round.answeredAt ?? detail.createdAt),
        status: "recorded",
      });
    }

    if (round.aiFollowUp?.trim()) {
      messages.push({
        id: `assistant-followup-${round.roundNumber}`,
        kind: "assistant",
        title: `第 ${round.roundNumber} 题 · 追问`,
        body: round.aiFollowUp,
        tag: "追问",
        isCurrent: false,
      });
    }

    if (round.question.type !== "project") {
      hasNonProjectRound = true;
    }
  }

  const currentAssistantId =
    detail.status === "in_progress"
      ? [...messages].reverse().find((message) => message.kind === "assistant")?.id ?? null
      : null;

  return messages.map((message) => {
    if (message.kind !== "assistant") {
      return message;
    }

    return {
      ...message,
      isCurrent: message.id === currentAssistantId,
    };
  });
}

function getReportProgressMessage(progress: { progress: number; stage: string; estimatedTime: number }) {
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
  if (!(error instanceof Error)) {
    return false;
  }

  const message = error.message.toLowerCase();
  return message.includes("401") || message.includes("unauthorized");
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
  const [reportProgress, setReportProgress] = useState<{ progress: number; stage: string; estimatedTime: number } | null>(null);
  const [, setConnectionStatus] = useState<InterviewConnectionStatus>("disconnected");
  const [connectionNotices, setConnectionNotices] = useState<SystemTimelineMessage[]>([]);
  const [pendingAnswer, setPendingAnswer] = useState<LocalPendingAnswer | null>(null);
  const [reportReady, setReportReady] = useState(false);
  const [storedDraft, setStoredDraft] = useState<string | null>(null);
  const [draftSavedAt, setDraftSavedAt] = useState<number | null>(null);
  const [draftRecoveredAt, setDraftRecoveredAt] = useState<number | null>(null);
  const [draftOwnedByEditor, setDraftOwnedByEditor] = useState(false);
  const [nowTick, setNowTick] = useState(() => Date.now());

  const interviewModeIndex = Math.max(0, INTERVIEW_MODE_OPTIONS.findIndex((option) => option.value === interviewMode));
  const interviewModeSliderStyle = {
    width: `calc((100% - 0.5rem) / ${INTERVIEW_MODE_OPTIONS.length})`,
    transform: `translateX(${interviewModeIndex * 100}%)`,
  };

  const startInterviewButtonClassName =
    "relative overflow-hidden transition-all duration-300 ease-out transform-gpu hover:-translate-y-0.5 hover:shadow-[0_16px_30px_rgba(17,24,39,0.18)] active:translate-y-0 active:scale-[0.97] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[rgba(17,24,39,0.28)] focus-visible:ring-offset-2 focus-visible:ring-offset-white motion-reduce:transform-none motion-reduce:transition-none before:pointer-events-none before:absolute before:inset-0 before:content-[''] before:bg-[linear-gradient(120deg,transparent,rgba(255,255,255,0.28),transparent)] before:translate-x-[-120%] before:opacity-0 before:transition-all before:duration-700 hover:before:translate-x-[120%] hover:before:opacity-100";

  const autoCreateAttemptRef = useRef<string | null>(null);
  const createInterviewOnceRef = useRef<ReturnType<typeof createSingleFlight<[string, string], string>> | null>(null);

  const entryMode = getInterviewEntryMode({
    interviewId,
    positionCode: positionFromQuery,
  });
  const showPositionCards = entryMode === "choose-position";
  const latestRound = useMemo(() => detail?.rounds[detail.rounds.length - 1] ?? null, [detail]);
  const isReportGenerating = detail?.status === "generating_report";
  const isReportFailed = detail?.status === "report_failed";
  const isCompleted = detail?.status === "completed" || reportReady;
  const draftStorageKey = interviewId ? `${DRAFT_STORAGE_PREFIX}${interviewId}` : null;

  if (!createInterviewOnceRef.current) {
    createInterviewOnceRef.current = createSingleFlight(async (positionCode: string, mode: string) => {
      setStartingPositionCode(positionCode);
      setError(null);
      try {
        const response = await createInterview(buildCreateInterviewPayload(positionCode, mode));
        router.push(getInterviewTargetUrl(response.interviewId));
        return response.interviewId;
      } catch (requestError) {
        setError(getRequestErrorMessage(requestError, "创建面试失败"));
        throw requestError;
      } finally {
        setStartingPositionCode(null);
      }
    });
  }

  const appendConnectionNotice = useCallback((body: string, tone: InterviewSystemTone) => {
    setConnectionNotices((current) => {
      const lastNotice = current[current.length - 1];
      if (lastNotice?.body === body) {
        return current;
      }

      const next = [...current, createSystemMessage(`connection-${Date.now()}`, body, { tone })];
      return next.slice(-6);
    });
  }, []);

  const navigateTo = useCallback((targetUrl: string) => {
    if (typeof window !== "undefined") {
      window.location.assign(targetUrl);
      return;
    }

    router.replace(targetUrl);
  }, [router]);

  const handleHubUnauthorized = useCallback(() => {
    const targetUrl =
      typeof window !== "undefined"
        ? `${window.location.pathname}${window.location.search}`
        : interviewId
          ? `/interview?interviewId=${encodeURIComponent(interviewId)}`
          : "/interview";

    useAuthStore.getState().clearSession();
    writeStoredAuth(null);
    openLogin({ type: "navigate", target: targetUrl });
  }, [interviewId, openLogin]);

  const clearDraft = useCallback(() => {
    if (draftStorageKey && typeof window !== "undefined") {
      window.localStorage.removeItem(draftStorageKey);
    }

    setStoredDraft(null);
    setDraftSavedAt(null);
    setDraftRecoveredAt(null);
    setDraftOwnedByEditor(false);
  }, [draftStorageKey]);

  const persistDraft = useCallback((nextValue: string) => {
    if (!draftStorageKey || typeof window === "undefined") {
      return;
    }

    if (!nextValue.trim()) {
      if (draftOwnedByEditor) {
        window.localStorage.removeItem(draftStorageKey);
        setStoredDraft(null);
        setDraftSavedAt(null);
      }
      return;
    }

    window.localStorage.setItem(draftStorageKey, nextValue);
    setStoredDraft(nextValue);
    setDraftSavedAt(Date.now());
    setDraftOwnedByEditor(true);
  }, [draftOwnedByEditor, draftStorageKey]);

  const refreshInterview = useCallback(async (targetInterviewId: string) => {
    try {
      const response = await getInterview(targetInterviewId);
      setDetail(response);
      setInterviewMode(response.interviewMode);
      setReportReady(response.status === "completed");
      setReportProgress((current) => {
        if (response.status === "generating_report") {
          return current ?? { progress: 10, stage: "ended", estimatedTime: 30 };
        }

        if (response.status === "completed") {
          return { progress: 100, stage: "completed", estimatedTime: 0 };
        }

        return null;
      });
      setError(null);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "面试详情加载失败"));
    }
  }, []);

  const handleDraftRestore = useCallback(() => {
    if (!storedDraft) {
      return;
    }

    setAnswerText(storedDraft);
    setDraftRecoveredAt(Date.now());
    setDraftOwnedByEditor(true);
  }, [storedDraft]);

  const handleAnswerChange = useCallback((nextValue: string) => {
    setAnswerText(nextValue);
    persistDraft(nextValue);
  }, [persistDraft]);

  const startPositionInterview = useCallback((positionCode: string, mode = interviewMode) => {
    if (!positionCode) {
      setError("请先选择岗位");
      return;
    }

    void createInterviewOnceRef.current?.(positionCode, mode).catch(() => undefined);
  }, [interviewMode]);

  const handleSubmitAnswer = useCallback(async () => {
    if (!interviewId || !detail || !latestRound || !answerText.trim()) {
      return;
    }

    const answer = answerText.trim();
    const pendingId = `pending-answer-${Date.now()}`;
    const roundNumber = latestRound.roundNumber;
    const pendingTimestamp = new Date().toISOString();
    const evaluatingTimer =
      typeof window !== "undefined"
        ? window.setTimeout(() => {
            setPendingAnswer((current) => {
              if (!current || current.id !== pendingId || current.status !== "sent") {
                return current;
              }

              return {
                ...current,
                status: "evaluating",
              };
            });
          }, 180)
        : null;

    setPendingAnswer({
      id: pendingId,
      roundNumber,
      text: answer,
      timestamp: pendingTimestamp,
      status: "sent",
    });
    setAnswerText("");
    clearDraft();
    setSubmittingAnswer(true);
    setError(null);

    try {
      const response = await submitAnswer(interviewId, {
        answer,
        inputMode: "text",
      });

      if (response.aiResponse.type === "follow_up") {
        setPendingAnswer((current) => {
          if (!current || current.id !== pendingId) {
            return current;
          }

          return {
            ...current,
            status: "followup",
          };
        });
      } else {
        setPendingAnswer((current) => {
          if (!current || current.id !== pendingId || current.status === "failed") {
            return current;
          }

          return {
            ...current,
            status: "evaluating",
          };
        });
      }

      await refreshInterview(interviewId);
    } catch (requestError) {
      const message = getRequestErrorMessage(requestError, "提交回答失败");
      setPendingAnswer((current) => {
        if (!current || current.id !== pendingId) {
          return current;
        }

        return {
          ...current,
          status: "failed",
        };
      });
      setAnswerText(answer);
      persistDraft(answer);
      setError(message);
    } finally {
      if (evaluatingTimer) {
        window.clearTimeout(evaluatingTimer);
      }
      setSubmittingAnswer(false);
    }
  }, [
    answerText,
    clearDraft,
    detail,
    interviewId,
    latestRound,
    persistDraft,
    refreshInterview,
  ]);

  const handleFinishInterview = useCallback(async () => {
    if (!interviewId) {
      return;
    }

    setFinishingInterview(true);
    setError(null);
    try {
      const response = await finishInterview(interviewId);
      if (response.status === "completed") {
        setReportReady(true);
        setReportProgress({
          progress: 100,
          stage: "completed",
          estimatedTime: 0,
        });
      } else {
        setReportReady(false);
        setReportProgress({
          progress: 10,
          stage: "ended",
          estimatedTime: response.estimatedTime,
        });
      }

      await refreshInterview(interviewId);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "结束面试失败"));
    } finally {
      setFinishingInterview(false);
    }
  }, [interviewId, refreshInterview]);

  const handleMessageAction = useCallback((action: InterviewSystemAction) => {
    if (!interviewId) {
      return;
    }

    if (action === "view-report") {
      navigateTo(`/report/${interviewId}`);
      return;
    }

    if (action === "retry-report") {
      void handleFinishInterview();
    }
  }, [handleFinishInterview, interviewId, navigateTo]);

  useEffect(() => {
    setPendingAnswer(null);
    setConnectionNotices([]);
    setConnectionStatus("disconnected");
    setReportReady(false);
    setReportProgress(null);
    setStoredDraft(null);
    setDraftSavedAt(null);
    setDraftRecoveredAt(null);
    setDraftOwnedByEditor(false);
    setAnswerText("");
  }, [interviewId]);

  useEffect(() => {
    if (!interviewId || typeof window === "undefined") {
      return;
    }

    const savedDraft = window.localStorage.getItem(`${DRAFT_STORAGE_PREFIX}${interviewId}`);
    if (!savedDraft?.trim()) {
      return;
    }

    setStoredDraft(savedDraft);
  }, [interviewId]);

  useEffect(() => {
    if (!detail) {
      return;
    }

    const timer = window.setInterval(() => {
      setNowTick(Date.now());
    }, 1000);

    return () => {
      window.clearInterval(timer);
    };
  }, [detail]);

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
  }, [interviewId, refreshInterview]);

  useEffect(() => {
    if (!pendingAnswer || !detail) {
      return;
    }

    const alreadyPersisted = detail.rounds.some(
      (round) =>
        round.roundNumber === pendingAnswer.roundNumber &&
        round.userAnswer?.trim() === pendingAnswer.text.trim(),
    );

    if (alreadyPersisted) {
      setPendingAnswer(null);
    }
  }, [detail, pendingAnswer]);

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

  useEffect(() => {
    if (!hydrated || !interviewId || !accessToken) {
      return;
    }

    let active = true;
    const connection = createInterviewHub(accessToken);

    const handleTypingIndicator = (payload?: unknown) => {
      if (!active) {
        return;
      }

      if (
        payload &&
        typeof payload === "object" &&
        "isTyping" in payload &&
        payload.isTyping === false
      ) {
        return;
      }

      setPendingAnswer((current) => {
        if (!current || current.status === "failed" || current.status === "followup") {
          return current;
        }

        return {
          ...current,
          status: "evaluating",
        };
      });
    };

    const handleReportProgress = (payload?: unknown) => {
      if (!active || !payload || typeof payload !== "object") {
        return;
      }

      const candidate = payload as {
        progress?: number;
        stage?: string;
        estimatedTime?: number;
      };

      if (typeof candidate.progress !== "number") {
        return;
      }

      setReportProgress({
        progress: candidate.progress,
        stage: candidate.stage ?? "ended",
        estimatedTime:
          typeof candidate.estimatedTime === "number" ? candidate.estimatedTime : 0,
      });
    };

    const handleErrorOccurred = (payload?: unknown) => {
      if (!active) {
        return;
      }

      if (
        payload &&
        typeof payload === "object" &&
        "message" in payload &&
        typeof payload.message === "string"
      ) {
        setError(payload.message);
      }

      if (
        payload &&
        typeof payload === "object" &&
        "stage" in payload &&
        payload.stage === "report_failed"
      ) {
        setReportReady(false);
      }

      void refreshInterview(interviewId);
    };

    const joinInterviewRoom = async () => {
      await connection.invoke("JoinInterview", { interviewId });
    };

    connection.on("ReceiveQuestion", () => {
      if (!active) {
        return;
      }

      void refreshInterview(interviewId);
    });
    connection.on("ReceiveFollowUp", () => {
      if (!active) {
        return;
      }

      setPendingAnswer((current) => {
        if (!current || current.status === "failed") {
          return current;
        }

        return {
          ...current,
          status: "followup",
        };
      });
      void refreshInterview(interviewId);
    });
    connection.on("InterviewStatusChanged", () => {
      if (!active) {
        return;
      }

      void refreshInterview(interviewId);
    });
    connection.on("ReportReady", () => {
      if (!active) {
        return;
      }

      setReportReady(true);
      setReportProgress({
        progress: 100,
        stage: "completed",
        estimatedTime: 0,
      });
      void refreshInterview(interviewId);
    });
    connection.on("ErrorOccurred", handleErrorOccurred);
    connection.on("TypingIndicator", handleTypingIndicator);
    connection.on("typingindicator", handleTypingIndicator);
    connection.on("ReportProgress", handleReportProgress);
    connection.on("reportprogress", handleReportProgress);
    connection.onreconnecting(() => {
      if (!active) {
        return;
      }

      setConnectionStatus("reconnecting");
      appendConnectionNotice("网络波动，正在重试连接。", "warning");
    });
    connection.onreconnected(() => {
      if (!active) {
        return;
      }

      setConnectionStatus("connected");
      appendConnectionNotice("连接已恢复。", "success");
      void (async () => {
        try {
          await joinInterviewRoom();
          await refreshInterview(interviewId);
        } catch (error) {
          if (isUnauthorizedSignalRError(error)) {
            handleHubUnauthorized();
            return;
          }

          return;
        }
      })();
    });
    connection.onclose(() => {
      if (!active) {
        return;
      }

      setConnectionStatus("disconnected");
      appendConnectionNotice("连接已断开，请稍后重试。", "danger");
    });

    void (async () => {
      try {
        await connection.start();
        if (!active) {
          return;
        }

        setConnectionStatus("connected");
        await joinInterviewRoom();
      } catch (error) {
        if (!active) {
          return;
        }

        if (isUnauthorizedSignalRError(error)) {
          setConnectionStatus("disconnected");
          handleHubUnauthorized();
          return;
        }

        setConnectionStatus("disconnected");
        appendConnectionNotice("暂时无法建立实时连接。", "warning");
      }
    })();

    return () => {
      active = false;
      void stopInterviewHub(connection);
    };
  }, [accessToken, appendConnectionNotice, handleHubUnauthorized, hydrated, interviewId, refreshInterview]);

  const pendingAnswerAlreadyPersisted = useMemo(() => {
    if (!pendingAnswer || !detail) {
      return false;
    }

    return detail.rounds.some(
      (round) =>
        round.roundNumber === pendingAnswer.roundNumber &&
        round.userAnswer?.trim() === pendingAnswer.text.trim(),
    );
  }, [detail, pendingAnswer]);

  const baseMessages = useMemo(
    () => (detail ? buildRoundMessages(detail) : []),
    [detail],
  );

  const tailMessages = useMemo(() => {
    const messages: InterviewTimelineMessage[] = [];

    if (pendingAnswer && !pendingAnswerAlreadyPersisted) {
      messages.push({
        id: pendingAnswer.id,
        kind: "user",
        body: pendingAnswer.text,
        timestamp: formatShortTime(pendingAnswer.timestamp),
        status: pendingAnswer.status,
      });
    }

    if (finishingInterview && detail?.status === "in_progress") {
      messages.push(createSystemMessage("interview-finishing", "正在结束面试，准备生成报告。"));
    }

    if (detail && detail.status !== "in_progress") {
      messages.push(createSystemMessage("interview-ended", "面试已结束，本场回答已封存。"));
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
        createSystemMessage("report-ready", "报告已生成，点击查看。", {
          tone: "success",
          actionKey: "view-report",
          actionLabel: "查看报告",
        }),
      );
    } else if (detail?.status === "generating_report" && reportProgress) {
      messages.push(
        createSystemMessage(
          `report-stage-${reportProgress.stage}`,
          getReportProgressMessage(reportProgress),
          { tone: reportProgress.stage === "ended" ? "warning" : "default" },
        ),
      );
    }

    messages.push(...connectionNotices);
    return messages;
  }, [
    connectionNotices,
    detail,
    finishingInterview,
    isCompleted,
    pendingAnswer,
    pendingAnswerAlreadyPersisted,
    reportProgress,
  ]);

  const messages = useMemo(() => {
    const seen = new Set<string>();
    return [...baseMessages, ...tailMessages].filter((message) => {
      if (seen.has(message.id)) {
        return false;
      }

      seen.add(message.id);
      return true;
    });
  }, [baseMessages, tailMessages]);

  const elapsedLabel = useMemo(() => {
    if (!detail) {
      return "00:00";
    }

    return formatElapsed(detail.createdAt, nowTick);
  }, [detail, nowTick]);

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

  const finishDisabled = useMemo(() => {
    if (!interviewId || submittingAnswer || finishingInterview) {
      return true;
    }

    if (isReportGenerating || isCompleted) {
      return true;
    }

    return false;
  }, [finishingInterview, interviewId, isCompleted, isReportGenerating, submittingAnswer]);

  const canSubmit = useMemo(() => {
    if (!interviewId || !detail || !answerText.trim()) {
      return false;
    }

    if (submittingAnswer || finishingInterview) {
      return false;
    }

    return detail.status === "in_progress";
  }, [answerText, detail, finishingInterview, interviewId, submittingAnswer]);

  const composerPlaceholder = useMemo(() => {
    if (isCompleted) {
      return "面试已结束，报告已生成。可通过消息流中的按钮查看完整报告。";
    }

    if (isReportFailed) {
      return "面试已结束，报告生成失败。可点击“重新生成报告”继续处理。";
    }

    if (isReportGenerating) {
      return "面试已结束，报告正在生成中。";
    }

    return "请在这里输入你的回答，支持长段落、多点拆解与结构化表达。";
  }, [isCompleted, isReportFailed, isReportGenerating]);

  const currentRound = detail ? Math.min(detail.currentRound, detail.totalRounds) : 0;

  const handleComposerKeyDown = useCallback<React.KeyboardEventHandler<HTMLTextAreaElement>>((event) => {
    if (event.key !== "Enter" || event.shiftKey) {
      return;
    }

    if (!event.metaKey && !event.ctrlKey) {
      return;
    }

    event.preventDefault();
    if (canSubmit) {
      void handleSubmitAnswer();
    }
  }, [canSubmit, handleSubmitAnswer]);

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
              {startingPositionCode ? "正在创建..." : "重新创建面试"}
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
            <h2 className="display-title !text-[clamp(2rem,3vw,3.4rem)]">选择一个岗位，直接进入正式面试</h2>
            <p className="text-caption max-w-[720px] text-[length:var(--token-font-size-lg)]">
              点击岗位卡片后会立即创建面试，并进入新的聊天式面试工作台。
            </p>
          </div>
          <div className="w-full shrink-0 md:w-[320px]">
            <div className="mb-2 flex flex-col items-end gap-0.5 text-right">
              <p className="text-[10px] font-semibold uppercase tracking-[0.24em] text-[var(--token-color-primary)]">
                面试氛围
              </p>
              <p className="text-[11px] text-[var(--token-color-text-secondary)]">控制节奏与压力感</p>
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
              className="flex h-full flex-col justify-between gap-5 transition-all duration-300 ease-out hover:-translate-y-1 hover:border-[rgba(17,24,39,0.12)] hover:shadow-[0_18px_45px_rgba(17,24,39,0.12)]"
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
                <div className="flex items-center justify-between">
                  <p className="text-caption">题量 {position.questionCount}</p>
                  <Button
                    className={startInterviewButtonClassName}
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
    <div className="flex min-h-[calc(100vh-11rem)] flex-col">
      <InterviewTopBar
        currentRound={currentRound}
        elapsedLabel={elapsedLabel}
        finishDisabled={finishDisabled}
        finishLabel={finishLabel}
        onFinish={() => void handleFinishInterview()}
        positionName={detail.positionName}
        totalRounds={detail.totalRounds}
      />

      <div className="pt-4">{error ? <ErrorState description={error} /> : null}</div>

      <InterviewMessageList messages={messages} onAction={handleMessageAction} />

      <InterviewComposer
        canRestoreDraft={Boolean(storedDraft && !answerText)}
        canSubmit={canSubmit}
        disabled={detail.status !== "in_progress"}
        draftLabel={draftLabel}
        hintText={undefined}
        onChange={handleAnswerChange}
        onKeyDown={handleComposerKeyDown}
        onRestoreDraft={handleDraftRestore}
        onSubmit={() => void handleSubmitAnswer()}
        placeholder={composerPlaceholder}
        sendLabel={submittingAnswer ? "发送中..." : "发送回答"}
        statusText={undefined}
        value={answerText}
      />
    </div>
  );
}
