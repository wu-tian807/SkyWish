import test from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { randomUUID } from 'node:crypto';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { callBridge } from '../src/harness/bridge.mjs';

const endpoint = () => process.platform === 'win32'
  ? `\\\\.\\pipe\\city-director-test-${randomUUID()}`
  : `/tmp/city-director-test-${randomUUID()}.sock`;

async function mock(t, handler) {
  const pipePath = endpoint();
  const sockets = new Set();
  const server = net.createServer(socket => {
    sockets.add(socket);
    socket.on('error', () => {});
    socket.on('close', () => sockets.delete(socket));
    let bytes = '';
    socket.on('data', data => {
      bytes += data;
      if (!bytes.includes('\n')) return;
      handler(socket, JSON.parse(bytes.split('\n')[0]));
    });
  });
  server.listen(pipePath);
  await once(server, 'listening');
  t.after(() => { for (const socket of sockets) socket.destroy(); return new Promise(resolve => server.close(resolve)); });
  return { pipePath };
}

test('pipe framing supports chunked UTF-8 responses', async t => {
  const options = await mock(t, (socket, request) => {
    const bytes = Buffer.from(JSON.stringify({ schemaVersion: 1, id: request.id, ok: true, result: { cityName: '临河城' } }) + '\n');
    const split = bytes.indexOf(Buffer.from('临')) + 1;
    socket.write(bytes.subarray(0, split));
    setImmediate(() => socket.end(bytes.subarray(split)));
  });
  assert.deepEqual(await callBridge('game.state', {}, options), { cityName: '临河城' });
});

test('pipe rejects mismatched response id', async t => {
  const options = await mock(t, socket => socket.end(JSON.stringify({ schemaVersion: 1, id: 'wrong', ok: true, result: {} }) + '\n'));
  await assert.rejects(callBridge('game.state', {}, options), { code: 'protocol_error' });
});

test('pipe reports malformed JSON, early close, remote errors and oversized replies', async t => {
  for (const [reply, code] of [
    ['oops\n', 'protocol_error'],
    ['', 'disconnected'],
    ['x'.repeat(1024 * 1024 + 1), 'protocol_error'],
  ]) {
    const options = await mock(t, socket => socket.end(reply));
    await assert.rejects(callBridge('game.state', {}, options), { code });
  }
  const options = await mock(t, (socket, request) => socket.end(JSON.stringify({ schemaVersion: 1, id: request.id, ok: false, error: { code: 'busy', message: 'Queue full' } }) + '\n'));
  await assert.rejects(callBridge('game.state', {}, options), { code: 'busy' });
});

test('timeout does not retry the request', async t => {
  let received = 0;
  const options = await mock(t, () => received++);
  await assert.rejects(callBridge('game.state', {}, { ...options, timeoutMs: 100 }), { code: 'timeout' });
  assert.equal(received, 1);
});

test('absent bridge returns actionable failure', async () => {
  await assert.rejects(callBridge('bridge.ping', {}, { pipePath: endpoint() }), { code: 'bridge_unavailable' });
});

const probePath = fileURLToPath(new URL('../artifacts/transport-probe/TransportProbe.exe', import.meta.url));
async function probe(t, drain = true) {
  assert.ok(existsSync(probePath), 'Run scripts/Test.ps1 to build the C# transport probe first.');
  const pipePath = endpoint();
  const child = spawn(probePath, [pipePath.split('\\').at(-1), ...drain ? [] : ['no-drain']], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  t.after(async () => {
    if (child.exitCode !== null) return;
    const exited = once(child, 'exit');
    child.stdin.end('\n');
    const timer = setTimeout(() => child.kill(), 2000);
    await exited;
    clearTimeout(timer);
  });
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('C# probe startup timeout')), 5000);
    child.once('error', error => { clearTimeout(timer); reject(error); });
    child.stdout.on('data', bytes => { if (bytes.toString().includes('READY')) { clearTimeout(timer); resolve(); } });
    child.once('exit', code => { clearTimeout(timer); reject(new Error(`Probe exited: ${code}`)); });
  });
  return { pipePath };
}

test('real C# transport: ping, queued reads, concurrent calls and unknown command', { skip: process.platform !== 'win32' }, async t => {
  const options = await probe(t);
  const status = await callBridge('bridge.ping', {}, options);
  assert.equal(status.readOnly, false);
  const reads = await Promise.all(Array.from({ length: 4 }, () => callBridge('game.state', { label: '滨河' }, options)));
  assert.ok(reads.every(read => read.transportProbe && read.echo.label === '滨河'));
  await assert.rejects(callBridge('scene.place', {}, options), { code: 'unsupported_method' });
});

test('real C# transport stays responsive when game thread does not drain reads', { skip: process.platform !== 'win32' }, async t => {
  const options = await probe(t, false);
  await assert.rejects(callBridge('game.state', {}, options), error => ['disconnected', 'timeout'].includes(error.code));
  assert.equal((await callBridge('bridge.ping', {}, options)).readOnly, false);
});
