"use client";

import { useEffect, useMemo, useState } from "react";
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
import { useAuthStore } from "@/stores/auth-store";
import type { InterviewCurrentDetail, PositionSummary } from "@/types/api";

export function InterviewClient() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const interviewId = searchParams.get("interviewId");
  const positionFromQuery = searchParams.get("positionCode");

  const [positions, setPositions] = useState<PositionSummary[]>([]);
  const [selectedPosition, setSelectedPosition] = useState(positionFromQuery ?? "");
  const [detail, setDetail] = useState<InterviewCurrentDetail | null>(null);
  const [answerText, setAnswerText] = useState("");
  const [interviewMode, setInterviewMode] = useState("standard");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const latestRound = useMemo(
    () => detail?.rounds[detail.rounds.length - 1] ?? null,
    [detail],
  );

  function navigateTo(targetUrl: string) {
    if (typeof window !== "undefined") {
      window.location.assign(targetUrl);
      return;
    }

    router.replace(targetUrl);
  }

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!accessToken) {
      router.replace("/login");
      return;
    }

    void (async () => {
      try {
        const positionList = await getPositions();
        setPositions(positionList);
        setSelectedPosition((current) => current || positionList[0]?.code || "");
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "岗位加载失败");
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, router]);

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
        // MVP 阶段失败时退化到轮询式刷新，不阻塞页面使用。
      }
    })();

    return () => {
      void stopInterviewHub(connection);
    };
  }, [accessToken, hydrated, interviewId, router]);

  async function refreshInterview(targetInterviewId: string) {
    try {
      const response = await getInterview(targetInterviewId);
      setDetail(response);
      setError(null);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "面试详情加载失败");
    }
  }

  async function handleCreateInterview() {
    if (!selectedPosition) {
      setError("请先选择岗位");
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const response = await createInterview({
        positionCode: selectedPosition,
        interviewMode,
        questionTypes: ["technical", "project", "scenario"],
        roundCount: 5,
      });
      navigateTo(`/interview?interviewId=${response.interviewId}`);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "创建面试失败");
    } finally {
      setSubmitting(false);
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
      setError(requestError instanceof Error ? requestError.message : "提交回答失败");
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
      setError(requestError instanceof Error ? requestError.message : "结束面试失败");
    } finally {
      setSubmitting(false);
    }
  }

  if (!hydrated || loading) {
    return <LoadingState label="正在初始化面试环境..." />;
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
          {!interviewId ? (
            <Button disabled={submitting} onClick={handleCreateInterview} type="button">
              {submitting ? "创建中..." : "创建一场新面试"}
            </Button>
          ) : (
            <Button
              disabled={submitting}
              onClick={handleFinishInterview}
              type="button"
              variant="secondary"
            >
              主动结束面试
            </Button>
          )}
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
            description="先从左侧选择岗位并创建面试。创建后会立即拿到首题，并进入后续追问或下一题流转。"
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
                placeholder="请输入你的回答。MVP 阶段先走文本回答链路。"
                value={answerText}
              />
              <div className="flex flex-wrap gap-3">
                <Button disabled={submitting || !answerText.trim()} onClick={handleSubmitAnswer} type="button">
                  {submitting ? "提交中..." : "提交回答"}
                </Button>
                <Button disabled={submitting} onClick={handleFinishInterview} type="button" variant="secondary">
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
                      <span className="text-caption">{round.answeredAt ? "已回答" : "待回答"}</span>
                    </div>
                    <p>{round.question.title}</p>
                    <p className="text-caption">{round.userAnswer ?? "尚未提交回答"}</p>
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
