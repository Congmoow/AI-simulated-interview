export function getTagIconUrl(tag: string): string | null {
  const lower = tag.toLowerCase();

  const baseUrl = "https://unpkg.com/devicon@latest/icons";
  const iconUrl = (name: string, variant = "original") =>
    `${baseUrl}/${name}/${name}-${variant}.svg`;

  // ── Java 后端岗位 ──
  if (lower.includes("java")) return iconUrl("java");
  if (lower.includes("spring boot") || lower.includes("springboot") || lower === "spring") {
    return iconUrl("spring", "original");
  }
  if (lower.includes("mysql")) return iconUrl("mysql");
  if (lower.includes("redis")) return iconUrl("redis");
  if (lower.includes("微服务") || lower.includes("microservice")) {
    return iconUrl("kubernetes");
  }

  // ── Web 前端岗位 ──
  if (lower.includes("react")) return iconUrl("react");
  if (lower.includes("typescript")) return iconUrl("typescript");
  if (lower.includes("css")) return iconUrl("css3");
  if (lower.includes("echarts")) return iconUrl("echarts");
  if (lower.includes("next.js") || lower.includes("nextjs")) return iconUrl("nextjs");

  // ── 其他常见技术栈 ──
  if (lower.includes("vue")) return iconUrl("vuejs");
  if (lower.includes("python")) return iconUrl("python");
  if (lower.includes("docker")) return iconUrl("docker");
  if (lower.includes("aws") || lower.includes("云")) return iconUrl("amazonwebservices");
  if (lower.includes("go")) return iconUrl("go");
  if (lower.includes("node")) return iconUrl("nodejs");
  if (lower.includes("postgresql") || lower.includes("postgres")) return iconUrl("postgresql");
  if (lower.includes("mongodb") || lower.includes("mongo")) return iconUrl("mongodb");
  if (lower.includes("git")) return iconUrl("git");
  if (lower.includes("linux")) return iconUrl("linux");
  if (lower.includes("nginx")) return iconUrl("nginx");
  if (lower.includes("kubernetes") || lower.includes("k8s")) return iconUrl("kubernetes");

  return null;
}
