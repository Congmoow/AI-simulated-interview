import { existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';

const frontendNodeModules = resolve('frontend', 'node_modules');

if (existsSync(frontendNodeModules)) {
  process.exit(0);
}

console.log('检测到 frontend 依赖未安装，正在执行 npm install ...');

const result = spawnSync(
  process.platform === 'win32' ? 'npm.cmd' : 'npm',
  ['install', '--prefix', 'frontend', '--no-audit', '--no-fund'],
  {
    stdio: 'inherit',
    env: {
      ...process.env,
      npm_config_cache: resolve('.npm-cache'),
    },
  },
);

if (result.error) {
  console.error(`frontend 依赖安装失败：${result.error.message}`);
  process.exit(1);
}

process.exit(result.status ?? 1);
