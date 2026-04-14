const AUTH_STORAGE_KEY = "ai-interview-auth";

export interface StoredAuth {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: {
    id: string;
    username: string;
    email: string;
    role: string;
    phone?: string | null;
    avatarUrl?: string | null;
    targetPosition?: {
      code: string;
      name: string;
    } | null;
  };
}

export function readStoredAuth(): StoredAuth | null {
  if (typeof window === "undefined") {
    return null;
  }

  const rawValue = window.localStorage.getItem(AUTH_STORAGE_KEY);
  if (!rawValue) {
    return null;
  }

  try {
    return JSON.parse(rawValue) as StoredAuth;
  } catch {
    window.localStorage.removeItem(AUTH_STORAGE_KEY);
    return null;
  }
}

export function writeStoredAuth(value: StoredAuth | null): void {
  if (typeof window === "undefined") {
    return;
  }

  if (!value) {
    window.localStorage.removeItem(AUTH_STORAGE_KEY);
    return;
  }

  window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(value));
}
