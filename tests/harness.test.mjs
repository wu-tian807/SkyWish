import test from 'node:test';
import assert from 'node:assert/strict';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { createServer } from '../src/harness/server.mjs';
import { BridgeError } from '../src/harness/bridge.mjs';
import { fileURLToPath } from 'node:url';
import { readFile } from 'node:fs/promises';

const expectedCatalog = JSON.parse(await readFile(new URL('../references/sources.lock.json', import.meta.url), 'utf8'));

test('MCP handshake, tool permissions, validation and error reporting', async t => {
  const calls = [];
  const server = createServer({ bridge: async (method, params) => {
    calls.push({ method, params });
    if (method === 'game.state') throw new BridgeError('game_unavailable', 'No city loaded');
    return { method, params };
  } });
  const client = new Client({ name: 'test', version: '1' });
  const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
  t.after(async () => { await client.close(); await server.close(); });
  await server.connect(serverTransport);
  await client.connect(clientTransport);
  const { tools } = await client.listTools();
  assert.equal(tools.length, 13);
  assert.equal(tools.find(tool => tool.name === 'director_import_asset').annotations.readOnlyHint, false);
  assert.equal(tools.find(tool => tool.name === 'director_import_status').annotations.readOnlyHint, true);
  assert.equal(tools.find(tool => tool.name === 'director_menu_tree').annotations.readOnlyHint, true);
  assert.equal(tools.find(tool => tool.name === 'director_assign_asset_category').annotations.readOnlyHint, false);
  const tree = await client.callTool({ name: 'director_menu_tree', arguments: {} });
  assert.equal(tree.structuredContent.method, 'menus.tree');
  const menu = await client.callTool({ name: 'director_menu_assets', arguments: { categoryId: 'UIAssetCategoryPrefab:PropsMisc' } });
  assert.deepEqual(menu.structuredContent.params, { categoryId: 'UIAssetCategoryPrefab:PropsMisc', query: '', offset: 0, limit: 50 });
  const assigned = await client.callTool({ name: 'director_assign_asset_category', arguments: { assetId: 'CD_Test0123456789abcdef0123456789abcdef', categoryId: 'UIAssetCategoryPrefab:PropsMisc', title: '测试长椅' } });
  assert.equal(assigned.structuredContent.method, 'menus.assign');
  const beforeMenuInvalid = calls.length;
  const invalidAssignment = await client.callTool({ name: 'director_assign_asset_category', arguments: { assetId: '../original', categoryId: 'UIAssetCategoryPrefab:PropsMisc' } });
  assert.equal(invalidAssignment.isError, true);
  const invalidPage = await client.callTool({ name: 'director_menu_assets', arguments: { categoryId: 'x', offset: -1 } });
  assert.equal(invalidPage.isError, true);
  assert.equal(calls.length, beforeMenuInvalid);
  const status = await client.callTool({ name: 'director_status', arguments: {} });
  assert.equal(status.structuredContent.method, 'bridge.ping');
  const search = await client.callTool({ name: 'director_find_assets', arguments: { query: '树' } });
  assert.deepEqual(search.structuredContent.params, { query: '树', limit: 20 });
  const before = calls.length;
  const invalid = await client.callTool({ name: 'director_find_assets', arguments: { limit: 101 } });
  assert.equal(invalid.isError, true);
  assert.equal(calls.length, before, 'Invalid parameters must not reach game');
  const failed = await client.callTool({ name: 'director_game_state', arguments: {} });
  assert.equal(failed.isError, true);
  assert.equal(failed.structuredContent.code, 'game_unavailable');
  const catalog = await client.callTool({ name: 'director_reference_sources', arguments: {} });
  assert.deepEqual(catalog.structuredContent, expectedCatalog);
});

test('real stdio MCP process starts from another working directory', async t => {
  const serverPath = fileURLToPath(new URL('../src/harness/server.mjs', import.meta.url));
  const transport = new StdioClientTransport({ command: process.execPath, args: [serverPath], cwd: fileURLToPath(new URL('../tests/', import.meta.url)), stderr: 'pipe' });
  const client = new Client({ name: 'stdio-test', version: '1' });
  t.after(() => client.close());
  await client.connect(transport);
  const catalog = await client.callTool({ name: 'director_reference_sources', arguments: {} });
  assert.deepEqual(catalog.structuredContent, expectedCatalog);
  assert.equal(catalog.isError, undefined);
});
