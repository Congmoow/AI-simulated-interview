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

let refreshingPromise: Promise<string> | null = null;

async function refreshAccessToken(): Promise<string> {
  const { refreshToken } = useAuthStore.getState();
  if (!refreshToken) {
    throw new Error("no refresh token");
  }

  const response = await axios.post(`${apiBaseUrl}/api/v1/auth/refresh`, {
    refreshToken,
  });

  const data = response.data?.data;
  if (!data?.accessToken) {
    throw new Error("invalid refresh response");
  }

  useAuthStore.getState().setSession({
    accessToken: data.accessToken,
    refreshToken: data.refreshToken ?? refreshToken,
    expiresIn: data.expiresIn ?? 0,
    user: useAuthStore.getState().user!,
  });

  return data.accessToken as string;
}

httpClient.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    const axiosError = error as {
      response?: { status?: number; config?: { _retry?: boolean; headers?: Record<string, string> } };
      config?: { _retry?: boolean; headers?: Record<string, string> };
    };

    const status = axiosError.response?.status;
    const originalConfig = axiosError.config;

    if (status === 401 && originalConfig && !originalConfig._retry) {
      originalConfig._retry = true;

      try {
        if (!refreshingPromise) {
          refreshingPromise = refreshAccessToken().finally(() => {
            refreshingPromise = null;
          });
        }

        const newToken = await refreshingPromise;
        originalConfig.headers = originalConfig.headers ?? {};
        originalConfig.headers.Authorization = `Bearer ${newToken}`;
        return httpClient(originalConfig);
      } catch {
        useAuthStore.getState().clearSession();
        useAuthModalStore.getState().openLogin(null);
        return Promise.reject(error);
      }
    }

    if (status === 401) {
      useAuthStore.getState().clearSession();
      useAuthModalStore.getState().openLogin(null);
    }

    return Promise.reject(error);
  },
);

export function buildApiUrl(path: string): string {
  return `${apiBaseUrl}${path}`;
}
