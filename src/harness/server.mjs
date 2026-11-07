import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { z } from 'zod';
import { readFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import { callBridge } from './bridge.mjs';
import { AssetLibrary, validateAsset } from './assets.mjs';

const annotations = { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false };
const result = value => ({ content: [{ type: 'text', text: JSON.stringify(value) }], structuredContent: value });

export function createServer({ bridge = callBridge, library = new AssetLibrary({ bridge }), validate = validateAsset } = {}) {
  const server = new McpServer({ name: 'city-director', version: '0.3.4' });
  const safe = fn => async args => {
    try { const value = await fn(args); return { ...result(value), ...(value.ok === false ? { isError: true } : {}) }; }
    catch (error) { return { ...result({ code: error.code ?? 'asset_error', message: error.message }), isError: true }; }
  };
  const invoke = (method, params = {}) => async () => {
    try { return result(await bridge(method, params)); }
    catch (error) { return { ...result({ code: error.code ?? 'internal_error', message: error.message }), isError: true }; }
  };
  server.registerTool('director_status', {
    description: 'Check the installed CityDirector bridge and its actual implemented capabilities. A ping does not prove a city is loaded.',
    inputSchema: {}, annotations,
  }, invoke('bridge.ping'));
  server.registerTool('director_game_state', {
    description: 'Read city loaded/loading status, city name, pause state and simulation speed on the game thread.',
    inputSchema: {}, annotations,
  }, invoke('game.state'));
  server.registerTool('director_find_assets', {
    description: 'Search loaded game prefab names and types. This does not place assets or guarantee an asset is placeable.',
    inputSchema: { query: z.string().max(120).default(''), limit: z.number().int().min(1).max(100).default(20) }, annotations,
  }, ({ query, limit }) => invoke('assets.search', { query, limit })());
  server.registerTool('director_reference_sources', {
    description: 'Read the pinned local reference repository inventory and license observations. Works without the game.',
    inputSchema: {}, annotations,
  }, async () => {
    try { return result(JSON.parse(await readFile(new URL('../../references/sources.lock.json', import.meta.url), 'utf8'))); }
    catch (error) { return { ...result({ code: 'catalog_unavailable', message: error.message }), isError: true }; }
  });
  const mutable = { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false };
  const id = z.string().regex(/^CD_[A-Za-z][A-Za-z0-9]{0,39}[a-f0-9]{32}$/);
  server.registerTool('director_validate_asset', {
    description: 'Validate a Blender-exported static-object FBX and textures without importing. Checks baked scale, materials, names and package contents.',
    inputSchema: { directory: z.string().min(1) }, annotations,
  }, safe(({ directory }) => validate(directory)));
  server.registerTool('director_stage_asset', {
    description: 'Validate and preserve a NEW immutable asset package, source .blend, recipe and preview in the managed library. Visible in the game asset manager; does not import or place it. Use a new UUID for every revision.',
    inputSchema: { directory: z.string().min(1), title: z.string().min(1).max(120), description: z.string().max(2000).default(''),
      sourceBlend: z.string().optional(), recipe: z.string().optional(), preview: z.string().optional(), parentAssetId: id.optional() }, annotations: mutable,
  }, safe(args => library.stage(args)));
  server.registerTool('director_import_asset', {
    description: 'Submit a previously staged static object to the running city native importer. Additive only; never overwrites, places or deletes. Returns a durable job receipt; poll status. A timeout is NOT a failed import: query the same asset id before doing anything else.',
    inputSchema: { assetId: id }, annotations: { ...mutable, idempotentHint: true },
  }, safe(({ assetId }) => library.import(assetId)));
  server.registerTool('director_import_status', {
    description: 'Read durable import receipt and whether the prefab is registered now. imported does NOT mean save/reload tested. Interrupted imports are retained and never automatically retried.',
    inputSchema: { assetId: id }, annotations,
  }, safe(({ assetId }) => library.status(assetId)));
  server.registerTool('director_asset_library', {
    description: 'List managed assets, provenance, checksums, archive state and last recorded import receipts; works offline. Use import_status for live state.',
    inputSchema: { includeArchived: z.boolean().default(false) }, annotations,
  }, safe(args => library.list(args)));
  server.registerTool('director_archive_asset', {
    description: 'Hide or show a library entry (legacy archive API). Retains all files and does not reclaim disk space. Users can separately confirm deletion of managed copies in the game asset manager; Agent originals are never deleted.',
    inputSchema: { assetId: id, archived: z.boolean() }, annotations: { ...mutable, idempotentHint: true },
  }, safe(({ assetId, archived }) => library.archive(assetId, archived)));
  server.registerTool('director_menu_tree', {
    description: 'Query live native bottom-toolbar menus and asset categories, including game/mod categories, localized names, exact category IDs, locks and entry counts. Query before choosing a category. Read-only; requires a loaded city.',
    inputSchema: {}, annotations,
  }, invoke('menus.tree'));
  const categoryId = z.string().min(1).max(256);
  server.registerTool('director_menu_assets', {
    description: 'List actual registered entries in a build-menu category with search and pagination. Indicates managed imports, icons and locks. Results precede the player\'s theme/pack filters; do not claim on-screen visibility from this alone.',
    inputSchema: { categoryId, query: z.string().max(120).default(''), offset: z.number().int().min(0).max(1000000).default(0), limit: z.number().int().min(1).max(100).default(50) }, annotations,
  }, args => invoke('menus.assets', args)());
  server.registerTool('director_assign_asset_category', {
    description: 'Hot-assign an already imported managed asset to one exact existing native build-menu category returned by menu_tree. Replaces previous membership, updates name/description/icon and refreshes the UI. Persists configuration for replay after restart. Does not place objects, change gameplay functionality or edit base-game/third-party assets. Missing categories and pending deletions are rejected. Query menu_assets to verify.',
    inputSchema: { assetId: id, categoryId, title: z.string().min(1).max(120).optional(), description: z.string().max(2000).optional(), priority: z.number().int().min(-10000).max(10000).optional() },
    annotations: { ...mutable, idempotentHint: true },
  }, args => invoke('menus.assign', args)());
  return server;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  await createServer().connect(new StdioServerTransport());
}
