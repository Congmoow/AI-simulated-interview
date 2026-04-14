"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { getPositions } from "@/services/catalog-service";
import { login, register } from "@/services/auth-service";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorState, LoadingState } from "@/components/ui/state-panel";
import { useAuthStore } from "@/stores/auth-store";
import { writeStoredAuth } from "@/utils/storage";
import type { PositionSummary } from "@/types/api";

type AuthMode = "login" | "register";

export default function LoginPage() {
  const router = useRouter();
  const setSession = useAuthStore((state) => state.setSession);
  const [mode, setMode] = useState<AuthMode>("login");
  const [positions, setPositions] = useState<PositionSummary[]>([]);
  const [loadingPositions, setLoadingPositions] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [form, setForm] = useState({
    username: "",
    password: "",
    email: "",
    phone: "",
    targetPosition: "",
  });

  useEffect(() => {
    void (async () => {
      try {
        const response = await getPositions();
        setPositions(response);
        setForm((current) => ({
          ...current,
          targetPosition: current.targetPosition || response[0]?.code || "",
        }));
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "岗位数据加载失败");
      } finally {
        setLoadingPositions(false);
      }
    })();
  }, []);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      if (mode === "register") {
        await register({
          username: form.username,
          password: form.password,
          email: form.email,
          phone: form.phone || undefined,
          targetPosition: form.targetPosition || undefined,
        });
      }

      const loginResult = await login({
        username: form.username,
        password: form.password,
      });

      setSession(loginResult);
      writeStoredAuth(loginResult);
      router.replace("/dashboard");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "认证失败");
    } finally {
      setSubmitting(false);
    }
  }

  if (loadingPositions) {
    return (
      <div className="mx-auto flex min-h-screen max-w-[1200px] items-center px-4 py-10">
        <LoadingState label="正在准备登录与注册所需数据..." />
      </div>
    );
  }

  if (error && positions.length === 0) {
    return (
      <div className="mx-auto flex min-h-screen max-w-[1200px] items-center px-4 py-10">
        <ErrorState description={error} />
      </div>
    );
  }

  return (
    <div className="mx-auto grid min-h-screen max-w-[1400px] gap-6 px-4 py-10 lg:grid-cols-[1.1fr_0.9fr]">
      <section className="surface-card flex flex-col justify-between gap-8 px-8 py-8 lg:px-10">
        <div className="space-y-5">
          <span className="section-label">文档约束落地</span>
          <h1 className="display-title">登录、注册与岗位准备，先进入同一套训练主链路。</h1>
          <p className="text-caption max-w-[620px] text-[length:var(--token-font-size-lg)]">
            前端只处理状态与展示；认证、岗位信息、后续问答流转全部通过后端 API 完成。
          </p>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <Card className="p-5">
            <p className="section-label">测试用户</p>
            <p className="mt-3 font-semibold">zhangsan / Pass1234</p>
            <p className="text-caption mt-2">普通用户，可直接开始模拟面试。</p>
          </Card>
          <Card className="p-5">
            <p className="section-label">管理员</p>
            <p className="mt-3 font-semibold">admin / Admin1234</p>
            <p className="text-caption mt-2">可进入后台创建题目并上传知识库文档。</p>
          </Card>
        </div>
      </section>
      <section className="surface-card px-6 py-8 lg:px-8">
        <div className="mb-6 flex gap-3">
          <Button
            className="flex-1"
            onClick={() => setMode("login")}
            type="button"
            variant={mode === "login" ? "primary" : "secondary"}
          >
            登录
          </Button>
          <Button
            className="flex-1"
            onClick={() => setMode("register")}
            type="button"
            variant={mode === "register" ? "primary" : "secondary"}
          >
            注册
          </Button>
        </div>
        <form className="space-y-4" onSubmit={handleSubmit}>
          <div>
            <label className="mb-2 block text-sm font-semibold">用户名</label>
            <input
              className="input-shell"
              onChange={(event) => setForm((current) => ({ ...current, username: event.target.value }))}
              placeholder="请输入用户名"
              required
              value={form.username}
            />
          </div>
          {mode === "register" ? (
            <>
              <div>
                <label className="mb-2 block text-sm font-semibold">邮箱</label>
                <input
                  className="input-shell"
                  onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
                  placeholder="name@example.com"
                  required
                  type="email"
                  value={form.email}
                />
              </div>
              <div>
                <label className="mb-2 block text-sm font-semibold">手机号</label>
                <input
                  className="input-shell"
                  onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))}
                  placeholder="选填"
                  value={form.phone}
                />
              </div>
              <div>
                <label className="mb-2 block text-sm font-semibold">目标岗位</label>
                <select
                  className="input-shell"
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      targetPosition: event.target.value,
                    }))
                  }
                  value={form.targetPosition}
                >
                  {positions.map((position) => (
                    <option key={position.code} value={position.code}>
                      {position.name}
                    </option>
                  ))}
                </select>
              </div>
            </>
          ) : null}
          <div>
            <label className="mb-2 block text-sm font-semibold">密码</label>
            <input
              className="input-shell"
              onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
              placeholder="请输入密码"
              required
              type="password"
              value={form.password}
            />
          </div>
          {error ? <p className="text-sm text-error">{error}</p> : null}
          <Button className="w-full" disabled={submitting} type="submit">
            {submitting ? "提交中..." : mode === "login" ? "登录进入系统" : "注册并登录"}
          </Button>
        </form>
      </section>
    </div>
  );
}
