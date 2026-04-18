"use client";

import { usePathname } from "next/navigation";
import { useMemo } from "react";

import { SideNav } from "@/components/layout/side-nav";
import { cn } from "@/lib/cn";

const PAGE_LABELS: Record<string, string> = {
  "/dashboard": "个人画像",
  "/interview": "模拟面试",
  "/reports": "报告中心",
  "/resources": "学习资源",
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
  const isInterviewPage = useMemo(() => pathname.startsWith("/interview"), [pathname]);

  if (isAuthPage) {
    return <main className="min-h-screen">{children}</main>;
  }

  return (
    <div
      className={cn(
        "p-4",
        isInterviewPage ? "h-dvh overflow-hidden" : "min-h-screen",
      )}
    >
      <div
        className={cn(
          "mx-auto flex w-full max-w-[1440px] gap-4",
          isInterviewPage ? "h-full" : "min-h-[calc(100vh-2rem)]",
        )}
      >
        <div className="hidden lg:flex">
          <SideNav />
        </div>
        <div
          className={cn(
            "flex min-w-0 flex-1 flex-col gap-6",
            isInterviewPage && "min-h-0 overflow-hidden",
          )}
        >
          <div className="flex items-center gap-2 px-1 pt-1 text-[length:var(--token-font-size-sm)]">
            <span className="font-semibold text-[var(--token-color-text-tertiary)]">APTAI</span>
            <span className="text-[var(--token-color-text-tertiary)]">/</span>
            <span className="font-medium text-[var(--token-color-text-secondary)]">
              {getPageLabel(pathname)}
            </span>
          </div>
          <main
            className={cn(
              "page-enter min-w-0 flex flex-1 min-h-0 flex-col rounded-3xl border border-[var(--token-color-border-default)] bg-[var(--token-color-bg-surface)] shadow-[var(--token-shadow-modal)] p-6",
              isInterviewPage && "overflow-hidden p-0",
            )}
          >
            {children}
          </main>
        </div>
      </div>
    </div>
  );
}
