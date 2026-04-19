import axios from "axios";
import { useAuthStore } from "@/stores/auth-store";
import { useAuthModalStore } from "@/stores/auth-modal-store";

const apiBaseUrl =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

export const httpClient = axios.create({
  baseURL: apiBaseUrl,
  headers: {
    "Content-Type": "application/json",
  },
});

httpClient.interceptors.request.use((config) => {
  const accessToken = useAuthStore.getState().accessToken;
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

let handling401 = false;

httpClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    const status =
      typeof error === "object" &&
      error !== null &&
      "response" in error &&
      typeof (error as { response?: { status?: number } }).response?.status === "number"
        ? (error as { response: { status: number } }).response.status
        : null;

    if (status === 401 && !handling401) {
      handling401 = true;
      useAuthStore.getState().clearSession();
      useAuthModalStore.getState().openLogin(null);
      setTimeout(() => {
        handling401 = false;
      }, 1000);
    }

    return Promise.reject(error);
  },
);

export function buildApiUrl(path: string): string {
  return `${apiBaseUrl}${path}`;
}
