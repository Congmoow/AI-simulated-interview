"use client";

import { useEffect, useState } from "react";
import { RequireLoginState } from "@/components/auth/require-login-state";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState, LoadingState } from "@/components/ui/state-panel";
import { AI_PROVIDER_OPTIONS, getAiProviderOption } from "@/lib/ai-provider-options";
import {
  getAiSettings,
  testAiConnection,
  updateAiSettings,
} from "@/services/ai-settings-service";
import { useAuthModalStore } from "@/stores/auth-modal-store";
import { useAuthStore } from "@/stores/auth-store";

const DEFAULT_SYSTEM_PROMPT = `你是一个资深技术面试评估师。限定返回格式为单一 JSON 对象，绝对不要输出任何 JSON 以外的内容。
JSON 必须包含以下字段：
- overallScore: 整体得分 0-100 的数字
- dimensions: 各维度评分对象，每个字段包含 score(分数) 和 detail(详细评价)
- strengths: 优势列表（字符串数组）
- weaknesses: 不足列表（字符串数组）
- suggestions: 具体改进建议列表（字符串数组）
- summary: 总结性评价（字符串）`;

type TestResultState = {
  success: boolean;
  message: string;
  latencyMs?: number | null;
};

type FormState = {
  provider: string;
  baseUrl: string;
  model: string;
  apiKey: string;
  isEnabled: boolean;
  temperature: number;
  maxTokens: number;
  systemPrompt: string;
};

const DEFAULT_FORM: FormState = {
  provider: "openai_compatible",
  baseUrl: "",
  model: "",
  apiKey: "",
  isEnabled: false,
  temperature: 0.7,
  maxTokens: 2048,
  systemPrompt: DEFAULT_SYSTEM_PROMPT,
};

const LEGACY_QWEN_BASE_URLS = new Set([
  "https://dashscope.aliyuncs.com/compatible-mode/v1",
  "https://dashscope-intl.aliyuncs.com/compatible-mode/v1",
  "https://dashscope-us.aliyuncs.com/compatible-mode/v1",
]);

