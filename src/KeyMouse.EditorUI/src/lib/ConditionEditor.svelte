<script lang="ts">
  /**
   * 条件选择器：把 `cond` 从"手写表达式"改成一组可选项。
   *
   * 选项只生成同一种流程表达式，缺必填项时生成空串——节点上的「待补」、抽屉顶部的
   * 「还缺 条件」和播放前检查会照常拦住，不会悄悄把 `text("")` 写进文件。
   */
  import {
    EMPTY_FIELDS,
    MODE_LABELS,
    buildCondition,
    parseCondition,
    type ConditionFields,
    type ConditionMode,
  } from './conditions';

  export let value = '';
  export let onchange: (value: string) => void = () => undefined;

  let mode: ConditionMode = '';
  let fields: ConditionFields = { ...EMPTY_FIELDS };
  let lastValue: string | null = null;
  let lastEmitted = '';

  $: expression = buildCondition(mode, fields);

  // 自己刚写出去的值不再回灌，否则每敲一个字都会把本地草稿覆盖成"已生成"的形态。
  $: if (lastValue === null || value !== lastValue) {
    lastValue = value;
    if (value !== lastEmitted) {
      const parsed = parseCondition(value);
      mode = parsed.mode;
      fields = parsed.fields;
      lastEmitted = value;
    }
  }

  function emit(): void {
    // reactive 的 expression 要等下一帧才更新；这里必须现算，否则每次输入都落后一个字符。
    const next = buildCondition(mode, fields);
    if (next === lastEmitted) return;
    lastEmitted = next;
    onchange(next);
  }

  function chooseMode(next: string): void {
    mode = next as ConditionMode;
    emit();
  }

  function chooseOperator(next: string): void {
    fields = { ...fields, operator: next as ConditionFields['operator'] };
    emit();
  }

  function patch<K extends keyof ConditionFields>(key: K, next: ConditionFields[K]): void {
    fields = { ...fields, [key]: next };
    emit();
  }

  function label(modeValue: ConditionMode): string {
    return MODE_LABELS.find((item) => item.value === modeValue)?.label ?? '';
  }

  function hint(modeValue: ConditionMode): string {
    return MODE_LABELS.find((item) => item.value === modeValue)?.hint ?? '';
  }

  function blank(value: string): boolean {
    return value.trim() === '';
  }

  function msBlank(): boolean {
    const parsed = Number.parseInt(fields.ms, 10);
    return !Number.isFinite(parsed) || parsed <= 0;
  }
</script>

