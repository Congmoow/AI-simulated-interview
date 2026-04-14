import { spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const mode = process.argv[2] ?? 'dev';

function runScript(scriptPath, ...args) {
  const result = spawnSync(process.execPath, [resolve(repoRoot, scriptPath), ...args], {
    cwd: repoRoot,
    encoding: 'utf8',
    shell: false,
    stdio: 'inherit',
  });

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

runScript('scripts/ensure-local-infra.mjs', mode);
runScript('scripts/bootstrap-frontend.mjs');
