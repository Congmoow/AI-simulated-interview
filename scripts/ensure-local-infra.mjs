import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const envFilePath = resolve(repoRoot, '.env.run');
const composeArgs = ['compose', '--env-file', '.env.run', 'up', '-d', 'postgres', 'redis'];
const healthCheckTimeoutMs = 120_000;
const pollIntervalMs = 2_000;
const mode = process.argv[2] ?? 'dev';

function run(command, args, options = {}) {
  return spawnSync(command, args, {
    cwd: repoRoot,
    encoding: 'utf8',
    shell: false,
    ...options,
  });
}

function getHealthStatus(containerName) {
  const result = run('docker', [
    'inspect',
    '-f',
    '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}',
    containerName,
  ]);

  if (result.status !== 0) {
    return null;
  }

  return result.stdout.trim();
}

function isContainerActive(containerName) {
  const status = getHealthStatus(containerName);
  return status === 'running' || status === 'healthy' || status === 'starting' || status === 'restarting' || status === 'paused';
}

async function waitForHealthy(containerName, label) {
  const deadline = Date.now() + healthCheckTimeoutMs;

  while (Date.now() < deadline) {
    const status = getHealthStatus(containerName);
    if (status === 'healthy') {
      console.log(`${label} 已就绪。`);
      return;
    }

    if (status === 'exited' || status === 'dead') {
      throw new Error(`${label} 容器已退出，请先检查 Docker 日志。`);
    }

    await new Promise((resolveTimer) => setTimeout(resolveTimer, pollIntervalMs));
  }

  throw new Error(`${label} 在 ${Math.round(healthCheckTimeoutMs / 1000)} 秒内未达到 healthy 状态。`);
}

async function main() {
  if (!existsSync(envFilePath)) {
    console.error('缺少根目录 .env.run，请先复制 .env.example 为 .env.run。');
    process.exit(1);
  }

  const postgresHealth = getHealthStatus('ai-interview-postgres');
  const redisHealth = getHealthStatus('ai-interview-redis');

  if (postgresHealth !== 'healthy' || redisHealth !== 'healthy') {
    console.log('检测到 PostgreSQL 或 Redis 容器未就绪，正在通过 Docker Compose 启动...');

    const upResult = run('docker', composeArgs, { stdio: 'inherit' });
    if (upResult.status !== 0) {
      console.error('Docker Compose 启动失败。请确认 Docker Desktop 已运行，并检查 .env.run 中的数据库配置。');
      process.exit(upResult.status ?? 1);
    }
  } else {
    console.log('检测到 PostgreSQL 和 Redis 容器已就绪。');
  }

  await waitForHealthy('ai-interview-postgres', 'PostgreSQL');
  await waitForHealthy('ai-interview-redis', 'Redis');

  const blockedServices = [
    { containerName: 'ai-interview-backend', label: 'Docker Compose 后端', port: '8080' },
    { containerName: 'ai-interview-frontend', label: 'Docker Compose 前端', port: '3000' },
  ];

  if (mode === 'full') {
    blockedServices.push({ containerName: 'ai-interview-ai-service', label: 'Docker Compose AI 服务', port: '8000' });
  }

  const activeServices = blockedServices.filter((service) => isContainerActive(service.containerName));
  if (activeServices.length > 0) {
    console.error('检测到以下 Docker Compose 服务仍在运行，会占用本地联动启动所需的端口：');
    for (const service of activeServices) {
      console.error(`- ${service.label}（端口 ${service.port}）`);
    }
    console.error('请先停止完整的 Docker Compose 前后端栈后，再运行根目录本地开发命令。');
    process.exit(1);
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
});
