import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const blender = process.env.BLENDER_PATH ?? 'D:\\SteamLibrary\\steamapps\\common\\Blender\\blender.exe';
const child = spawn(blender, ['--background', '--factory-startup', '--disable-autoexec', '--python-exit-code', '1',
  '--python', fileURLToPath(new URL('./Build-DemoAsset.py', import.meta.url)), '--', '--output',
  fileURLToPath(new URL('../artifacts/generated/', import.meta.url))], { windowsHide: true, stdio: 'inherit' });
child.on('error', error => { console.error(error); process.exitCode = 1; });
child.on('exit', code => { process.exitCode = code ?? 1; });
