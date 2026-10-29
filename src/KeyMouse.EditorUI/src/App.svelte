<script lang="ts">
  import { onMount } from 'svelte';
  import { invoke } from '@tauri-apps/api/core';
  import StepIcon from './lib/StepIcon.svelte';
  import {
    childSteps,
    stepSummary,
    stepTitle,
    targetLabel,
    type FlowStep,
    type FlowTarget,
  } from './lib/model';

  /** 演示数据：与仓库里的 `samples\demo-flow.json` 逐字段一致，方便拿文件对照界面。 */
  const steps: FlowStep[] = [
    { type: 'focus', target: { process: 'notepad' }, note: '切到记事本' },
    { type: 'type', text: '你好，{{name}}', intervalMs: 30 },
    { type: 'key', combo: 'Ctrl+S' },
    { type: 'sleep', ms: 300 },
    {
      type: 'click-text',
      target: { process: 'notepad' },
      region: { space: 'client', x: 0, y: 0, width: 420, height: 40 },
      text: '保存',
      match: 'contains',
      maxErrors: 1,
      timeoutMs: 8000,
    },
    { type: 'repeat', times: 3, steps: [{ type: 'wheel', delta: -120 }] },
  ];

  /** 真正打开流程之前，先把"打开的是哪个文件"说清楚：相对仓库根显示。 */
  $: documentPath = runtime?.repoRoot ? `${runtime.repoRoot}\\samples\\demo-flow.json` : 'samples\\demo-flow.json';

  let selected = 0;
  let outputOpen = false;
  let runtime: {
    cliPath: string;
    cliVersion: string;
    environment: string;
    repoRoot: string;
    editorExe: string;
    elevated: boolean;
  } | null = null;

  onMount(async () => {
    try {
      runtime = await invoke('runtime_info');
    } catch (e) {
      runtime = null;
      console.error(e);
    }
  });

  // 右侧检查器的内容跟着选中的行走；分组步骤选中的是它自己，不是子步骤。
  $: current = steps[selected] ?? steps[0];

  function rowNumber(index: number): string {
    return String(index + 1);
  }

  function targetOf(step: FlowStep): FlowTarget {
    return step.target ?? {};
  }

  function requiredHint(step: FlowStep): string {
    return step.target ? '必填' : '可选';
  }
</script>

