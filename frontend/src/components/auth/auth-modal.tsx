"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { login, register } from "@/services/auth-service";
import { getPositions } from "@/services/catalog-service";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { executeAuthCallback } from "@/lib/auth-action-registry";
import { writeStoredAuth } from "@/utils/storage";
import { Button } from "@/components/ui/button";
import type { PositionSummary } from "@/types/api";

export function AuthModal() {
  const router = useRouter();
  const setSession = useAuthStore((state) => state.setSession);
  const { open, mode, pendingAction, setMode, clearPendingAction, close } = useAuthModalStore();

  const [form, setForm] = useState({
    username: "",
    password: "",
    email: "",
    phone: "",
    targetPosition: "",
  });
  const [positions, setPositions] = useState<PositionSummary[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open && mode === "register" && positions.length === 0) {
      void getPositions().then((data) => {
        setPositions(data);
        setForm((f) => ({ ...f, targetPosition: f.targetPosition || data[0]?.code || "" }));
      });
    }
  }, [open, mode, positions.length]);

  useEffect(() => {
    if (!open) {
      setForm({ username: "", password: "", email: "", phone: "", targetPosition: "" });
      setError(null);
    }
  }, [open]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
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
      const result = await login({ username: form.username, password: form.password });

      setSession(result);
      writeStoredAuth(result);
      const action = pendingAction;
      clearPendingAction();
      close();
      if (action?.type === "navigate") {
        router.push(action.target);
      } else if (action?.type === "callback") {
        executeAuthCallback(action.callbackId);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "操作失败");
    } finally {
      setSubmitting(false);
    }
  }

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm"
      onClick={close}
    >
      <div
        className="relative w-full max-w-sm rounded-3xl border border-[var(--token-color-border-default)] bg-white p-8 shadow-[var(--token-shadow-modal)]"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          className="absolute right-5 top-5 text-[var(--token-color-text-tertiary)] transition-colors hover:text-[var(--token-color-text-primary)]"
          onClick={close}
          type="button"
        >
          <svg fill="none" height="16" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" viewBox="0 0 24 24" width="16">
            <path d="M18 6 6 18M6 6l12 12" />
          </svg>
        </button>

        <div className="mb-6 flex gap-2">
          <button
            className={`flex-1 rounded-[var(--token-radius-lg)] py-2 text-sm font-semibold transition-colors ${mode === "login" ? "bg-[rgba(0,102,255,0.08)] text-primary" : "text-[var(--token-color-text-secondary)] hover:bg-[rgba(17,24,39,0.04)]"}`}
            onClick={() => { setMode("login"); setError(null); }}
            type="button"
          >
            登录
          </button>
          <button
            className={`flex-1 rounded-[var(--token-radius-lg)] py-2 text-sm font-semibold transition-colors ${mode === "register" ? "bg-[rgba(0,102,255,0.08)] text-primary" : "text-[var(--token-color-text-secondary)] hover:bg-[rgba(17,24,39,0.04)]"}`}
            onClick={() => { setMode("register"); setError(null); }}
            type="button"
          >
            注册
          </button>
        </div>

        <form className="space-y-4" onSubmit={handleSubmit}>
          <div>
            <label className="mb-1.5 block text-sm font-semibold">用户名</label>
            <input
              className="input-shell"
              onChange={(e) => setForm((f) => ({ ...f, username: e.target.value }))}
              placeholder="请输入用户名"
              required
              value={form.username}
            />
          </div>

          {mode === "register" && (
            <>
              <div>
                <label className="mb-1.5 block text-sm font-semibold">邮箱</label>
                <input
                  className="input-shell"
                  onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                  placeholder="name@example.com"
                  required
                  type="email"
                  value={form.email}
                />
              </div>
              <div>
                <label className="mb-1.5 block text-sm font-semibold">手机号</label>
                <input
                  className="input-shell"
                  onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                  placeholder="选填"
                  value={form.phone}
                />
              </div>
              {positions.length > 0 && (
                <div>
                  <label className="mb-1.5 block text-sm font-semibold">目标岗位</label>
                  <select
                    className="input-shell"
                    onChange={(e) => setForm((f) => ({ ...f, targetPosition: e.target.value }))}
                    value={form.targetPosition}
                  >
                    {positions.map((p) => (
                      <option key={p.code} value={p.code}>{p.name}</option>
                    ))}
                  </select>
                </div>
              )}
            </>
          )}

          <div>
            <label className="mb-1.5 block text-sm font-semibold">密码</label>
            <input
              className="input-shell"
              onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
              placeholder="请输入密码"
              required
              type="password"
              value={form.password}
            />
          </div>

          {error ? <p className="text-sm text-error">{error}</p> : null}

          <Button className="w-full" disabled={submitting} type="submit">
            {submitting ? "处理中..." : mode === "login" ? "登录" : "注册并登录"}
          </Button>
        </form>
      </div>
    </div>
  );
}
