<script lang="ts">
  export let fileLabel: string;
  export let dirty: boolean;
  export let path: string | null;
</script>

  <header class="topbar">
    <div class="brand">
      <span class="mark" aria-hidden="true">▞</span>
      <span class="product">KeyMouse</span>
      <span class="kind">流程编辑器</span>
    </div>
    <div class="doc">
      <span class="name" class:warn={dirty}>{fileLabel}{#if dirty}<span class="unsaved">未保存</span>{/if}</span>
      {#if path}<span class="path" title={path}>{path}</span>{/if}
    </div>
    <div class="file-actions">
      <slot />
    </div>
  </header>

<style>
  /* ── 顶栏：报头 ───────────────────────────────────────
     三段：kicker（等宽小字）／衬线文件标题 + 等宽路径／右侧动作。
     整页只有这一条 1.5px 的实线，下面工具栏、列表、检查器一律发丝线。 */
  .topbar {
    display: grid;
    grid-template-columns: auto minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--kp-gap-xl);
    padding: 13px var(--kp-gap-xl) 11px;
    border-bottom: 1.5px solid var(--kp-ink);
  }

  .brand {
    display: flex;
    align-items: baseline;
    gap: 7px;
    font: 600 10.5px/1 var(--kp-font-mono);
    letter-spacing: 0.2em;
    text-transform: uppercase;
    color: var(--kp-ink2);
  }

  .mark {
    color: var(--kp-signal);
    font-size: 13px;
    transform: translateY(-1px);
  }

  .product {
    font-weight: 600;
  }

  .kind {
    color: var(--kp-ink3);
    font-weight: 500;
    letter-spacing: 0.14em;
  }

  /* 打开的是哪个文件：标题在上（衬线），路径在下（等宽、可截断） */
  .doc {
    display: flex;
    flex-direction: column;
    gap: 2px;
    min-width: 0;
  }

  .doc .name {
    display: flex;
    align-items: baseline;
    gap: 8px;
    font: 500 17px/1.3 var(--kp-font-serif);
    letter-spacing: 0.02em;
    color: var(--kp-ink);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .doc .name.warn {
    color: var(--kp-ink2);
  }

  .doc .path {
    font-size: 11.5px;
    line-height: 1.4;
    color: var(--kp-ink3);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .unsaved {
    flex: none;
    padding: 1px 6px;
    border: 1px solid var(--kp-signal);
    border-radius: 2px;
    color: var(--kp-signal);
    font: 500 10px/1.5 var(--kp-font-mono);
    letter-spacing: 0.08em;
  }

  .file-actions {
    display: flex;
    align-items: center;
    gap: 2px;
  }

  .file-actions :global(.ghost:disabled) {
    opacity: 0.4;
  }

  .path {
    font-family: var(--kp-font-mono);
    font-size: var(--kp-fs-small);
    color: var(--kp-ink3);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    user-select: text;
  }

</style>
