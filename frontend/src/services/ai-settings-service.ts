import { getRequest, putRequest, postRequest } from "@/services/request";

export interface AiSettingsDto {
  id: string;
  provider: string;
  baseUrl: string;
  model: string;
  isKeyConfigured: boolean;
  apiKeyMasked: string | null;
  isEnabled: boolean;
  temperature: number;
  maxTokens: number;
  systemPrompt: string;
  updatedBy: string;
  updatedAt: string;
}

export interface UpdateAiSettingsPayload {
  provider: string;
  baseUrl: string;
  model: string;
  apiKey?: string;
  isEnabled: boolean;
  temperature: number;
  maxTokens: number;
  systemPrompt: string;
}

export interface TestAiConnectionPayload {
  provider?: string;
  baseUrl?: string;
  model?: string;
  apiKey?: string;
}

export interface AiTestResult {
  success: boolean;
  errorMessage: string | null;
  latencyMs: number | null;
}

export function getAiSettings() {
  return getRequest<AiSettingsDto>("/api/v1/admin/ai-settings");
}

export function updateAiSettings(payload: UpdateAiSettingsPayload) {
  return putRequest<AiSettingsDto>("/api/v1/admin/ai-settings", payload);
}

export function testAiConnection(payload?: TestAiConnectionPayload) {
  return postRequest<AiTestResult>("/api/v1/admin/ai-settings/test", payload ?? {});
}
