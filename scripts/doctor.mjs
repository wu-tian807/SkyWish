import { existsSync, readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { callBridge } from '../src/harness/bridge.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const game = [process.env.CS2_PATH, process.env.CSII_INSTALLATIONPATH,
  'D:/SteamLibrary/steamapps/common/Cities Skylines II',
  'C:/Program Files (x86)/Steam/steamapps/common/Cities Skylines II']
  .find(p => p && existsSync(path.join(p, 'Cities2_Data/Managed/Game.dll')));
const sdkPath = path.join(root, '.tools/dotnet/dotnet.exe');
let sdk;
try { sdk = execFileSync(existsSync(sdkPath) ? sdkPath : 'dotnet', ['--list-sdks'], { encoding: 'utf8', windowsHide: true }).trim(); }
catch { sdk = ''; }
let bridge;
try { bridge = { connected: true, ...await callBridge('bridge.ping', {}, { timeoutMs: 1000 }) }; }
catch (error) { bridge = { connected: false, code: error.code }; }
const manifest = new URL('../references/sources.lock.json', import.meta.url);
const report = {
  node: process.version, gamePath: game ?? null, dotnetSdks: sdk || null,
  modBuilt: existsSync(path.join(root, 'artifacts/mod/CityDirector.dll')),
  referenceCount: existsSync(manifest) ? JSON.parse(readFileSync(manifest, 'utf8')).repositories.length : 0,
  bridge,
};
console.log(JSON.stringify(report, null, 2));
if (!game || !sdk || !report.modBuilt) process.exitCode = 1;
