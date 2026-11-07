import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { mkdir, readFile, writeFile, readdir, lstat, realpath, copyFile, rename, access } from 'node:fs/promises';
import { constants } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash, randomUUID } from 'node:crypto';

const exec = promisify(execFile);
const workspace = fileURLToPath(new URL('../../', import.meta.url));
export const defaultLibrary = path.join(workspace, 'asset-library');
const idPattern = /^CD_[A-Za-z][A-Za-z0-9]{0,39}[a-f0-9]{32}$/;
export function assetId(id) {
  if (!idPattern.test(id)) throw new Error('Invalid managed asset id.');
  return id;
}
export async function readJson(file, fallback = undefined) {
  try { return JSON.parse(await readFile(file, 'utf8')); }
  catch (e) { if (e.code === 'ENOENT' && fallback !== undefined) return fallback; throw e; }
}
async function atomicJson(file, value) {
  await mkdir(path.dirname(file), { recursive: true });
  const temp = `${file}.${randomUUID()}.tmp`;
  await writeFile(temp, JSON.stringify(value, null, 2), { flag: 'wx' });
  await rename(temp, file);
}
async function fileHash(file) { return createHash('sha256').update(await readFile(file)).digest('hex'); }
async function inventory(dir) {
  const files = [];
  for (const name of (await readdir(dir)).sort()) {
    const file = path.join(dir, name);
    const info = await lstat(file);
    if (!info.isFile() || info.isSymbolicLink()) throw new Error('Asset folder must contain regular files only.');
    if (info.size > 128 * 1024 * 1024) throw new Error('A source asset file exceeds the 128 MiB milestone limit.');
    files.push({ name, bytes: info.size, sha256: await fileHash(file) });
  }
  if (files.reduce((sum, f) => sum + f.bytes, 0) > 256 * 1024 * 1024) throw new Error('Asset exceeds 256 MiB milestone limit.');
  return files;
}
export async function validateAsset(directory) {
  const dir = await realpath(directory);
  await inventory(dir);
  const python = process.env.CITY_DIRECTOR_PYTHON ?? 'D:\\SteamLibrary\\steamapps\\common\\Blender\\5.2\\python\\bin\\python.exe';
  const { stdout } = await exec(python, [fileURLToPath(new URL('../asset-pipeline/validate.py', import.meta.url)), dir],
    { windowsHide: true, timeout: 30000, maxBuffer: 1024 * 1024 });
  return JSON.parse(stdout);
}

