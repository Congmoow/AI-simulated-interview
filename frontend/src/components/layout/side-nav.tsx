"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useState } from "react";

import { cn } from "@/lib/cn";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { useAuthStore } from "@/stores/auth-store";
import { writeStoredAuth } from "@/utils/storage";

const navItems = [
  { href: "/dashboard", label: "个人画像", hint: "当前能力分析" },
  { href: "/interview", label: "模拟面试", hint: "实时问答" },
  { href: "/reports", label: "报告中心", hint: "复盘诊断" },
  { href: "/resources", label: "学习资源", hint: "推荐内容" },
  { href: "/admin", label: "管理后台", hint: "题库与知识库" },
];

export function SideNav() {
  const pathname = usePathname();
  const router = useRouter();
  const user = useAuthStore((state) => state.user);
  const clearSession = useAuthStore((state) => state.clearSession);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const [menuOpen, setMenuOpen] = useState(false);

  function handleLogout() {
    clearSession();
    writeStoredAuth(null);
    setMenuOpen(false);
    void router.push("/");
  }

  return (
    <>
      <aside className="sticky top-4 flex h-[calc(100vh-2rem)] w-full max-w-[288px] flex-col justify-between overflow-y-auto p-6">
        <div className="space-y-6">
          <div className="space-y-3">
            <span className="section-label">AI Interview OS</span>
            <div>
              <p className="section-title">面试演练台</p>
              <p className="text-caption mt-2">
                把岗位准备、实战演练、复盘与训练计划放进同一条闭环。
              </p>
            </div>
          </div>
          <nav className="space-y-3">
            {navItems
              .filter((item) => item.href !== "/admin" || user?.role?.toLowerCase() === "admin")
              .map((item) => {
                const active =
                  pathname === item.href ||
                  (item.href !== "/dashboard" && pathname.startsWith(item.href));

                return (
                  <Link
                    className={cn("nav-entry", active ? "nav-entry--active" : "")}
                    href={item.href}
                    key={item.href}
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-semibold text-[length:var(--token-font-size-sm)]">
                        {item.label}
                      </span>
                    </div>
                    <p className="text-caption mt-1">{item.hint}</p>
                  </Link>
                );
              })}
          </nav>
        </div>
        <div className="relative">
          <button
            className={cn(
              "flex w-full items-center gap-2 rounded-[var(--token-radius-lg)] border-0 bg-transparent px-3 py-3 text-left transition-all duration-150 hover:translate-x-1 hover:bg-[rgba(17,24,39,0.04)]",
              menuOpen ? "bg-[rgba(17,24,39,0.04)]" : "",
            )}
            onClick={() => setMenuOpen((v) => !v)}
            type="button"
          >
            <svg
              fill="none"
              height="15"
              stroke="currentColor"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="2"
              viewBox="0 0 24 24"
              width="15"
              xmlns="http://www.w3.org/2000/svg"
            >
              <path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z" />
              <circle cx="12" cy="12" r="3" />
            </svg>
            <span className="font-semibold text-[length:var(--token-font-size-sm)]">设置</span>
          </button>
          {menuOpen && (
            <div className="dropdown-panel absolute bottom-full left-0 mb-1 w-full">
              {user ? (
                <>
                  <div className="border-b border-[var(--token-color-border-default)] px-4 py-3">
                    <p className="text-[length:var(--token-font-size-xs)] text-[var(--token-color-text-tertiary)]">
                      已登录账户
                    </p>
                    <p className="mt-0.5 font-semibold text-[length:var(--token-font-size-sm)] text-[var(--token-color-text-primary)]">
                      {user.username}
                    </p>
                  </div>
                  {user.role?.toLowerCase() === "admin" ? (
                    <Link
                      className="block w-full px-4 py-2.5 text-left text-[length:var(--token-font-size-sm)] text-[var(--token-color-text-primary)] transition-colors hover:bg-[rgba(17,24,39,0.04)]"
                      href="/admin/ai-settings"
                      onClick={() => setMenuOpen(false)}
                    >
                      AI 配置
                    </Link>
                  ) : null}
                  <button
                    className="w-full px-4 py-2.5 text-left text-[length:var(--token-font-size-sm)] text-[var(--token-color-text-primary)] transition-colors hover:bg-[rgba(17,24,39,0.04)]"
                    onClick={handleLogout}
                    type="button"
                  >
                    退出登录
                  </button>
                </>
              ) : (
                <button
                  className="w-full px-4 py-2.5 text-left text-[length:var(--token-font-size-sm)] text-[var(--token-color-text-primary)] transition-colors hover:bg-[rgba(17,24,39,0.04)]"
                  onClick={() => {
                    setMenuOpen(false);
                    openLogin(null);
                  }}
                  type="button"
                >
                  去登录
                </button>
              )}
            </div>
          )}
        </div>
      </aside>
    </>
  );
}
