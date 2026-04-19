"use client";

import { create } from "zustand";
import type { CurrentUser } from "@/types/api";
import { clearLegacyAuthStorage } from "@/utils/storage";

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  expiresIn: number | null;
  user: CurrentUser | null;
  hydrated: boolean;
  setSession: (payload: {
    accessToken: string;
    refreshToken: string;
    expiresIn: number;
    user: CurrentUser;
  }) => void;
  clearSession: () => void;
  markHydrated: () => void;
}

export const useAuthStore = create<AuthState>()(
  (set) => ({
    accessToken: null,
    refreshToken: null,
    expiresIn: null,
    user: null,
    hydrated: true,
    setSession: (payload) => {
      clearLegacyAuthStorage();
      set({
        accessToken: payload.accessToken,
        refreshToken: payload.refreshToken,
        expiresIn: payload.expiresIn,
        user: payload.user,
      });
    },
    clearSession: () => {
      clearLegacyAuthStorage();
      set({
        accessToken: null,
        refreshToken: null,
        expiresIn: null,
        user: null,
      });
    },
    markHydrated: () => undefined,
  }),
);
