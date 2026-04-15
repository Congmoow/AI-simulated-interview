export type AiProviderOption = {
  value: string;
  label: string;
  baseUrl: string;
  description: string;
};

export const AI_PROVIDER_OPTIONS: AiProviderOption[] = [
  {
    value: "openai",
    label: "OpenAI",
    baseUrl: "https://api.openai.com/v1",
    description: "OpenAI 官方 API，适合直接接入 GPT 系列模型。",
  },
  {
    value: "deepseek",
    label: "DeepSeek",
    baseUrl: "https://api.deepseek.com/v1",
    description: "DeepSeek 官方 OpenAI 兼容接口。",
  },
  {
    value: "siliconflow",
    label: "SiliconFlow",
    baseUrl: "https://api.siliconflow.cn/v1",
    description: "SiliconFlow 官方 OpenAI 兼容接口。",
  },
  {
    value: "openrouter",
    label: "OpenRouter",
    baseUrl: "https://openrouter.ai/api/v1",
    description: "OpenRouter 官方 OpenAI 兼容接口，支持多模型路由。",
  },
  {
    value: "qwen",
    label: "阿里云百炼 Qwen",
    baseUrl: "https://dashscope.aliyuncs.com/compatible-mode/v1",
    description: "阿里云百炼 Qwen 官方 OpenAI 兼容接口。默认填充通用兼容域名；如果你接入专用区域或代理网关，可手动改写 Base URL。",
  },
  {
    value: "openai_compatible",
    label: "自定义 OpenAI Compatible",
    baseUrl: "",
    description: "用于兼容 OpenAI API 规范的自定义服务商，需要手动填写 Base URL。",
  },
];

export function getAiProviderOption(value: string): AiProviderOption | undefined {
  return AI_PROVIDER_OPTIONS.find((item) => item.value === value);
}
