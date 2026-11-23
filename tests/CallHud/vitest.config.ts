export default {
  cacheDir: process.env.CALL_HUD_CACHE,
  resolve: { alias: { vitest: new URL('../../src/KeyMouse.EditorUI/node_modules/vitest/dist/index.js', import.meta.url).pathname.replace(/^\/(\w:)/, '$1') } },
  test: { include: ['tests/CallHud/hud.test.ts'] },
};
