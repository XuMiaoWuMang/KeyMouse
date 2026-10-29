import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

// Tauri 的 dev 服务器：端口固定，免得 tauri.conf.json 与 vite 两处各写一个。
// 预处理配置（TS）在 svelte.config.js —— svelte-check 也读那一份，只写一处。
export default defineConfig({
  plugins: [svelte()],
  clearScreen: false,
  server: {
    port: 5183,
    strictPort: true,
    // Rust 的 target/ 一直在写，watcher 会撞上 EBUSY 把 dev 服务器带崩。
    watch: { ignored: ['**/src-tauri/**', '**/node_modules/**'] },
  },
  build: { target: 'chrome110', outDir: 'dist', emptyOutDir: true },
});