<div class="shell">
  <header class="topbar">
    <div class="brand">
      <span class="mark" aria-hidden="true">▞</span>
      <span class="product">KeyMouse</span>
      <span class="kind">流程编辑器</span>
    </div>
    <div class="path" class:muted={!documentPath}>{documentPath || '还没有保存到文件'}</div>
  </header>

  <nav class="toolbar">
    <div class="palette">
      <button class="palette-item"><StepIcon name="click" />点击</button>
      <button class="palette-item"><StepIcon name="type" />输入文字</button>
      <button class="palette-item"><StepIcon name="wait-text" />等文字出现</button>
      <button class="palette-item"><StepIcon name="click-text" />找字并点它</button>
      <button class="palette-item more">+ 更多</button>
    </div>

    <div class="actions">
      <button class="ghost"><span class="rec-dot" aria-hidden="true"></span>录制</button>
      <button class="ghost">试运行</button>
      <button class="primary" disabled={false}>
        <svg viewBox="0 0 16 16" aria-hidden="true" class="play"><path d="M5 3.5 12 8l-7 4.5z" /></svg>
        播放
      </button>
      <button class="ghost icon-only" aria-label="更多操作">···</button>
    </div>
  </nav>

  <main class="body">
    <section class="list" aria-label="步骤">
      {#each steps as step, index}
        <button
          class="row"
          class:selected={index === selected}
          on:click={() => (selected = index)}
        >
          <span class="rail" aria-hidden="true"></span>
          <span class="step-dot" aria-hidden="true"></span>
          <span class="number">{rowNumber(index)}</span>
          <span class="chip" aria-hidden="true"><StepIcon name={step.type} size={14} /></span>
          <span class="text">
            <span class="title">{stepTitle(step.type)}</span>
            <span class="summary">{stepSummary(step)}</span>
          </span>
          {#if childSteps(step).length > 0}
            <span class="count">{childSteps(step).length} 步</span>
          {/if}
        </button>

        {#each childSteps(step) as child, childIndex}
          <div class="row child">
            <span class="rail" aria-hidden="true"></span>
            <span class="step-dot" aria-hidden="true"></span>
            <span class="number">{rowNumber(index)}.{childIndex + 1}</span>
            <span class="chip" aria-hidden="true"><StepIcon name={child.type} size={14} /></span>
            <span class="text">
              <span class="title">{stepTitle(child.type)}</span>
              <span class="summary">{stepSummary(child)}</span>
            </span>
          </div>
        {/each}
      {/each}
    </section>

    <aside class="side" aria-label="这一步的设置">
      <div class="side-head">
        <h2>目标窗口</h2>
        <span class="hint">{requiredHint(current)}</span>
      </div>

      <label class="field">
        <span class="label">进程名</span>
        <input value={targetOf(current).process ?? ''} placeholder="notepad" />
      </label>
      <label class="field">
        <span class="label">窗口类名</span>
        <input value={targetOf(current).class ?? ''} placeholder="" />
      </label>
      <label class="field">
        <span class="label">标题</span>
        <input value={targetOf(current).title ?? ''} placeholder="" />
      </label>

      <h2 class="section">只在满足条件时执行</h2>
      <div class="toggle-row">
        <span class="switch" role="switch" aria-checked="false" tabindex="0"><span class="knob"></span></span>
        <span>每次都执行</span>
      </div>

      <label class="field">
        <span class="label">备注 <span class="info" title="只给人看，不影响回放">i</span></span>
        <input value={current.note ?? ''} />
      </label>

      <button class="disclosure">JSON <span aria-hidden="true">⌄</span></button>
    </aside>
  </main>

  <footer class="foot">
    <div class="status">
      {#if runtime}
        <span class="status-dot" class:warn={runtime.elevated}></span>
        {#if runtime.cliPath}
          <span class="mono">{runtime.cliVersion || '（命令行没有回版本号）'}</span>
          <span class="dim">· {runtime.environment === 'development' ? '开发树' : '安装版'}</span>
        {:else}
          <span class="dim">没找到命令行 KeyMouse.exe</span>
        {/if}
      {:else}
        <span class="dim">外壳没连上（浏览器里预览时正常）</span>
      {/if}
    </div>
    <button class="output-pill" on:click={() => (outputOpen = !outputOpen)}>
      <svg viewBox="0 0 16 16" aria-hidden="true"><rect x="2.5" y="3.5" width="11" height="8" rx="1.5" /><path d="M5 6.5h6M5 9h3.5" /></svg>
      输出
    </button>
  </footer>

  {#if outputOpen}
    <section class="output" aria-label="运行输出">
      <header>
        <h2>运行输出</h2>
        <button class="ghost small" on:click={() => (outputOpen = false)}>收起</button>
      </header>
      {#if runtime}
        <pre>环境：{runtime.environment}
命令行：{runtime.cliPath || '（没找到）'}
版本：{runtime.cliVersion || '（没回版本号）'}
外壳：{runtime.editorExe}
提权：{runtime.elevated ? '是（不该如此）' : '否（正确）'}</pre>
      {:else}
        <pre>外壳没连上。浏览器里直接打开预览时是正常的——
只有在 `pnpm tauri dev` 起的窗口里才有 IPC。</pre>
      {/if}
    </section>
  {/if}
</div>

<style>
  .shell {
    height: 100%;
    display: grid;
    grid-template-rows: auto auto 1fr auto;
    position: relative;
  }

  /* ── 顶栏 ───────────────────────────────────────────── */
  .topbar {
    display: grid;
    grid-template-columns: auto 1fr;
    align-items: baseline;
    gap: var(--kp-gap-xl);
    padding: 14px var(--kp-gap-xl) 10px;
  }

  .brand {
    display: flex;
    align-items: baseline;
    gap: 8px;
    font-size: var(--kp-fs-title);
    letter-spacing: -0.01em;
  }

  .mark {
    color: var(--kp-signal);
    font-size: 18px;
    transform: translateY(-1px);
  }

  .product {
    font-weight: 600;
  }

  .kind {
    color: var(--kp-ink2);
    font-size: var(--kp-fs-body);
  }

  .path {
    font-family: var(--kp-font-mono);
    font-size: var(--kp-fs-small);
    color: var(--kp-ink2);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    user-select: text;
  }

  /* ── 工具栏 ─────────────────────────────────────────── */
  .toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--kp-gap-lg);
    padding: 0 var(--kp-gap-xl) 12px;
    border-bottom: 1px solid var(--kp-rule);
  }

  .palette,
  .actions {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .palette-item,
  .ghost {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 32px;
    padding: 0 10px;
    border-radius: var(--kp-radius-control);
    color: var(--kp-ink2);
  }

  .palette-item:hover,
  .ghost:hover {
    background: var(--kp-field);
    color: var(--kp-ink);
  }

  .output-pill svg,
  .primary svg {
    width: 16px;
    height: 16px;
    fill: none;
    stroke: currentColor;
    stroke-width: 1.3;
    stroke-linecap: round;
    stroke-linejoin: round;
  }

  .more {
    color: var(--kp-ink2);
  }

  .rec-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    border: 1.5px solid currentColor;
  }

  .primary {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    height: 34px;
    padding: 0 16px 0 12px;
    border-radius: var(--kp-radius-control);
    background: var(--kp-signal);
    color: #fff;
    font-weight: 500;
  }

  .primary svg.play {
    fill: currentColor;
    stroke: none;
  }

  .primary:hover {
    background: var(--kp-signal-hover);
  }

  .primary:active {
    background: var(--kp-signal-pressed);
  }

  .icon-only {
    font-size: 15px;
    letter-spacing: 1px;
  }

  /* ── 两栏 ───────────────────────────────────────────── */
  .body {
    display: grid;
    grid-template-columns: minmax(420px, 1fr) minmax(360px, 46%);
    min-height: 0;
    padding: var(--kp-gap-lg) var(--kp-gap-xl);
    gap: var(--kp-gap-xl);
  }

  .list {
    position: relative;
    overflow: auto;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
  }

  /* 穿起所有步骤的那根线：从第一个点到最后一个点，画在行下面，被点上的一点底色盖住。 */
  .list::before {
    content: '';
    position: absolute;
    left: 21px;
    top: 30px;
    bottom: 30px;
    width: 1px;
    background: var(--kp-rule);
  }

  .row {
    position: relative;
    width: 100%;
    display: grid;
    grid-template-columns: 34px 24px 1fr auto;
    align-items: center;
    gap: 10px;
    padding: 11px 16px 11px 40px;
    border-bottom: 1px solid var(--kp-rule);
    text-align: left;
  }

  .row:last-child {
    border-bottom: 0;
  }

  .row:hover {
    background: var(--kp-field);
  }

  .row.selected {
    background: var(--kp-selected);
  }

  .row .rail {
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 2px;
    background: transparent;
  }

  .row.selected .rail {
    background: var(--kp-signal);
  }

  /* 步骤前的点：空心是"待跑"，实心朱砂是"当前这一步"。绝对定位是为了压在线上，
     底色把身后那根线遮住，看起来才像串起来的一列。 */
  .step-dot {
    position: absolute;
    left: 17px;
    top: 50%;
    transform: translateY(-50%);
    width: 9px;
    height: 9px;
    border-radius: 50%;
    border: 1px solid var(--kp-ink3);
    background: var(--kp-card);
  }

  .row:hover .step-dot {
    background: var(--kp-field);
  }

  .row.selected .step-dot {
    border-color: var(--kp-signal);
    background: var(--kp-signal);
    box-shadow: 0 0 0 2px var(--kp-card);
  }

  .number {
    font-family: var(--kp-font-mono);
    font-size: var(--kp-fs-small);
    color: var(--kp-ink3);
    text-align: right;
  }

  .row.selected .number {
    color: var(--kp-ink2);
  }

  .chip {
    display: grid;
    place-items: center;
    width: 24px;
    height: 24px;
    border-radius: 6px;
    background: var(--kp-field);
    color: var(--kp-ink2);
  }

  .row.selected .chip {
    background: var(--kp-signal);
    color: #fff;
  }

  .text {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .title {
    font-size: var(--kp-fs-row-title);
    line-height: 1.35;
  }

  .summary {
    font-size: var(--kp-fs-small);
    color: var(--kp-ink2);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .count {
    font-size: var(--kp-fs-small);
    color: var(--kp-ink3);
  }

  .row.child {
    /* 子步骤不再整体右移：点还得落在那根线上，缩进靠序号自己表达。 */
    border-bottom: 0;
    padding-top: 6px;
    padding-bottom: 6px;
  }

  .row.child .title,
  .row.child .summary {
    font-size: var(--kp-fs-small);
    color: var(--kp-ink2);
  }

  /* ── 右栏 ───────────────────────────────────────────── */
  .side {
    overflow: auto;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
    padding: 18px 20px 24px;
  }

  .side-head {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    padding-bottom: 10px;
    border-bottom: 1px solid var(--kp-rule);
  }

  h2 {
    margin: 0;
    font-size: 18px;
    font-weight: 600;
    letter-spacing: -0.01em;
  }

  h2.section {
    margin-top: 22px;
    padding-bottom: 10px;
    border-bottom: 1px solid var(--kp-rule);
  }

  .hint {
    font-size: var(--kp-fs-small);
    color: var(--kp-signal);
  }

  .field {
    display: grid;
    grid-template-columns: 92px 1fr;
    align-items: center;
    gap: 12px;
    margin-top: 14px;
  }

  .label {
    justify-self: end;
    color: var(--kp-ink2);
    font-size: var(--kp-fs-body);
  }

  .info {
    display: inline-grid;
    place-items: center;
    width: 15px;
    height: 15px;
    margin-left: 2px;
    border: 1px solid var(--kp-ink3);
    border-radius: 50%;
    color: var(--kp-ink3);
    font-size: 10px;
    line-height: 1;
    transform: translateY(-1px);
  }

  input {
    height: 34px;
    padding: 0 10px;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-control);
    background: var(--kp-field);
    user-select: text;
  }

  input::placeholder {
    color: var(--kp-ink3);
  }

  input:focus {
    outline: none;
    border-color: var(--kp-signal);
    background: #fff;
  }

  .toggle-row {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-top: 14px;
  }

  .switch {
    position: relative;
    width: 40px;
    height: 20px;
    border: 1px solid var(--kp-rule);
    border-radius: 999px;
    background: var(--kp-ink3);
  }

  .switch .knob {
    position: absolute;
    top: 2px;
    left: 2px;
    width: 14px;
    height: 14px;
    border-radius: 50%;
    background: #fff;
  }

  .disclosure {
    display: flex;
    width: 100%;
    align-items: center;
    justify-content: space-between;
    margin-top: 22px;
    padding: 10px 0;
    border-top: 1px solid var(--kp-rule);
    color: var(--kp-ink2);
    font-size: var(--kp-fs-small);
    letter-spacing: 0.04em;
  }

  /* ── 底部 ───────────────────────────────────────────── */
  .foot {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--kp-gap-lg);
    padding: 0 var(--kp-gap-xl) 14px;
  }

  .status {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: var(--kp-fs-small);
    color: var(--kp-ink2);
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .status-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--kp-signal);
  }

  .status-dot.warn {
    background: var(--kp-critical);
  }

  .mono {
    font-family: var(--kp-font-mono);
  }

  .dim {
    color: var(--kp-ink3);
  }

  .output-pill {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    height: 34px;
    padding: 0 16px;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-control);
    background: var(--kp-card);
    color: var(--kp-ink2);
  }

  .output-pill:hover {
    border-color: var(--kp-ink3);
    color: var(--kp-ink);
  }

  .output {
    position: absolute;
    right: var(--kp-gap-xl);
    bottom: 58px;
    width: min(760px, calc(100% - 96px));
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
    padding: 14px 16px 16px;
  }

  .output header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding-bottom: 8px;
  }

  .output h2 {
    font-size: var(--kp-fs-body);
    color: var(--kp-ink2);
    font-weight: 500;
  }

  .ghost.small {
    height: 26px;
    font-size: var(--kp-fs-small);
  }

  .output pre {
    margin: 0;
    padding: 12px;
    border-radius: var(--kp-radius-control);
    background: var(--kp-field);
    font-family: var(--kp-font-mono);
    font-size: var(--kp-fs-small);
    line-height: 1.7;
    white-space: pre-wrap;
    user-select: text;
  }
</style>
