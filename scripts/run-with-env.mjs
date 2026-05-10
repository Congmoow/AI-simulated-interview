/**
 * 从 .env.run 读取环境变量，注入到子进程中运行指定命令。
 * 用法: node scripts/run-with-env.mjs <command> [args...]
 */
import { readFileSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { spawn } from "node:child_process";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const envFile = resolve(repoRoot, ".env.run");

const env = { ...process.env };

try {
  const content = readFileSync(envFile, "utf-8");
  for (const line of content.split("\n")) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;
    const eqIndex = trimmed.indexOf("=");
    if (eqIndex < 1) continue;
    const key = trimmed.slice(0, eqIndex).trim();
    const value = trimmed.slice(eqIndex + 1).trim();
    // 不覆盖已有的系统环境变量（用户可能已通过 $env: 设置）
    if (!(key in env) || !env[key]) {
      env[key] = value;
    }
  }
} catch {
  // .env.run 不存在时忽略
}

const args = process.argv.slice(2);
if (args.length === 0) {
  console.error("用法: node scripts/run-with-env.mjs <command> [args...]");
  process.exit(1);
}

const child = spawn(args[0], args.slice(1), {
  cwd: repoRoot,
  env,
  stdio: "inherit",
  shell: true,
});

child.on("exit", (code) => process.exit(code ?? 1));
