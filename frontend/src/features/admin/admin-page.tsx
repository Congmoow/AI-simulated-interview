"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/state-panel";
import { createQuestion, getKnowledgeDocuments, uploadKnowledgeDocument } from "@/services/admin-service";
import { getPositions } from "@/services/catalog-service";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { RequireLoginState } from "@/components/auth/require-login-state";
import type { KnowledgeDocumentItem, PositionSummary } from "@/types/api";

export default function AdminPage() {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const user = useAuthStore((state) => state.user);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const [positions, setPositions] = useState<PositionSummary[]>([]);
  const [documents, setDocuments] = useState<KnowledgeDocumentItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [questionForm, setQuestionForm] = useState({
    positionCode: "",
    type: "technical",
    difficulty: "medium",
    title: "",
    content: "",
    idealAnswer: "",
    tags: "Java, Spring Boot",
  });
  const [documentForm, setDocumentForm] = useState({
    title: "",
    positionCode: "",
    tags: "知识库, MVP",
    file: null as File | null,
  });

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!accessToken) {
      openLogin({ type: "navigate", target: "/admin" });
      return;
    }

    if (user?.role && user.role !== "admin") {
      setLoading(false);
      return;
    }

    void (async () => {
      try {
        const [positionList, documentList] = await Promise.all([
          getPositions(),
          getKnowledgeDocuments({ page: 1, pageSize: 10 }),
        ]);
        setPositions(positionList);
        setDocuments(documentList.items);
        setQuestionForm((current) => ({
          ...current,
          positionCode: current.positionCode || positionList[0]?.code || "",
        }));
        setDocumentForm((current) => ({
          ...current,
          positionCode: current.positionCode || positionList[0]?.code || "",
        }));
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "管理数据加载失败");
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin, router, user]);

  async function handleCreateQuestion() {
    setSubmitting(true);
    setError(null);
    try {
      await createQuestion({
        positionCode: questionForm.positionCode,
        type: questionForm.type,
        difficulty: questionForm.difficulty,
        title: questionForm.title,
        content: questionForm.content,
        idealAnswer: questionForm.idealAnswer,
        tags: questionForm.tags.split(",").map((item) => item.trim()).filter(Boolean),
        scoringRubric: {
          technicalAccuracy: 30,
          depth: 25,
          clarity: 20,
          practicality: 25,
        },
      });
      setQuestionForm((current) => ({
        ...current,
        title: "",
        content: "",
        idealAnswer: "",
      }));
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "创建题目失败");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleUploadDocument() {
    if (!documentForm.file) {
      setError("请先选择文件");
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await uploadKnowledgeDocument({
        title: documentForm.title,
        positionCode: documentForm.positionCode,
        tags: documentForm.tags.split(",").map((item) => item.trim()).filter(Boolean),
        file: documentForm.file,
      });
      const refreshed = await getKnowledgeDocuments({ page: 1, pageSize: 10 });
      setDocuments(refreshed.items);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "上传文档失败");
    } finally {
      setSubmitting(false);
    }
  }

  if (!hydrated) {
    return <LoadingState label="正在准备管理后台..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在准备管理后台..." />;
  }

  if (user?.role !== "admin") {
    return (
      <EmptyState
        description="当前账号不是管理员。请使用已授权的管理员账号登录后再进入该页面。"
        title="你没有后台访问权限"
      />
    );
  }

  if (error && documents.length === 0) {
    return <ErrorState description={error} />;
  }

  return (
    <div className="space-y-6">
      {error ? <p className="text-sm text-error">{error}</p> : null}
      <Card className="space-y-4">
        <span className="section-label">AI 配置</span>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p className="font-semibold text-[length:var(--token-font-size-md)] text-[var(--token-color-text-primary)]">
              配置真实 LLM 的报告生成能力
            </p>
            <p className="text-caption mt-1">
              前往 AI 配置页面填写 Base URL、Model 和 API Key，并测试真实 LLM 连接状态。
            </p>
          </div>
          <Button onClick={() => void router.push("/admin/ai-settings")} type="button" variant="secondary">
            前往 AI 配置
          </Button>
        </div>
      </Card>
      <section className="grid gap-6 xl:grid-cols-2">
        <Card className="space-y-4">
          <span className="section-label">创建题目</span>
          <input
            className="input-shell"
            onChange={(event) => setQuestionForm((current) => ({ ...current, title: event.target.value }))}
            placeholder="题目标题"
            value={questionForm.title}
          />
          <textarea
            className="input-shell min-h-[120px]"
            onChange={(event) => setQuestionForm((current) => ({ ...current, content: event.target.value }))}
            placeholder="题目内容"
            value={questionForm.content}
          />
          <textarea
            className="input-shell min-h-[120px]"
            onChange={(event) =>
              setQuestionForm((current) => ({
                ...current,
                idealAnswer: event.target.value,
              }))
            }
            placeholder="参考答案"
            value={questionForm.idealAnswer}
          />
          <div className="grid gap-3 md:grid-cols-3">
            <select
              className="input-shell"
              onChange={(event) =>
                setQuestionForm((current) => ({
                  ...current,
                  positionCode: event.target.value,
                }))
              }
              value={questionForm.positionCode}
            >
              {positions.map((position) => (
                <option key={position.code} value={position.code}>
                  {position.name}
                </option>
              ))}
            </select>
            <select
              className="input-shell"
              onChange={(event) => setQuestionForm((current) => ({ ...current, type: event.target.value }))}
              value={questionForm.type}
            >
              <option value="technical">technical</option>
              <option value="project">project</option>
              <option value="scenario">scenario</option>
              <option value="behavioral">behavioral</option>
            </select>
            <select
              className="input-shell"
              onChange={(event) =>
                setQuestionForm((current) => ({
                  ...current,
                  difficulty: event.target.value,
                }))
              }
              value={questionForm.difficulty}
            >
              <option value="easy">easy</option>
              <option value="medium">medium</option>
              <option value="hard">hard</option>
            </select>
          </div>
          <input
            className="input-shell"
            onChange={(event) => setQuestionForm((current) => ({ ...current, tags: event.target.value }))}
            placeholder="标签，逗号分隔"
            value={questionForm.tags}
          />
          <Button disabled={submitting} onClick={handleCreateQuestion} type="button">
            创建题目
          </Button>
        </Card>
        <Card className="space-y-4">
          <span className="section-label">上传知识库文档</span>
          <input
            className="input-shell"
            onChange={(event) => setDocumentForm((current) => ({ ...current, title: event.target.value }))}
            placeholder="文档标题"
            value={documentForm.title}
          />
          <select
            className="input-shell"
            onChange={(event) =>
              setDocumentForm((current) => ({
                ...current,
                positionCode: event.target.value,
              }))
            }
            value={documentForm.positionCode}
          >
            {positions.map((position) => (
              <option key={position.code} value={position.code}>
                {position.name}
              </option>
            ))}
          </select>
          <input
            className="input-shell"
            onChange={(event) => setDocumentForm((current) => ({ ...current, tags: event.target.value }))}
            placeholder="标签，逗号分隔"
            value={documentForm.tags}
          />
          <input
            className="input-shell"
            onChange={(event) =>
              setDocumentForm((current) => ({
                ...current,
                file: event.target.files?.[0] ?? null,
              }))
            }
            type="file"
          />
          <Button disabled={submitting} onClick={handleUploadDocument} type="button" variant="secondary">
            上传文档
          </Button>
        </Card>
      </section>
      <Card className="space-y-4">
        <span className="section-label">知识库文档列表</span>
        <div className="space-y-4">
          {documents.map((item) => (
            <div className="surface-muted flex flex-col gap-2 p-4 lg:flex-row lg:items-center lg:justify-between" key={item.documentId}>
              <div>
                <p className="font-semibold">{item.title}</p>
                <p className="text-caption">
                  {item.positionCode} · {item.status} · {item.fileSize}
                </p>
              </div>
              <p className="text-caption">切片数：{item.chunkCount}</p>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}
