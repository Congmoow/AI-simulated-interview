import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, extname, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDir, '..', '..');
const frontendDir = resolve(repoRoot, 'frontend');
const frontendSrcDir = resolve(frontendDir, 'src');
const tsxPackageJsonPath = require.resolve('tsx/package.json', { paths: [frontendDir] });
const tsxPackageJson = JSON.parse(readFileSync(tsxPackageJsonPath, 'utf8'));
const tsxCliPath = resolve(dirname(tsxPackageJsonPath), tsxPackageJson.bin);

function collectTests(directory) {
  const entries = readdirSync(directory, { withFileTypes: true });
  const tests = [];

  for (const entry of entries) {
    const fullPath = resolve(directory, entry.name);
    if (entry.isDirectory()) {
      tests.push(...collectTests(fullPath));
      continue;
    }

    if (!entry.isFile()) {
      continue;
    }

    if (!entry.name.includes('.test.')) {
      continue;
    }

    const extension = extname(entry.name);
    if (extension === '.ts' || extension === '.tsx') {
      tests.push(fullPath);
    }
  }

  return tests;
}

if (!existsSync(frontendSrcDir) || !statSync(frontendSrcDir).isDirectory()) {
  console.error(`未找到前端源码目录：${frontendSrcDir}`);
  process.exit(1);
}

if (!existsSync(tsxCliPath)) {
  console.error(`未找到 tsx CLI 入口：${tsxCliPath}`);
  process.exit(1);
}

const testFiles = collectTests(frontendSrcDir).sort();

if (testFiles.length === 0) {
  console.error('未找到任何前端测试文件，已终止以避免空跑假绿。');
  process.exit(1);
}

const result = spawnSync(
  process.execPath,
  [tsxCliPath, '--test', ...testFiles],
  {
    cwd: frontendDir,
    stdio: 'inherit',
  },
);

if (result.error) {
  console.error(`前端测试执行失败：${result.error.message}`);
  process.exit(1);
}

process.exit(result.status ?? 1);
