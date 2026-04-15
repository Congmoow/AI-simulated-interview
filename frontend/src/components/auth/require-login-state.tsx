"use client";

import { useAuthModalStore } from "@/stores/auth-modal-store";
import { Card } from "@/components/ui/card";

export function RequireLoginState() {
  const openLogin = useAuthModalStore((state) => state.openLogin);

  return (
    <Card className="state-card">
      <div className="space-y-3">
        <p className="section-title">请先登录</p>
        <p className="text-caption max-w-[520px]">需要登录后才能查看此页面的内容。</p>
      </div>
      <button className="primary-button" onClick={() => openLogin(null)} type="button">
        立即登录
      </button>
    </Card>
  );
}
