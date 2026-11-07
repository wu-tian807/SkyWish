import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const source = JSON.parse(readFileSync(path.join(root, 'references/sources.json'), 'utf8'));
const repositories = source.repositories.map(item => {
  const directory = `third_party/${item.repository.replace('/', '-')}`;
  const cwd = path.join(root, directory);
  const git = (...args) => execFileSync('git', ['-C', cwd, ...args], { encoding: 'utf8', windowsHide: true }).trim();
  const remote = git('remote', 'get-url', 'origin');
  if (remote !== `https://github.com/${item.repository}.git`) throw new Error(`Unexpected origin: ${item.repository}`);
  if (git('status', '--porcelain')) throw new Error(`Reference tree has changes: ${item.repository}`);
  const licenseFiles = readdirSync(cwd).filter(name => /^(LICENSE|COPYING)(\.|$)/i.test(name)).map(name => ({
    path: name, sha256: createHash('sha256').update(readFileSync(path.join(cwd, name))).digest('hex'),
  }));
  return { ...item, directory, url: remote, commit: git('rev-parse', 'HEAD'), branch: git('branch', '--show-current'),
    commitDate: git('show', '-s', '--format=%cI', 'HEAD'), shallow: git('rev-parse', '--is-shallow-repository') === 'true',
    licenseFiles, runtimeTested: false };
});
writeFileSync(path.join(root, 'references/sources.lock.json'), JSON.stringify({ ...source, capturedAt: new Date().toISOString(), repositories }, null, 2) + '\n');
console.log(`Pinned ${repositories.length} clean source repositories.`);
