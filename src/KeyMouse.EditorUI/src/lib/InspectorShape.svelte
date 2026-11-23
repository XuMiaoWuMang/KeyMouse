<script lang="ts">
  /**
   * 把契约里一个 shape（target / region / point / probe）的字段排成控件。
   *
   * 检查器里"条件读哪里"与"前提条件"都要编辑这种嵌套对象；复制三遍字段清单迟早漏一个。
   * 这里只按 kind 渲染，字段从 schema 拿，和 Inspector 的主字段区同一套规则。
   *
   * 坐标块与主检查器保持一致：两选项的空间用滑钮，x|y 一排、dx|dy 一排，取点收尾。
   * 屏幕区域不需要窗口，嵌套的 target 就不显示了——填了运行时也不看。
   */
  import {
    helpOf,
    label,
    shapeFields,
    type FieldContract,
    type FlowSchema,
  } from './schema';
  import { readPath, readText } from './fieldpath';

  export let schema: FlowSchema;
  export let step: object;
  export let fields: FieldContract[];
  export let prefix: string;
  export let onchange: (path: string, value: unknown) => void;
  export let onpick: (kind: 'point' | 'region', path: string) => void;

  function hasValue(value: unknown): boolean {
    if (value === null || value === undefined) return false;
    if (typeof value === 'string') return value.trim() !== '';
    if (Array.isArray(value)) return value.length > 0;
    return true;
  }

  function asText(value: unknown): string {
    return value === null || value === undefined ? '' : String(value);
  }

  function enumText(field: FieldContract, path: string): string {
    const written = readText(step, path);
    return written !== '' ? written : asText(field.default);
  }

  function commitEnum(path: string, picked: string, field: FieldContract): void {
    if (picked === '') onchange(path, undefined);
    else onchange(path, picked === asText(field.default) ? undefined : picked);
  }

  function commitInt(path: string, raw: string, field: FieldContract): void {
    const trimmed = raw.trim();
    if (trimmed === '') {
      onchange(path, undefined);
      return;
    }
    const value = Number.parseInt(trimmed, 10);
    if (!Number.isFinite(value)) return;
    let bounded = value;
    if (typeof field.min === 'number' && bounded < field.min) bounded = field.min;
    if (typeof field.max === 'number' && bounded > field.max) bounded = field.max;
    onchange(path, bounded);
  }

  /** `space` 没写就是契约默认值——界面和引擎必须按同一个默认解释。 */
  function shapeSpace(field: FieldContract): string {
    const written = readText(step, `${prefix}.${field.name}.space`);
    if (written !== '') return written;
    const space = shapeFields(schema, field.kind).find((item) => item.name === 'space');
    return asText(space?.default ?? 'client');
  }

  function spaceContract(field: FieldContract): FieldContract | undefined {
    return shapeFields(schema, field.kind).find((item) => item.name === 'space');
  }

  function relativeShape(field: FieldContract): boolean {
    return field.kind === 'region' ? shapeSpace(field) !== 'screen' : shapeSpace(field) === 'client';
  }

  function commitSpace(field: FieldContract, value: string) {
    const contract = spaceContract(field);
    if (contract) commitEnum(`${prefix}.${field.name}.space`, value, contract);
  }

  function toggleSpace(field: FieldContract) {
    const contract = spaceContract(field);
    if (contract) commitEnum(`${prefix}.${field.name}.space`, relativeShape(field) ? 'screen' : 'client', contract);
  }

  // 读屏区域落在屏幕上时不需要窗口；target 那几行就不占地方。
  $: regionScreen = readText(step, `${prefix}.region.space`) === 'screen';
  $: visible = fields.filter((field) => !(field.kind === 'target' && regionScreen));
</script>

