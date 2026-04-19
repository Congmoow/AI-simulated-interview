const AUTH_STORAGE_KEY = "ai-interview-auth";

export function clearLegacyAuthStorage(): void {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(AUTH_STORAGE_KEY);
  window.sessionStorage.removeItem(AUTH_STORAGE_KEY);
}
