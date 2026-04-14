import type { CurrentUser, LoginPayload } from "@/types/api";
import { getRequest, postRequest, putRequest } from "@/services/request";

export function login(payload: { username: string; password: string }) {
  return postRequest<LoginPayload>("/api/v1/auth/login", payload);
}

export function register(payload: {
  username: string;
  password: string;
  email: string;
  phone?: string;
  targetPosition?: string;
}) {
  return postRequest("/api/v1/auth/register", payload);
}

export function getCurrentUser() {
  return getRequest<CurrentUser>("/api/v1/auth/me");
}

export function updateProfile(payload: {
  email?: string;
  phone?: string;
  targetPosition?: string;
  avatarUrl?: string;
}) {
  return putRequest<CurrentUser>("/api/v1/auth/profile", payload);
}
