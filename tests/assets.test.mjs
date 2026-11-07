import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { AssetLibrary } from '../src/harness/assets.mjs';
const id = 'CD_Test0123456789abcdef0123456789abcdef';
async function fixture() {
  const base = await mkdtemp(path.join(tmpdir(), 'city-director-assets-'));
  const source = path.join(base, 'source', id);
  await mkdir(source, { recursive: true });
  await writeFile(path.join(source, id + '.fbx'), 'test fixture; validator is injected');
  const calls = [];
  const library = new AssetLibrary({ root: path.join(base, 'library'), validate: async () => ({ ok: true }),
    bridge: async (method, params) => { calls.push({ method, params }); return { state: 'importing' }; } });
  return { base, source, library, calls };
}
test('immutable staging preserves source content and does not call game', async () => {
  const { source, library, calls } = await fixture();
  const staged = await library.stage({ directory: source, title: '长椅' });
  assert.equal(staged.ok, true);
  assert.equal(calls.length, 0);
  assert.deepEqual(JSON.parse(await readFile(path.join(library.root, 'imports', 'themes.json'), 'utf8')).themePrefixes, ['CD']);
  assert.equal((await library.stage({ directory: source, title: '长椅' })).existing, true);
  await writeFile(path.join(source, id + '.fbx'), 'changed geometry');
  await assert.rejects(library.stage({ directory: source, title: 'change' }), /immutable/);
  assert.equal(await readFile(path.join(staged.asset.directory, id + '.fbx'), 'utf8'), 'test fixture; validator is injected');
});
test('archive survives a fresh manager, retains files, blocks import and can be restored', async () => {
  const { source, library, calls } = await fixture();
  await library.stage({ directory: source, title: 'Bench' });
  await library.archive(id, true);
  const fresh = new AssetLibrary({ root: library.root });
  assert.equal((await fresh.list()).assets.length, 0);
  assert.equal((await fresh.list({ includeArchived: true })).assets[0].deletionAllowed, true);
  await assert.rejects(library.import(id), /Archived/);
  assert.equal(calls.length, 0);
  await library.archive(id, false);
  assert.equal((await library.import(id)).state, 'importing');
  assert.deepEqual(calls, [{ method: 'assets.import', params: { assetId: id } }]);
});
test('modified sealed package and user-disabled imports cannot reach the game', async () => {
  const { source, library, calls } = await fixture();
  const staged = await library.stage({ directory: source, title: 'Bench' });
  await writeFile(path.join(library.root, 'policy.json'), JSON.stringify({ allowImports: false }));
  await assert.rejects(library.import(id), /disabled/);
  await writeFile(path.join(library.root, 'policy.json'), JSON.stringify({ allowImports: true }));
  await writeFile(path.join(staged.asset.directory, id + '.fbx'), 'changed');
  await assert.rejects(library.import(id), /changed/);
  assert.equal(calls.length, 0);
});
test('bad ids and failed validation cannot create an import', async () => {
  const { source, library, calls } = await fixture();
  library.validate = async () => ({ ok: false, problems: ['bad geometry'] });
  assert.equal((await library.stage({ directory: source, title: 'Bench' })).ok, false);
  await assert.rejects(library.import('../other'), /Invalid/);
  assert.equal(calls.length, 0);
});
test('pending deletion blocks import and restaging while keeping the status visible', async () => {
  const { source, library, calls } = await fixture();
  await library.stage({ directory: source, title: 'Bench' });
  await mkdir(path.join(library.root, 'deletions'));
  await writeFile(library.file('deletions', id), JSON.stringify({ assetId: id, state: 'waiting_for_exit' }));
  await assert.rejects(library.import(id), /deletion request/);
  await assert.rejects(library.stage({ directory: source, title: 'Bench' }), /deletion record/);
  assert.equal(calls.length, 0);
  assert.equal((await library.list()).assets[0].deletion.state, 'waiting_for_exit');
});
