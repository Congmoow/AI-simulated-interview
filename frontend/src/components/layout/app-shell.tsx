"use client";

import { usePathname } from "next/navigation";
import { useMemo } from "react";
import { SideNav } from "@/components/layout/side-nav";

const PAGE_LABELS: Record<string, string> = {
  "/dashboard": "个人画像",
  "/interview": "模拟面试",
  "/report": "报告中心",
  "/history": "历史趋势",
  "/admin": "管理后台",
};

function getPageLabel(pathname: string): string {
  const match = Object.entries(PAGE_LABELS).find(
    ([key]) => pathname === key || (key !== "/dashboard" && pathname.startsWith(key)),
  );
  return match?.[1] ?? "首页";
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const isAuthPage = useMemo(() => pathname.startsWith("/login"), [pathname]);

  if (isAuthPage) {
    return <main className="min-h-screen">{children}</main>;
  }

  return (
    <div className="min-h-screen p-4">
      <div className="mx-auto flex w-full max-w-[1440px] min-h-[calc(100vh-2rem)] gap-4">
        <div className="hidden lg:flex">
          <SideNav />
        </div>
        <div className="flex min-w-0 flex-1 flex-col gap-6">
          <div className="flex items-center gap-2 px-1 pt-1 text-[length:var(--token-font-size-sm)]">
            <span className="text-[var(--token-color-text-tertiary)]">Simulate OS</span>
            <span className="text-[var(--token-color-text-tertiary)]">/</span>
            <span className="font-medium text-[var(--token-color-text-secondary)]">{getPageLabel(pathname)}</span>
          </div>
          <main className="page-enter min-w-0 flex-1 rounded-3xl border border-[var(--token-color-border-default)] bg-[var(--token-color-bg-surface)] shadow-[var(--token-shadow-modal)] p-6">{children}</main>
        </div>
      </div>
    </div>
  );
}
