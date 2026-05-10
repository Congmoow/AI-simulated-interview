"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { AuthModal } from "@/components/auth/auth-modal";
import { useAuthModalStore } from "@/stores/auth-modal-store";

export function GlobalModals() {
  const router = useRouter();
  const openLogin = useAuthModalStore((state) => state.openLogin);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (params.get("auth") === "login") {
      openLogin(null);
      router.replace("/");
    }
  }, [openLogin, router]);

  return <AuthModal />;
}