<div class="condition">
  <label class="field">
    <span class="label">条件类型</span>
    <select value={mode} on:change={(event) => chooseMode(event.currentTarget.value)}>
      {#each MODE_LABELS as item}
        <option value={item.value}>{item.label}</option>
      {/each}
    </select>
    <p class="hint">{hint(mode)}</p>
  </label>

  {#if mode === 'compare'}
    <div class="compare-grid">
      <label class="field">
        <span class="label">左值 {#if blank(fields.left)}<span class="need">必填</span>{/if}</span>
        <input value={fields.left} placeholder="变量名，例如 n" on:input={(e) => patch('left', e.currentTarget.value)} />
      </label>
      <label class="field">
        <span class="label">运算符</span>
        <select value={fields.operator} on:change={(e) => chooseOperator(e.currentTarget.value)}>
          {#each ['==', '!=', '<', '<=', '>', '>='] as op}
            <option value={op}>{op}</option>
          {/each}
        </select>
      </label>
      <label class="field">
        <span class="label">右值 {#if blank(fields.right)}<span class="need">必填</span>{/if}</span>
        <input value={fields.right} placeholder="3 或 “完成”" on:input={(e) => patch('right', e.currentTarget.value)} />
      </label>
    </div>
  {:else if mode === 'text'}
    <label class="field">
      <span class="label">要找的文字 {#if blank(fields.text)}<span class="need">必填</span>{/if}</span>
      <input value={fields.text} placeholder="例如 保存" on:input={(e) => patch('text', e.currentTarget.value)} />
    </label>
    <p class="hint">生成 <code>text("…")</code>；下面「条件读哪里」的窗口与区域也都要填。</p>
  {:else if mode === 'window' || mode === 'front'}
    <label class="field">
      <span class="label">进程名 {#if blank(fields.process)}<span class="need">必填</span>{/if}</span>
      <input value={fields.process} placeholder="例如 notepad" on:input={(e) => patch('process', e.currentTarget.value)} />
    </label>
  {:else if mode === 'found'}
    <label class="field">
      <span class="label">包含的文字 {#if blank(fields.text)}<span class="need">必填</span>{/if}</span>
      <input value={fields.text} placeholder="例如 副本" on:input={(e) => patch('text', e.currentTarget.value)} />
    </label>
  {:else if mode === 'elapsed'}
    <label class="field">
      <span class="label">超过多少毫秒 {#if msBlank()}<span class="need">必填</span>{/if}</span>
      <input type="number" min="1" value={fields.ms} on:input={(e) => patch('ms', e.currentTarget.value)} />
    </label>
  {:else if mode === 'custom'}
    <label class="field">
      <span class="label">表达式 {#if blank(fields.custom)}<span class="need">必填</span>{/if}</span>
      <input
        class="mono"
        spellcheck="false"
        value={fields.custom}
        placeholder={'例如 n > 1 && text("好")'}
        on:input={(e) => patch('custom', e.currentTarget.value)}
      />
    </label>
  {/if}

  <p class="preview">
    <span class="preview-label">生成的条件</span>
    <code>{expression || '（还没填完）'}</code>
  </p>

  {#if mode && expression === ''}
    <p class="warn">条件还没填完：带「必填」的项不能空，右上角会显示「还缺 条件」。</p>
  {/if}

  {#if mode === ''}
    <p class="hint">选一种条件类型即可；需要特殊写法时选「自定义表达式」。</p>
  {/if}
</div>

<style>
  .condition {
    display: flex;
    flex-direction: column;
    gap: 9px;
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: 5px;
  }

  .label {
    display: flex;
    align-items: baseline;
    gap: 6px;
    font-size: var(--kp-fs-small);
    color: var(--kp-ink2);
  }

  .need {
    color: var(--kp-signal);
    font-size: 10px;
  }

  input,
  select {
    width: 100%;
    box-sizing: border-box;
    padding: 7px 9px;
    border: 1px solid var(--kp-rule);
    border-radius: 4px;
    background: var(--kp-card);
    color: var(--kp-ink);
    font: inherit;
    font-size: 12.5px;
  }

  input:focus,
  select:focus {
    outline: none;
    border-color: var(--kp-signal);
  }

  .mono {
    font-family: var(--kp-font-mono);
    font-size: 12px;
  }

  .compare-grid {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 74px minmax(0, 1fr);
    gap: 7px;
    align-items: start;
  }

  .hint {
    margin: 0;
    font-size: 10.5px;
    line-height: 1.5;
    color: var(--kp-ink3);
  }

  .hint code {
    font-family: var(--kp-font-mono);
    font-size: 10px;
    color: var(--kp-ink2);
  }

  .preview {
    display: flex;
    align-items: baseline;
    gap: 8px;
    margin: 2px 0 0;
    padding-top: 7px;
    border-top: 1px solid var(--kp-rule);
    min-width: 0;
  }

  .preview-label {
    flex: none;
    font: 500 9px/1 var(--kp-font-mono);
    letter-spacing: 0.1em;
    color: var(--kp-ink3);
  }

  .preview code {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-family: var(--kp-font-mono);
    font-size: 10.5px;
    color: var(--kp-ink2);
  }

  .warn {
    margin: 0;
    color: var(--kp-signal);
    font-size: 10.5px;
    line-height: 1.5;
  }
</style>