export default function AiSettingsPage() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const hydrated = useAuthStore((state) => state.hydrated);
  const user = useAuthStore((state) => state.user);
  const openLogin = useAuthModalStore((state) => state.openLogin);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [testResult, setTestResult] = useState<TestResultState | null>(null);
  const [maskedKey, setMaskedKey] = useState<string | null>(null);
  const [isKeyConfigured, setIsKeyConfigured] = useState(false);
  const [form, setForm] = useState<FormState>(DEFAULT_FORM);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (!accessToken) {
      openLogin({ type: "navigate", target: "/admin/ai-settings" });
      return;
    }

    if (user?.role !== "admin") {
      setLoading(false);
      return;
    }

    void (async () => {
      try {
        const data = await getAiSettings();
        const providerValue = getAiProviderOption(data.provider)?.value ?? "openai_compatible";
        const normalizedBaseUrl =
          providerValue === "qwen" && LEGACY_QWEN_BASE_URLS.has(data.baseUrl)
            ? "https://cn-hongkong.dashscope.aliyuncs.com/compatible-mode/v1"
            : data.baseUrl;
        setForm({
          provider: providerValue,
          baseUrl: normalizedBaseUrl,
          model: data.model,
          apiKey: "",
          isEnabled: data.isEnabled,
          temperature: data.temperature,
          maxTokens: data.maxTokens,
          systemPrompt: data.systemPrompt || DEFAULT_SYSTEM_PROMPT,
        });
        setMaskedKey(data.apiKeyMasked);
        setIsKeyConfigured(data.isKeyConfigured);
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "加载 AI 配置失败");
      } finally {
        setLoading(false);
      }
    })();
  }, [accessToken, hydrated, openLogin, user]);

  function updateForm<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function handleProviderChange(provider: string) {
    const selectedProvider = getAiProviderOption(provider);
    setForm((current) => ({
      ...current,
      provider,
      baseUrl: selectedProvider?.baseUrl ?? current.baseUrl,
    }));
  }

  async function handleSave() {
    setSaving(true);
    setSaveSuccess(false);
    setError(null);
    setTestResult(null);

    try {
      const trimmedApiKey = form.apiKey.trim();
      const payload = {
        provider: form.provider,
        baseUrl: form.baseUrl.trim(),
        model: form.model.trim(),
        isEnabled: form.isEnabled,
        temperature: form.temperature,
        maxTokens: form.maxTokens,
        systemPrompt: form.systemPrompt,
        ...(trimmedApiKey ? { apiKey: trimmedApiKey } : {}),
      };

      const saved = await updateAiSettings(payload);
      setMaskedKey(saved.apiKeyMasked);
      setIsKeyConfigured(saved.isKeyConfigured);
      setForm((current) => ({ ...current, apiKey: "" }));
      setSaveSuccess(true);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "保存 AI 配置失败");
    } finally {
      setSaving(false);
    }
  }

  async function handleTest() {
    setTesting(true);
    setError(null);
    setTestResult(null);

    try {
      const result = await testAiConnection({
        provider: form.provider,
        baseUrl: form.baseUrl.trim() || undefined,
        model: form.model.trim() || undefined,
        apiKey: form.apiKey.trim() || undefined,
      });

      setTestResult({
        success: result.success,
        message: result.success
          ? `连接成功，耗时 ${result.latencyMs ?? "-"} ms`
          : (result.errorMessage ?? "连接测试失败"),
        latencyMs: result.latencyMs,
      });
    } catch (requestError) {
      setTestResult({
        success: false,
        message: requestError instanceof Error ? requestError.message : "连接测试请求失败",
      });
    } finally {
      setTesting(false);
    }
  }

  if (!hydrated) {
    return <LoadingState label="正在准备 AI 配置页面..." />;
  }

  if (!accessToken) {
    return <RequireLoginState />;
  }

  if (loading) {
    return <LoadingState label="正在加载 AI 配置..." />;
  }

  if (user?.role !== "admin") {
    return (
      <EmptyState
        title="你没有后台访问权限"
        description="当前账号不是管理员，请使用管理员账号登录后再访问此页面。"
      />
    );
  }

  const currentProvider = getAiProviderOption(form.provider);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
        <div>
          <h1 className="text-lg font-semibold">AI 大模型配置</h1>
          <p className="text-caption mt-1">
            本页只配置报告生成阶段使用的真实 LLM；未启用或未配置 API Key 时，系统会降级到 Python ai-service。
          </p>
        </div>
        <div className="rounded-[var(--token-radius-lg)] border border-[var(--token-color-border-default)] px-4 py-3 text-sm">
          <p className="font-medium text-[var(--token-color-text-primary)]">
            当前 API Key 状态：{isKeyConfigured ? "已配置" : "未配置"}
          </p>
          <p className="text-caption mt-1">
            {maskedKey ? `当前已保存：${maskedKey}` : "当前未保存 API Key"}
          </p>
        </div>
      </div>

      {error ? <p className="text-sm text-error">{error}</p> : null}
      {saveSuccess ? <p className="text-sm text-success">AI 配置已保存。</p> : null}
      {testResult ? (
        <p className={`text-sm ${testResult.success ? "text-success" : "text-error"}`}>
          {testResult.message}
        </p>
      ) : null}

      <Card className="space-y-4">
        <span className="section-label">Provider 设置</span>

        <div className="grid gap-4 md:grid-cols-2">
          <div className="flex flex-col gap-1">
            <label className="text-caption">Provider 类型</label>
            <select
              className="input-shell"
              value={form.provider}
              onChange={(event) => handleProviderChange(event.target.value)}
            >
              {AI_PROVIDER_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
            <p className="text-caption">
              {currentProvider?.description ?? "选择供应商后会自动填充官方 Base URL。"}
            </p>
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-caption">启用真实 LLM</label>
            <label className="flex h-10 items-center gap-2">
              <input
                type="checkbox"
                className="h-4 w-4 cursor-pointer"
                checked={form.isEnabled}
                onChange={(event) => updateForm("isEnabled", event.target.checked)}
              />
              <span className="text-sm">
                {form.isEnabled ? "已启用真实 LLM" : "未启用，当前会走 Python ai-service 降级路径"}
              </span>
            </label>
          </div>
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-caption">Base URL</label>
          <input
            className="input-shell"
            placeholder="https://api.openai.com/v1"
            value={form.baseUrl}
            onChange={(event) => updateForm("baseUrl", event.target.value)}
          />
          <p className="text-caption">
            选择供应商后会自动填充官方 Base URL；如果你接的是代理、网关或私有部署，也可以继续手动修改。
          </p>
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-caption">Model</label>
          <input
            className="input-shell"
            placeholder="例如：gpt-4.1-mini / deepseek-chat / Qwen/Qwen3-32B"
            value={form.model}
            onChange={(event) => updateForm("model", event.target.value)}
          />
        </div>

        <div className="flex flex-col gap-2">
          <label className="text-caption">API Key</label>
          <input
            type="password"
            className="input-shell"
            placeholder={isKeyConfigured ? "输入新的 API Key 以覆盖旧值" : "请输入新的 API Key"}
            value={form.apiKey}
            onChange={(event) => updateForm("apiKey", event.target.value)}
            autoComplete="new-password"
          />
          <p className="text-caption">
            留空表示保留当前 API Key，不会清空已保存的 key；只有输入新的非空 key，保存时才会传给后端。
          </p>
          {maskedKey === "Reques...500" ? (
            <p className="text-sm text-error">
              当前已保存的 key 看起来不是有效 API Key，而是一段旧错误消息。请重新输入真实的 DashScope API Key 后再保存。
            </p>
          ) : null}
          <p className="text-caption">
            测试连接时，如果当前输入了新的 key，会优先使用当前输入值；如果留空，则使用后端已保存的 key 测试连接。
          </p>
        </div>
      </Card>

      <Card className="space-y-4">
        <span className="section-label">调用参数</span>

        <div className="grid gap-4 md:grid-cols-2">
          <div className="flex flex-col gap-1">
            <label className="text-caption">Temperature（0 ~ 2）</label>
            <input
              type="number"
              className="input-shell"
              min={0}
              max={2}
              step={0.1}
              value={form.temperature}
              onChange={(event) => updateForm("temperature", Number(event.target.value) || 0.7)}
            />
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-caption">Max Tokens</label>
            <input
              type="number"
              className="input-shell"
              min={256}
              max={16384}
              step={256}
              value={form.maxTokens}
              onChange={(event) => updateForm("maxTokens", Number.parseInt(event.target.value, 10) || 2048)}
            />
          </div>
        </div>
      </Card>

      <Card className="space-y-4">
        <span className="section-label">报告生成系统提示词</span>
        <textarea
          className="input-shell min-h-[140px] font-mono text-xs"
          value={form.systemPrompt}
          onChange={(event) => updateForm("systemPrompt", event.target.value)}
          placeholder="系统提示词会注入到真实 LLM 的报告生成请求中。"
        />
      </Card>

      <div className="flex flex-wrap gap-3">
        <Button onClick={handleSave} disabled={saving}>
          {saving ? "正在保存..." : "保存配置"}
        </Button>
        <Button variant="secondary" onClick={handleTest} disabled={saving || testing}>
          {testing ? "正在测试..." : "测试连接"}
        </Button>
      </div>
    </div>
  );
}
