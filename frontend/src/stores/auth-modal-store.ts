import { create } from "zustand";

export type AuthModalMode = "login" | "register";

export type PendingAction =
  | { type: "navigate"; target: string }
  | { type: "callback"; callbackId: string }
  | null;

interface AuthModalState {
  open: boolean;
  mode: AuthModalMode;
  pendingAction: PendingAction;
  openLogin: (action?: PendingAction) => void;
  openRegister: (action?: PendingAction) => void;
  close: () => void;
  setMode: (mode: AuthModalMode) => void;
  clearPendingAction: () => void;
}

export const useAuthModalStore = create<AuthModalState>((set) => ({
  open: false,
  mode: "login",
  pendingAction: null,
  openLogin: (action) => set({ open: true, mode: "login", pendingAction: action ?? null }),
  openRegister: (action) => set({ open: true, mode: "register", pendingAction: action ?? null }),
  close: () => set({ open: false }),
  setMode: (mode) => set({ mode }),
  clearPendingAction: () => set({ pendingAction: null }),
}));