{#each visible as field}
  <div class="field" class:wide={field.kind === 'region' || field.kind === 'point'}>
    <span class="label">
      {label(field)}
      {#if helpOf(field)}<span class="info" title={helpOf(field)}>i</span>{/if}
      {#if field.required && !hasValue(readPath(step, `${prefix}.${field.name}`))}<span class="need" title="必填">*</span>{/if}
    </span>

    {#if field.kind === 'target'}
      <div class="target-grid">
        {#each shapeFields(schema, 'target') as sub}
          <input
            value={readText(step, `${prefix}.${field.name}.${sub.name}`)}
            placeholder={label(sub)}
            aria-label={`${label(field)} ${label(sub)}`}
            title={helpOf(sub)}
            on:input={(event) => onchange(`${prefix}.${field.name}.${sub.name}`, event.currentTarget.value)}
          />
        {/each}
      </div>
    {:else if field.kind === 'region' || field.kind === 'point'}
      <div class="coord">
        {#if spaceContract(field)}
          {#if (spaceContract(field)?.values ?? []).length === 2}
            <div class="space-row">
              <span class="space-side" class:on={!relativeShape(field)}>屏幕</span>
              <button
                class="switch"
                class:on={relativeShape(field)}
                role="switch"
                aria-checked={relativeShape(field)}
                aria-label="客户区相对坐标"
                title={relativeShape(field) ? '后台客户区：直接操作或读取目标窗口，不移动系统鼠标' : '屏幕绝对坐标'}
                on:click={() => toggleSpace(field)}
              >
                <span class="knob"></span>
              </button>
              <span class="space-side" class:on={relativeShape(field)}>客户区 · 后台</span>
            </div>
          {:else}
            <label class="space-row">
              <span class="space-side on">坐标空间</span>
              <select
                class="space-select"
                value={shapeSpace(field)}
                on:change={(event) => commitSpace(field, event.currentTarget.value)}
              >
                {#each spaceContract(field)?.values ?? [] as value}
                  <option value={value}>
                    {spaceContract(field)?.valueLabels?.[value]?.zh ?? value}
                  </option>
                {/each}
              </select>
            </label>
          {/if}
        {/if}

        <div class="coords">
          {#each shapeFields(schema, field.kind).filter((sub) => sub.name !== 'space') as sub}
            <label class="field compact">
              <span class="label">{label(sub)}</span>
              <input
                type="number"
                value={asText(readPath(step, `${prefix}.${field.name}.${sub.name}`))}
                on:input={(event) =>
                  commitInt(`${prefix}.${field.name}.${sub.name}`, event.currentTarget.value, sub)}
              />
            </label>
          {/each}
        </div>

        <button
          class="pick"
          on:click={() => onpick(field.kind === 'region' ? 'region' : 'point', `${prefix}.${field.name}`)}
        >
          {field.kind === 'region' ? '取一块' : '取点'}
        </button>
      </div>
    {:else if field.kind === 'enum'}
      <select
        value={enumText(field, `${prefix}.${field.name}`)}
        on:change={(event) => commitEnum(`${prefix}.${field.name}`, event.currentTarget.value, field)}
      >
        {#each field.values ?? [] as value}
          <option value={value}>{field.valueLabels?.[value]?.zh ?? value}</option>
        {/each}
      </select>
    {:else if field.kind === 'int'}
      <input
        type="number"
        min={field.min}
        max={field.max}
        value={asText(readPath(step, `${prefix}.${field.name}`))}
        on:input={(event) => commitInt(`${prefix}.${field.name}`, event.currentTarget.value, field)}
      />
    {:else if field.kind === 'multiline'}
      <textarea
        rows="3"
        value={readText(step, `${prefix}.${field.name}`)}
        on:input={(event) => onchange(`${prefix}.${field.name}`, event.currentTarget.value)}
      ></textarea>
    {:else if field.kind === 'bool'}
      <button
        class="switch"
        class:on={readPath(step, `${prefix}.${field.name}`) === true}
        role="switch"
        aria-checked={readPath(step, `${prefix}.${field.name}`) === true}
        on:click={() => onchange(`${prefix}.${field.name}`, readPath(step, `${prefix}.${field.name}`) !== true)}
      >
        <span class="knob"></span>
      </button>
    {:else if field.kind === 'text'}
      <input
        value={readText(step, `${prefix}.${field.name}`)}
        title={helpOf(field)}
        on:input={(event) => onchange(`${prefix}.${field.name}`, event.currentTarget.value)}
      />
    {:else}
      <p class="readonly">{asText(readPath(step, `${prefix}.${field.name}`)) || '——'}</p>
    {/if}
  </div>
{/each}

<style>
  .field {
    display: flex;
    flex-direction: column;
    gap: 5px;
    margin: 10px 0;
  }

  .field.compact {
    margin: 0;
    gap: 3px;
  }

  .label {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: var(--kp-fs-small);
    color: var(--kp-ink2);
  }

  .info {
    display: inline-grid;
    place-items: center;
    width: 14px;
    height: 14px;
    border: 1px solid var(--kp-rule);
    border-radius: 50%;
    font: 500 9px/1 var(--kp-font-serif);
    color: var(--kp-ink3);
    cursor: help;
  }

  .need {
    margin-left: 4px;
    color: var(--kp-signal);
    font-size: 13px;
    font-weight: 600;
  }

  input,
  select,
  textarea {
    width: 100%;
    box-sizing: border-box;
    padding: 7px 9px;
    border: 1px solid var(--kp-rule);
    border-radius: 4px;
    background: var(--kp-card);
    color: var(--kp-ink);
    font: inherit;
    font-size: 13px;
  }

  input:focus,
  select:focus,
  textarea:focus {
    outline: none;
    border-color: var(--kp-signal);
  }

  .target-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 6px;
  }

  .target-grid input:last-child {
    grid-column: 1 / -1;
  }

  .coord {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .space-row {
    display: flex;
    align-items: center;
    gap: 10px;
    margin: 0;
  }

  .space-side {
    font-size: 12px;
    color: var(--kp-ink3);
    transition: color 120ms ease;
  }

  .space-side.on {
    color: var(--kp-ink);
    font-weight: 600;
  }

  .space-select {
    flex: 1;
    min-height: 28px;
    padding: 4px 8px;
    font-size: 12px;
  }

  .coords {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 6px 10px;
  }

  .pick {
    align-self: flex-end;
    height: 29px;
    padding: 0 10px;
    border: 1px solid var(--kp-signal);
    border-radius: 4px;
    background: transparent;
    font: inherit;
    font-size: 12px;
    color: var(--kp-signal);
    cursor: pointer;
    white-space: nowrap;
  }

  .pick:hover {
    background: var(--kp-signal);
    color: #fff;
  }

  .coord > .pick {
    align-self: stretch;
    width: 100%;
    height: 30px;
  }

  .readonly {
    margin: 0;
    font-size: 12px;
    color: var(--kp-ink3);
  }

  .switch {
    position: relative;
    width: 38px;
    height: 22px;
    padding: 0;
    border: 1px solid var(--kp-rule);
    border-radius: 999px;
    background: var(--kp-field);
    cursor: pointer;
    transition: background 120ms ease;
  }

  .switch.on {
    background: var(--kp-signal);
    border-color: var(--kp-signal);
  }

  .knob {
    position: absolute;
    top: 2px;
    left: 2px;
    width: 16px;
    height: 16px;
    border-radius: 50%;
    background: var(--kp-ink3);
    transition: left 120ms ease, background 120ms ease;
  }

  .switch.on .knob {
    left: 18px;
    background: #fff;
  }
</style>
