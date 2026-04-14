"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/cn";

const navItems = [
  { href: "/dashboard", label: "仪表盘", hint: "岗位与入口" },
  { href: "/interview", label: "模拟面试", hint: "实时问答" },
  { href: "/report", label: "报告中心", hint: "复盘诊断" },
  { href: "/history", label: "历史趋势", hint: "成长轨迹" },
  { href: "/admin", label: "管理后台", hint: "题库与知识库" },
];

export function SideNav() {
  const pathname = usePathname();

  return (
    <aside className="surface-card sticky top-6 flex h-[calc(100vh-3rem)] w-full max-w-[288px] flex-col justify-between overflow-hidden p-6">
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
          {navItems.map((item) => {
            const active =
              pathname === item.href ||
              (item.href !== "/dashboard" && pathname.startsWith(item.href));
            return (
              <Link
                className={cn(
                  "block rounded-[var(--token-radius-xl)] border px-4 py-4 transition-all",
                  active
                    ? "border-[rgba(0,102,255,0.22)] bg-[rgba(0,102,255,0.08)]"
                    : "border-transparent bg-[rgba(17,24,39,0.03)] hover:border-[rgba(17,24,39,0.08)] hover:bg-white",
                )}
                href={item.href}
                key={item.href}
              >
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-[length:var(--token-font-size-base)]">
                    {item.label}
                  </span>
                  {active ? (
                    <span className="status-pill bg-[rgba(0,102,255,0.12)] text-primary">
                      当前
                    </span>
                  ) : null}
                </div>
                <p className="text-caption mt-2">{item.hint}</p>
              </Link>
            );
          })}
        </nav>
      </div>
      <div className="surface-muted space-y-3 p-4">
        <p className="section-label">MVP 范围</p>
        <p className="text-caption">
          当前版本先保证登录、岗位选择、两轮问答、报告、历史和基础管理链路可演示。
        </p>
      </div>
    </aside>
  );
}
