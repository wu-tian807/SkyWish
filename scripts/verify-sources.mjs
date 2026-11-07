import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const lock = JSON.parse(readFileSync(path.join(root, 'references/sources.lock.json'), 'utf8'));
let failed = 0;
for (const item of lock.repositories) {
  try {
    const cwd = path.join(root, item.directory);
    const git = (...args) => execFileSync('git', ['-C', cwd, ...args], { encoding: 'utf8', windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] }).trim();
    if (git('rev-parse', 'HEAD') !== item.commit) throw new Error('Commit changed');
    if (git('remote', 'get-url', 'origin') !== item.url) throw new Error('Origin changed');
    if (git('status', '--porcelain')) throw new Error('Working tree changed');
    git('fsck', '--full', '--no-dangling');
    for (const license of item.licenseFiles) {
      const hash = createHash('sha256').update(readFileSync(path.join(cwd, license.path))).digest('hex');
      if (hash !== license.sha256) throw new Error('License file hash changed');
    }
    console.log(`OK ${item.repository} ${item.commit.slice(0, 12)}`);
  } catch (error) { failed++; console.error(`FAIL ${item.repository}: ${error.message}`); }
}
console.log(`${lock.repositories.length - failed}/${lock.repositories.length} source repositories verified.`);
if (failed) process.exitCode = 1;
