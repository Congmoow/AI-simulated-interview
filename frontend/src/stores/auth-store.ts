"use client";

import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import type { CurrentUser } from "@/types/api";

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
  persist(
    (set) => ({
      accessToken: null,
      refreshToken: null,
      expiresIn: null,
      user: null,
      hydrated: false,
      setSession: (payload) =>
        set({
          accessToken: payload.accessToken,
          refreshToken: payload.refreshToken,
          expiresIn: payload.expiresIn,
          user: payload.user,
        }),
      clearSession: () =>
        set({
          accessToken: null,
          refreshToken: null,
          expiresIn: null,
          user: null,
        }),
      markHydrated: () => set({ hydrated: true }),
    }),
    {
      name: "ai-interview-auth-store",
      storage: createJSONStorage(() => localStorage),
      onRehydrateStorage: () => (state) => {
        state?.markHydrated();
      },
    },
  ),
);
