import type { ApiResponse } from "@/types/api";
import { httpClient } from "@/services/http";

export async function unwrapResponse<T>(
  request: Promise<{ data: ApiResponse<T> }>,
): Promise<T> {
  const response = await request;
  return response.data.data;
}

export function getRequest<T>(url: string, params?: Record<string, unknown>) {
  return unwrapResponse<T>(httpClient.get(url, { params }));
}

export function postRequest<T>(url: string, body?: unknown) {
  return unwrapResponse<T>(httpClient.post(url, body));
}

export function putRequest<T>(url: string, body?: unknown) {
  return unwrapResponse<T>(httpClient.put(url, body));
}
