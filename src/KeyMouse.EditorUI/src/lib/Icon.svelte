<script lang="ts">
  /**
   * 图标：全部内联 SVG，线宽 1.5，颜色走 currentColor。
   *
   * 为什么不引网络图标（用户说"图标可以使用网络图像"是许可，不是命令）：
   *   · 外壳的生产环境只依赖系统 WebView2，离线也得能用；远程图标一断网就是空白方块；
   *   · 远程 svg/png 还要过 CORS 与 CSP，为一个 16px 的齿轮引一条网络请求不划算；
   *   · 内联 SVG 跟 currentColor 走，换纸色、切夜间主题都不用备第二套图。
   * 哪天要位图风格再引网络图，但图标这一层不值得。
   */

  import type { IconName } from './icon';


  export let name: IconName;
  /** 边长（px）。默认 16，设置页的小标题用 14，报头用 18。 */
  export let size = 16;
</script>

<svg
  class="icon"
  width={size}
  height={size}
  viewBox="0 0 24 24"
  fill="none"
  stroke="currentColor"
  stroke-width="1.5"
  stroke-linecap="round"
  stroke-linejoin="round"
  aria-hidden="true"
  focusable="false"
>
  {#if name === 'settings'}
    <circle cx="12" cy="12" r="3" />
    <path d="M12 3v2.4M12 18.6V21M4.9 7.8l2 1.2M17.1 15l2 1.2M4.9 16.2l2-1.2M17.1 9l2-1.2" />
  {:else if name === 'flow'}
    <path d="M5 5h5M5 12h9M5 19h5" />
    <circle cx="17.5" cy="5" r="1.6" />
    <circle cx="17.5" cy="19" r="1.6" />
    <circle cx="19" cy="12" r="1.6" />
  {:else if name === 'play'}
    <path d="M8 5.5v13l10-6.5-10-6.5Z" />
  {:else if name === 'record'}
    <circle cx="12" cy="12" r="6.5" />
    <circle cx="12" cy="12" r="2.6" fill="currentColor" stroke="none" />
  {:else if name === 'bulb'}
    <path d="M9 17h6M10 20h4" />
    <path d="M12 3.5a5.5 5.5 0 0 0-3.2 9.9c.5.4.7 1 .7 1.6h5c0-.6.2-1.2.7-1.6A5.5 5.5 0 0 0 12 3.5Z" />
  {:else if name === 'folder'}
    <path d="M3.5 7.5A1.5 1.5 0 0 1 5 6h4l1.8 2H19a1.5 1.5 0 0 1 1.5 1.5v7A1.5 1.5 0 0 1 19 18H5a1.5 1.5 0 0 1-1.5-1.5v-9Z" />
  {:else if name === 'wrench'}
    <path d="M14.5 6a3.5 3.5 0 0 0 4.4 4.4L9.6 20.1a2.1 2.1 0 0 1-3-3L16.8 6.9" />
    <path d="M6.4 17.7h.01" />
  {:else if name === 'keyboard'}
    <rect x="2.8" y="7" width="18.4" height="10" rx="1.6" />
    <path d="M6.5 11h.01M9.8 11h.01M13.1 11h.01M16.4 11h.01M8 14.3h8" />
  {:else if name === 'info'}
    <circle cx="12" cy="12" r="8.5" />
    <path d="M12 11v5.5M12 8h.01" />
  {:else if name === 'check'}
    <path d="M5 12.8 9.4 17 19 7.4" />
  {:else if name === 'close'}
    <path d="M6.5 6.5l11 11M17.5 6.5l-11 11" />
  {:else if name === 'copy'}
    <rect x="9" y="9" width="11" height="11" rx="1.6" />
    <path d="M15 9V6.6A1.6 1.6 0 0 0 13.4 5H6.6A1.6 1.6 0 0 0 5 6.6v6.8A1.6 1.6 0 0 0 6.6 15H9" />
  {:else if name === 'theme'}
    <circle cx="12" cy="12" r="8.5" />
    <path d="M12 3.5v17" />
    <path d="M12 3.5a8.5 8.5 0 0 1 0 17Z" fill="currentColor" fill-opacity="0.18" stroke="none" />
  {:else if name === 'pulse'}
    <path d="M3 12.5h4l2-4.5 3 9 2.2-4.5H21" />
  {/if}
</svg>

<style>
  .icon {
    display: block;
    flex: none;
  }
</style>
