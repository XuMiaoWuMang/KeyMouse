import { defineConfig } from 'vite';
import { svelte, vitePreprocess } from '@sveltejs/vite-plugin-svelte';

// Tauri 的 dev 服务器：端口固定，免得 tauri.conf.json 与 vite 两处各写一个。
export default defineConfig({
  // `<script lang="ts">` 要预处理，否则 Svelte 直接把 TS 当 JS 解析（ParseError）。
  plugins: [svelte({ preprocess: vitePreprocess() })],
  clearScreen: false,
  server: {
    port: 5183,
    strictPort: true,
    // Rust 的 target/ 一直在写，watcher 会撞上 EBUSY 把 dev 服务器带崩。
    watch: { ignored: ['**/src-tauri/**', '**/node_modules/**'] },
  },
  build: { target: 'chrome110', outDir: 'dist', emptyOutDir: true },
});
