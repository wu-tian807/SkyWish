import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

test('native UI guard consumes pointer/keyboard events and releases all owned listeners over repeated opens', () => {
  const source = readFileSync(new URL('../src/CityDirector.Mod/AssetLibraryInput.js', import.meta.url), 'utf8');
  const handlers = new Map();
  const children = [];
  const body = {
    appendChild(node) { node.parentNode = this; children.push(node); },
    removeChild(node) { children.splice(children.indexOf(node), 1); node.parentNode = null; },
  };
  const window = {
    addEventListener(name, handler, capture) {
      assert.equal(capture, true);
      assert.ok(!handlers.has(name), `duplicate listener: ${name}`);
      handlers.set(name, handler);
    },
    removeEventListener(name, handler, capture) {
      assert.equal(capture, true);
      assert.equal(handlers.get(name), handler);
      handlers.delete(name);
    },
  };
  const context = vm.createContext({ window, document: { body, createElement: () => ({ style: {} }) } });
  for (let cycle = 0; cycle < 3; cycle++) {
    vm.runInContext(source, context);
    vm.runInContext(source, context); // Opening twice must not accumulate blockers.
    assert.equal(children.length, 1);
    for (const name of ['mousedown', 'mouseup', 'click', 'wheel', 'keydown', 'keyup']) {
      let prevented = false, stopped = false;
      handlers.get(name)({ preventDefault() { prevented = true; }, stopImmediatePropagation() { stopped = true; } });
      assert.ok(prevented && stopped, `${name} leaked to the native toolbar`);
    }
    window.__cityDirectorInputGuard();
    assert.equal(handlers.size, 0);
    assert.equal(children.length, 0);
    assert.equal(window.__cityDirectorInputGuard, undefined);
  }
});
