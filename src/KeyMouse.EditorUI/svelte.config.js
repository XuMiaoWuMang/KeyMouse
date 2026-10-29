import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

/** svelte-check 与 vite 都从这里读预处理配置，只写一处。
 *  少了它，`<script lang="ts">` 在 svelte-check 里会被当成"有 lang 标签却没有预处理器"。 */
export default {
  preprocess: vitePreprocess(),
};
