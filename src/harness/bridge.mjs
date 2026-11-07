import net from 'node:net';
import { randomUUID } from 'node:crypto';

export const PIPE_PATH = '\\\\.\\pipe\\city-director-v1';
const MAX_RESPONSE = 1024 * 1024;

export class BridgeError extends Error {
  constructor(code, message) { super(message); this.name = 'BridgeError'; this.code = code; }
}

// Each invocation has one connection and one request. No automatic retries.
export function callBridge(method, params = {}, { pipePath = PIPE_PATH, timeoutMs = 6000 } = {}) {
  const id = randomUUID();
  return new Promise((resolve, reject) => {
    const socket = net.createConnection(pipePath);
    let bytes = Buffer.alloc(0);
    let settled = false;
    const finish = (error, value) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      socket.destroy();
      if (error) reject(error); else resolve(value);
    };
    const timer = setTimeout(() => finish(new BridgeError('timeout', 'Game did not respond before the deadline. No retry was sent.')), timeoutMs);
    socket.once('connect', () => socket.write(JSON.stringify({ schemaVersion: 1, id, method, params }) + '\n'));
    socket.on('data', chunk => {
      if (bytes.length + chunk.length > MAX_RESPONSE) return finish(new BridgeError('protocol_error', 'Bridge response exceeded 1 MiB.'));
      bytes = Buffer.concat([bytes, chunk]);
      const end = bytes.indexOf(10);
      if (end < 0) return;
      try {
        const response = JSON.parse(bytes.subarray(0, end).toString('utf8'));
        if (response.schemaVersion !== 1 || response.id !== id || typeof response.ok !== 'boolean') {
          throw new BridgeError('protocol_error', 'Bridge returned an incompatible response or mismatched request id.');
        }
        if (!response.ok) {
          if (typeof response.error?.code !== 'string' || typeof response.error?.message !== 'string') {
            throw new BridgeError('protocol_error', 'Malformed bridge error.');
          }
          return finish(new BridgeError(response.error.code, response.error.message));
        }
        if (response.result === null || typeof response.result !== 'object' || Array.isArray(response.result)) {
          throw new BridgeError('protocol_error', 'Bridge result must be an object.');
        }
        finish(null, response.result);
      } catch (error) {
        finish(error instanceof BridgeError ? error : new BridgeError('protocol_error', 'Bridge returned invalid JSON.'));
      }
    });
    socket.once('error', error => finish(new BridgeError('bridge_unavailable', `CityDirector bridge is unavailable (${error.code ?? 'connection error'}). Start CS2 with the local mod installed.`)));
    socket.once('close', () => finish(new BridgeError('disconnected', 'Bridge closed before returning a complete response.')));
  });
}
