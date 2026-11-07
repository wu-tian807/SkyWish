import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdir, mkdtemp, writeFile, readFile, access, symlink } from 'node:fs/promises';
import { execFile, spawn } from 'node:child_process';
import { promisify } from 'node:util';
import { once } from 'node:events';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const exec = promisify(execFile);
const workspace = fileURLToPath(new URL('../', import.meta.url));
const probe = path.join(workspace, 'artifacts/deletion-probe/DeletionProbe.exe');
const worker = path.join(workspace, 'artifacts/mod/cleanup/CityDirector.Cleanup.exe');
const id = 'CD_Delete0123456789abcdef0123456789abcdef';
const other = 'CD_Keep0123456789abcdef0123456789abcdef';
async function fixture() {
  const parent = path.join(workspace, 'artifacts/test-deletions');
  await mkdir(parent, { recursive: true });
  const base = await mkdtemp(path.join(parent, 'case with spaces-'));
  const root = path.join(base, 'library'), game = path.join(base, 'game');
  const original = path.join(base, 'agent-blender', 'model.blend');
  const targets = ['imports', 'sources', 'native-backups'].map(area => path.join(root, area, id, 'model.bin'));
  targets.push(path.join(game, 'ImportedData', id, 'nested', 'asset.Prefab'));
  for (const area of ['catalog', 'receipts', 'state', 'menus']) targets.push(path.join(root, area, id + '.json'));
  const sibling = path.join(root, 'imports', other, 'model.fbx');
  for (const file of [...targets, original, sibling]) { await mkdir(path.dirname(file), { recursive: true }); await writeFile(file, 'keep-until-authorized'); }
  // Deliberately hostile metadata must never determine deletion paths.
  await writeFile(path.join(root, 'catalog', id + '.json'), JSON.stringify({ id, directory: path.dirname(original), provenance: { blend: { file: original } } }));
  await writeFile(path.join(root, 'receipts', id + '.json'), JSON.stringify({ nativeDirectory: path.dirname(original), nativeBackupDirectory: path.dirname(original) }));
  return { base, root, game, targets, original, sibling };
}
const run = (mode, f, assetId = id) => exec(probe, [mode, f.root, f.game, assetId], { windowsHide: true });
test('real deletion removes only managed copies, ignores metadata paths and preserves Agent originals/sibling assets', async () => {
  const f = await fixture();
  const estimate = Number((await run('estimate', f)).stdout.trim());
  assert.ok(estimate > 0);
  assert.equal(Number((await run('execute', f)).stdout.trim()), estimate);
  for (const target of f.targets) await assert.rejects(access(target));
  assert.equal(await readFile(f.original, 'utf8'), 'keep-until-authorized');
  assert.equal(await readFile(f.sibling, 'utf8'), 'keep-until-authorized');
  assert.equal(Number((await run('execute', f)).stdout.trim()), 0, 'retry is safe after completion');
});
test('invalid ids and nested directory junctions fail preflight before deleting any asset file', async () => {
  const f = await fixture();
  await assert.rejects(run('execute', f, '../agent-blender'), /Invalid managed asset id/);
  await symlink(path.dirname(f.original), path.join(f.root, 'sources', id, 'escape'), 'junction');
  await assert.rejects(run('execute', f), /Refusing linked path/);
  for (const target of f.targets) await access(target);
  assert.equal(await readFile(f.original, 'utf8'), 'keep-until-authorized');
});
test('after-exit worker waits for the exact process, enforces game-running guard and persists results', async t => {
  const f = await fixture();
  const stop = path.join(f.base, 'release-holder');
  const holder = spawn(probe, ['hold', stop], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  t.after(() => { if (holder.exitCode === null) holder.kill(); });
  const ticks = String((await once(holder.stdout, 'data'))[0]).trim();
  const jobPath = path.join(f.root, 'deletions', id + '.json');
  await mkdir(path.dirname(jobPath), { recursive: true });
  // Preserve Int64 ticks exactly; JS numbers cannot represent .NET ticks.
  const job = JSON.stringify({ assetId: id, token: 'test-request', state: 'waiting_for_exit', processId: holder.pid, processStartTicks: 'TICKS', deletedBytes: 0 }).replace('"TICKS"', ticks);
  await writeFile(jobPath, job);
  const runtimeWorker = (await exec(probe, ['prepare-worker', f.root, worker], { windowsHide: true })).stdout.trim();
  assert.ok(runtimeWorker.startsWith(path.join(f.root, 'cleanup-runtime') + path.sep));
  const child = spawn(runtimeWorker, [f.root, f.game, id, 'test-request'], { cwd: path.dirname(runtimeWorker), windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  t.after(() => { if (child.exitCode === null) child.kill(); });
  const ended = once(child, 'exit');
  await new Promise(resolve => setTimeout(resolve, 250));
  assert.equal(child.exitCode, null, 'worker must wait');
  await run('reconcile', f);
  assert.equal(JSON.parse(await readFile(jobPath, 'utf8')).state, 'waiting_for_exit', 'an active worker must not be marked failed');
  for (const target of f.targets) await access(target);
  const gameRunning = (await exec(probe, ['game-running'], { windowsHide: true })).stdout.trim() === 'True';
  await writeFile(stop, 'exit');
  const [code] = await ended;
  const result = JSON.parse(await readFile(jobPath, 'utf8'));
  assert.equal(code, gameRunning ? 1 : 0);
  assert.equal(result.state, gameRunning ? 'failed' : 'deleted');
  if (gameRunning) for (const target of f.targets) await access(target);
  else for (const target of f.targets) await assert.rejects(access(target));
  await access(f.original);
  await access(f.sibling);
});

test('orphaned waiting/deleting jobs become retryable without deleting files, recent starts get a grace period', async () => {
  const f = await fixture();
  const jobPath = path.join(f.root, 'deletions', id + '.json');
  await mkdir(path.dirname(jobPath), { recursive: true });
  for (const state of ['waiting_for_exit', 'deleting']) {
    await writeFile(jobPath, JSON.stringify({ assetId: id, token: 'orphan', state, updatedAt: '2020-01-01T00:00:00Z', deletedBytes: 10 }));
    await run('reconcile', f);
    const job = JSON.parse(await readFile(jobPath, 'utf8'));
    assert.equal(job.state, 'failed');
    assert.match(job.error, /清理程序已停止/);
    assert.equal(job.deletedBytes, 10);
    for (const file of f.targets) await access(file);
  }
  await writeFile(jobPath, JSON.stringify({ assetId: id, token: 'starting', state: 'waiting_for_exit', updatedAt: new Date().toISOString() }));
  await run('reconcile', f);
  assert.equal(JSON.parse(await readFile(jobPath, 'utf8')).state, 'waiting_for_exit');
});
