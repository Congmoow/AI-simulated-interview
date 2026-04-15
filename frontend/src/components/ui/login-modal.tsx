"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { login } from "@/services/auth-service";
import { useAuthStore } from "@/stores/auth-store";
import { writeStoredAuth } from "@/utils/storage";
import { Button } from "@/components/ui/button";

interface LoginModalProps {
  onClose: () => void;
}

export function LoginModal({ onClose }: LoginModalProps) {
  const router = useRouter();
  const setSession = useAuthStore((state) => state.setSession);
  const [form, setForm] = useState({ username: "", password: "" });
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const result = await login(form);
      setSession(result);
      writeStoredAuth(result);
      onClose();
      void router.push("/dashboard");
    } catch (err) {
      setError(err instanceof Error ? err.message : "登录失败");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm"
      onClick={onClose}
    >
      <div
        className="relative w-full max-w-sm rounded-3xl border border-[var(--token-color-border-default)] bg-white p-8 shadow-[var(--token-shadow-modal)]"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          className="absolute right-5 top-5 text-[var(--token-color-text-tertiary)] transition-colors hover:text-[var(--token-color-text-primary)]"
          onClick={onClose}
          type="button"
        >
          <svg fill="none" height="16" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" viewBox="0 0 24 24" width="16">
            <path d="M18 6 6 18M6 6l12 12" />
          </svg>
        </button>
        <h2 className="text-[length:var(--token-font-size-xl)] font-semibold">登录</h2>
        <p className="text-caption mt-1">登录后继续使用 AI 模拟面试</p>
        <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
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
            {submitting ? "登录中..." : "登录"}
          </Button>
        </form>
      </div>
    </div>
  );
}
