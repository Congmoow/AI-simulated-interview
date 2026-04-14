import type { Metadata } from "next";
import "./globals.css";
import { AppShell } from "@/components/layout/app-shell";

export const metadata: Metadata = {
  title: "AI 模拟面试与能力提升软件",
  description: "聚焦模拟面试、报告复盘、成长趋势与学习推荐的一体化平台。",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="zh-CN">
      <body className="shell-background">
        <AppShell>{children}</AppShell>
      </body>
    </html>
  );
}
