"use client";

import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { registerAuthCallback } from "@/lib/auth-action-registry";

interface RequireAuthOptions {
  navigateTo?: string;
  onAuthed?: () => void | Promise<void>;
}

export function useRequireAuth() {
  const user = useAuthStore((state) => state.user);
  const hydrated = useAuthStore((state) => state.hydrated);
  const openLogin = useAuthModalStore((state) => state.openLogin);
  const router = useRouter();

  return function requireAuth(options: RequireAuthOptions = {}) {
    if (!hydrated) return;

    if (user) {
      if (options.navigateTo) {
        router.push(options.navigateTo);
      } else if (options.onAuthed) {
        void options.onAuthed();
      }
      return;
    }

    if (options.navigateTo) {
      openLogin({ type: "navigate", target: options.navigateTo });
    } else if (options.onAuthed) {
      const callbackId = registerAuthCallback(options.onAuthed);
      openLogin({ type: "callback", callbackId });
    } else {
      openLogin(null);
    }
  };
}
