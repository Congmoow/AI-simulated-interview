"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useMemo } from "react";
import { SideNav } from "@/components/layout/side-nav";
import { useAuthStore } from "@/stores/auth-store";
import { writeStoredAuth } from "@/utils/storage";

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const user = useAuthStore((state) => state.user);
  const clearSession = useAuthStore((state) => state.clearSession);
  const isAuthPage = useMemo(() => pathname.startsWith("/login"), [pathname]);

  if (isAuthPage) {
    return <main className="min-h-screen">{children}</main>;
  }

  return (
    <div className="mx-auto flex min-h-screen w-full max-w-[1600px] gap-6 px-4 py-6 lg:px-6">
      <div className="hidden lg:block">
        <SideNav />
      </div>
      <div className="flex min-h-[calc(100vh-3rem)] min-w-0 flex-1 flex-col gap-6">
        <header className="surface-card flex flex-col gap-4 px-6 py-5 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <span className="section-label">系统导航</span>
            <h1 className="mt-2 text-[length:var(--token-font-size-2xl)] font-semibold">
              AI 模拟面试与能力提升软件
            </h1>
            <p className="text-caption mt-2">
              前端只负责展示与交互，核心业务链路统一由 ASP.NET Core API 编排。
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <div className="surface-muted px-4 py-3">
              <p className="text-caption">
                {user ? `${user.username} · ${user.role}` : "未登录"}
              </p>
            </div>
            {user ? (
              <button
                className="secondary-button"
                onClick={() => {
                  clearSession();
                  writeStoredAuth(null);
                }}
                type="button"
              >
                退出登录
              </button>
            ) : (
              <Link className="primary-button" href="/login">
                去登录
              </Link>
            )}
          </div>
        </header>
        <main className="page-enter flex-1">{children}</main>
      </div>
    </div>
  );
}
