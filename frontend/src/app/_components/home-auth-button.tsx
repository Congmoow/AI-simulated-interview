"use client";

import { useRequireAuth } from "@/hooks/use-require-auth";

export function HomeAuthButton() {
  const requireAuth = useRequireAuth();

  return (
    <button
      className="primary-button"
      onClick={() => requireAuth({ navigateTo: "/dashboard" })}
      type="button"
    >
      开始体验
    </button>
  );
}