export class AssetLibrary {
  constructor({ root = defaultLibrary, bridge, validate = validateAsset } = {}) {
    this.root = path.resolve(root); this.bridge = bridge; this.validate = validate;
  }
  file(area, id) { return path.join(this.root, area, `${assetId(id)}.json`); }
  async stage({ directory, title, description = '', sourceBlend, recipe, preview, parentAssetId }) {
    const source = await realpath(directory);
    const id = assetId(path.basename(source));
    if (await readJson(this.file('deletions', id), null)) throw new Error('Asset has a deletion record. Use a new asset id.');
    if (parentAssetId) assetId(parentAssetId);
    const check = await this.validate(source);
    if (!check.ok) return { ok: false, validation: check };
    const files = await inventory(source);
    const existing = await readJson(this.file('catalog', id), null);
    if (existing) {
      if (JSON.stringify(existing.files) !== JSON.stringify(files)) throw new Error('Asset id is immutable. Generate a new id for changed files.');
      return { ok: true, existing: true, asset: existing };
    }
    await mkdir(path.join(this.root, 'imports'), { recursive: true });
    // Native CS2 uses the first underscore-separated token as a theme only if registered.
    const themesFile = path.join(this.root, 'imports', 'themes.json');
    const themes = await readJson(themesFile, { themePrefixes: [] });
    if (!Array.isArray(themes.themePrefixes) || themes.themePrefixes.some(x => typeof x !== 'string'))
      throw new Error('Invalid imports/themes.json themePrefixes.');
    if (!themes.themePrefixes.includes('CD'))
      await atomicJson(themesFile, { ...themes, themePrefixes: [...themes.themePrefixes, 'CD'] });
    const destination = path.join(this.root, 'imports', id);
    // Exclusive mkdir is a cross-process reservation. Interrupted staging is retained, never silently overwritten.
    await mkdir(destination);
    for (const f of files) await copyFile(path.join(source, f.name), path.join(destination, f.name), constants.COPYFILE_EXCL);
    if (JSON.stringify(await inventory(destination)) !== JSON.stringify(files)) throw new Error('Source files changed during staging; use a fresh id.');
    const provenance = {};
    const sourceDir = path.join(this.root, 'sources', id);
    await mkdir(sourceDir, { recursive: true });
    for (const [key, file, name] of [['blend', sourceBlend, 'model.blend'], ['recipe', recipe, 'recipe.py'], ['preview', preview, 'preview.png']]) {
      if (!file) continue;
      const info = await lstat(file);
      if (!info.isFile() || info.isSymbolicLink()) throw new Error('Provenance inputs must be regular files.');
      const target = path.join(sourceDir, name);
      await copyFile(file, target, constants.COPYFILE_EXCL);
      provenance[key] = { file: target, sha256: await fileHash(target) };
    }
    const manifest = { schemaVersion: 1, id, title, description, createdAt: new Date().toISOString(),
      preset: 'StaticObject', directory: destination, files, provenance, parentAssetId: parentAssetId ?? null,
      declaredDependencies: [], dependencyAudit: 'not-yet-verified-in-game', saveUsage: 'unknown', deletionAllowed: true,
      validation: check };
    await atomicJson(this.file('catalog', id), manifest);
    return { ok: true, asset: manifest };
  }
  async import(id) {
    if (await readJson(this.file('deletions', id), null)) throw new Error('Asset has a deletion request. Use a new asset id.');
    const manifest = await readJson(this.file('catalog', id));
    if ((await readJson(this.file('state', id), {})).archived) throw new Error('Archived asset cannot be imported. Restore it first.');
    if (!(await readJson(path.join(this.root, 'policy.json'), { allowImports: true })).allowImports) throw new Error('Imports are disabled in the in-game asset manager.');
    if (JSON.stringify(await inventory(manifest.directory)) !== JSON.stringify(manifest.files)) throw new Error('Staged files changed; import refused. Create a new asset version.');
    // Caller supplies only an id: the game resolves its own configured library, verifies hashes and persists its own receipt.
    return this.bridge('assets.import', { assetId: id });
  }
  async status(id) { assetId(id); return this.bridge('assets.import_status', { assetId: id }); }
  async list({ includeArchived = false } = {}) {
    let names;
    try { names = await readdir(path.join(this.root, 'catalog')); }
    catch (e) { if (e.code === 'ENOENT') return { libraryRoot: this.root, assets: [] }; throw e; }
    const assets = [];
    for (const name of names.filter(n => n.endsWith('.json')).sort()) {
      const id = name.slice(0, -5); assetId(id);
      const manifest = await readJson(this.file('catalog', id));
      const state = await readJson(this.file('state', id), {});
      if (state.archived && !includeArchived) continue;
      const receipt = await readJson(this.file('receipts', id), null);
      const deletion = await readJson(this.file('deletions', id), null);
      const menu = await readJson(this.file('menus', id), null);
      assets.push({ ...manifest, archived: !!state.archived, receipt, deletion, menu, deletionAllowed: true,
        deletionControl: 'Confirm deletion in the game asset manager; managed copies only, after game exit.' });
    }
    return { libraryRoot: this.root, assets };
  }
  async archive(id, archived) {
    await access(this.file('catalog', id));
    await atomicJson(this.file('state', id), { archived, updatedAt: new Date().toISOString() });
    return { id, archived, filesRetained: true, gamePrefabsUnchanged: true, deletionAllowed: true,
      reason: 'Hide/show only. To reclaim disk space, confirm deletion in the game asset manager.' };
  }
}
