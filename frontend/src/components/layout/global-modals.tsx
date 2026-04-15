"use client";

import { useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { AuthModal } from "@/components/auth/auth-modal";
import { useAuthModalStore } from "@/stores/auth-modal-store";

export function GlobalModals() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const openLogin = useAuthModalStore((state) => state.openLogin);

  useEffect(() => {
    if (searchParams.get("auth") === "login") {
      openLogin(null);
      router.replace("/");
    }
  }, [searchParams, openLogin, router]);

  return <AuthModal />;
}
