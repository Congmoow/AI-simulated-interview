import axios from "axios";

interface ErrorPayload {
  message?: unknown;
  errors?: Array<{
    message?: unknown;
  }>;
}

export function getRequestErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const payload = error.response?.data as ErrorPayload | undefined;
    if (typeof payload?.message === "string" && payload.message.trim()) {
      return payload.message;
    }

    const firstError = payload?.errors?.find(
      (item) => typeof item?.message === "string" && item.message.trim(),
    );
    if (typeof firstError?.message === "string" && firstError.message.trim()) {
      return firstError.message;
    }
  }

  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
}
