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
    // 编辑器保存文件时会在旁边落一个 `.<名字>.<pid>.<guid>.tmpdir` 的临时目录，
    // 里面那个 .tmp 一写入就被锁——同样会把 watcher 带崩，一并忽略。
    watch: {
      ignored: ['**/src-tauri/**', '**/node_modules/**', '**/.*.tmpdir/**', '**/*.tmp'],
    },
  },
  build: { target: 'chrome110', outDir: 'dist', emptyOutDir: true },
});
