import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
const [name, argumentFile] = process.argv.slice(2);
if (!name) throw new Error('Usage: node scripts/mcp-call.mjs tool_name [arguments.json]');
const client = new Client({ name: 'city-director-local-validation', version: '0.2.0' });
try {
  await client.connect(new StdioClientTransport({ command: process.execPath,
    args: [fileURLToPath(new URL('../src/harness/server.mjs', import.meta.url))], stderr: 'inherit' }));
  const args = argumentFile ? JSON.parse(await readFile(argumentFile, 'utf8')) : {};
  const response = await client.callTool({ name, arguments: args }, undefined, { timeout: 60000 });
  console.log(JSON.stringify(response.structuredContent ?? response, null, 2));
  if (response.isError) process.exitCode = 1;
} finally { await client.close(); }
