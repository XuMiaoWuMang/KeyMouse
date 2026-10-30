<script lang="ts">
  /**
   * 这一步的设置面板。
   *
   * 字段不在这里写死：形状、标签、必填、取值范围全部来自命令行给出的契约
   * （`KeyMouse schema`，见 `docs/reference/interface.md`）。老外壳按契约渲染，
   * 新外壳照做才对得上——否则契约加一个字段，两边就得各改一次，迟早走样。
   */
  import {
    fieldsOf,
    helpOf,
    label,
    shapeFields,
    visibleBy,
    type FieldContract,
    type FlowSchema,
  } from './schema';
  import { readBool, readPath, readText } from './fieldpath';

  export let schema: FlowSchema | null = null;
  export let step: object;
  export let onchange: (path: string, value: unknown) => void;
  export let onpick: (kind: 'point' | 'region', path: string) => void;

  $: type = String((step as Record<string, unknown>).type ?? '');
  // 契约把 note / when / shot 放在 common 里，每种步骤都自带——老外壳也是拼起来渲染的。
  // shot 不在这里画：证据图有自己的块（要读文件、要能"在文件夹里看"）。
  $: fields = schema
    ? [...fieldsOf(schema, type), ...schema.common]
        .filter((field) => field.name !== 'shot')
        .filter((field) => visibleBy(field, step as Record<string, unknown>))
    : [];
  $: target = fields.find((field) => field.kind === 'target') ?? null;
  $: when = fields.find((field) => field.kind === 'when') ?? null;
  $: note = fields.find((field) => field.name === 'note') ?? null;
  $: plain = fields.filter(
    (field) => field.kind !== 'target' && field.kind !== 'when' && field.name !== 'note',
  );
  $: targetEmpty =
    target !== null &&
    schema !== null &&
    shapeFields(schema, 'target').every((field) => !hasValue(readPath(step, `target.${field.name}`)));

  function hasValue(value: unknown): boolean {
    if (value === null || value === undefined) return false;
    if (typeof value === 'string') return value.trim() !== '';
    if (Array.isArray(value)) return value.length > 0;
    return true;
  }

  function kindOf(field: FieldContract): 'point' | 'region' {
    return field.kind === 'region' ? 'region' : 'point';
  }

  function asText(value: unknown): string {
    return value === null || value === undefined ? '' : String(value);
  }

  /** 数字框：清空就删掉这个键，别让 `int` 字段变成空串。 */
  function commitInt(path: string, raw: string, field: FieldContract) {
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
</script>

{#if !schema}
  <p class="waiting">正在读流程格式的契约…</p>
{:else}
  {#if target}
    <div class="side-head">
      <h2>{label(target)}</h2>
      <span class="hint" class:warn={targetEmpty}>{targetEmpty ? '必填' : '齐全'}</span>
    </div>
    {#each shapeFields(schema, 'target') as field}
      <label class="field">
        <span class="label">{label(field)}</span>
        <input
          value={asText(readPath(step, `target.${field.name}`))}
          placeholder={helpOf(field).slice(0, 20)}
          title={helpOf(field)}
          on:input={(event) => onchange(`target.${field.name}`, event.currentTarget.value)}
        />
      </label>
    {/each}
  {/if}

  {#if plain.length > 0}
    <h2 class="section-head">这一步</h2>
    {#each plain as field}
      {#if field.kind === 'bool'}
        <div class="toggle-row">
          <button
            class="switch"
            class:on={readBool(step, field.name)}
            role="switch"
            aria-checked={readBool(step, field.name)}
            on:click={() => onchange(field.name, !readBool(step, field.name))}
          >
            <span class="knob"></span>
          </button>
          <span>
            {label(field)}
            {#if field.required && !hasValue(readPath(step, field.name))}<span class="need">必填</span>{/if}
          </span>
        </div>
      {:else}
        <div class="field" class:wide={field.kind === 'point' || field.kind === 'region'}>
          <span class="label">
            {label(field)}
            {#if helpOf(field)}<span class="info" title={helpOf(field)}>i</span>{/if}
            {#if field.required && !hasValue(readPath(step, field.name))}<span class="need">必填</span>{/if}
          </span>

          {#if field.kind === 'multiline'}
            <textarea
              rows="3"
              value={readText(step, field.name)}
              on:input={(event) => onchange(field.name, event.currentTarget.value)}
            ></textarea>
          {:else if field.kind === 'int'}
            <input
              type="number"
              min={field.min}
              max={field.max}
              value={asText(readPath(step, field.name))}
              on:input={(event) => commitInt(field.name, event.currentTarget.value, field)}
            />
          {:else if field.kind === 'enum'}
            <select
              value={readText(step, field.name)}
              on:change={(event) => {
                const picked = event.currentTarget.value;
                onchange(field.name, picked === '' ? undefined : picked);
              }}
            >
              {#each field.values ?? [] as value}
                <option value={value}>{field.valueLabels?.[value]?.zh ?? value}</option>
              {/each}
            </select>
          {:else if field.kind === 'point' || field.kind === 'region'}
            <div class="shape-block">
              <!-- 坐标是几个短字段，横排一行；标签在上、数字在下，窄窗口里也能看清。 -->
              {#each shapeFields(schema, field.kind) as sub}
                <label class="field compact">
                  <span class="label">{label(sub)}</span>
                  {#if sub.kind === 'enum'}
                    <select
                      value={readText(step, `${field.name}.${sub.name}`)}
                      on:change={(event) =>
                        onchange(`${field.name}.${sub.name}`, event.currentTarget.value)}
                    >
                      {#each sub.values ?? [] as value}
                        <option value={value}>{sub.valueLabels?.[value]?.zh ?? value}</option>
                      {/each}
                    </select>
                  {:else}
                    <input
                      type="number"
                      value={asText(readPath(step, `${field.name}.${sub.name}`))}
                      on:input={(event) =>
                        commitInt(`${field.name}.${sub.name}`, event.currentTarget.value, sub)}
                    />
                  {/if}
                </label>
              {/each}
              <button
                class="pick"
                on:click={() => onpick(kindOf(field), field.name)}
              >
                {field.kind === 'point' ? '取点' : '取一块'}
              </button>
            </div>
          {:else if field.kind === 'text'}
            <input
              value={readText(step, field.name)}
              on:input={(event) => onchange(field.name, event.currentTarget.value)}
            />
          {:else}
            <p class="readonly">
              {field.kind === 'steps' || field.kind === 'vars' || field.kind === 'stringList'
                ? '这类字段在界面里还改不了，先在 JSON 里写（见下面的折叠块）。'
                : asText(readPath(step, field.name)) || '——'}
            </p>
          {/if}
        </div>
      {/if}
    {/each}
  {/if}

  {#if when}
    <h2 class="section-head">{label(when)}</h2>
    <div class="toggle-row">
      <button
        class="switch"
        class:on={readBool(step, 'when')}
        role="switch"
        aria-checked={readBool(step, 'when')}
        on:click={() => onchange('when', readBool(step, 'when') ? undefined : {})}
      >
        <span class="knob"></span>
      </button>
      <span>{readBool(step, 'when') ? '满足条件才执行' : '每次都执行'}</span>
    </div>

    {#if readBool(step, 'when')}
      {#each shapeFields(schema, 'when') as sub}
        {#if sub.kind === 'bool'}
          <div class="toggle-row">
            <button
              class="switch"
              class:on={readBool(step, `when.${sub.name}`)}
              role="switch"
              aria-checked={readBool(step, `when.${sub.name}`)}
              on:click={() => onchange(`when.${sub.name}`, !readBool(step, `when.${sub.name}`))}
            >
              <span class="knob"></span>
            </button>
            <span>{label(sub)}</span>
          </div>
        {:else if sub.kind === 'enum'}
          <label class="field">
            <span class="label">{label(sub)}</span>
            <select
              value={readText(step, `when.${sub.name}`)}
              on:change={(event) => onchange(`when.${sub.name}`, event.currentTarget.value)}
            >
              {#each sub.values ?? [] as value}
                <option value={value}>{sub.valueLabels?.[value]?.zh ?? value}</option>
              {/each}
            </select>
          </label>
        {:else if sub.kind === 'int'}
          <label class="field">
            <span class="label">{label(sub)}</span>
            <input
              type="number"
              value={asText(readPath(step, `when.${sub.name}`))}
              on:input={(event) => commitInt(`when.${sub.name}`, event.currentTarget.value, sub)}
            />
          </label>
        {:else if sub.kind === 'text'}
          <label class="field">
            <span class="label">{label(sub)}</span>
            <input
              value={readText(step, `when.${sub.name}`)}
              on:input={(event) => onchange(`when.${sub.name}`, event.currentTarget.value)}
            />
          </label>
        {:else}
          <p class="readonly">条件里的「{label(sub)}」先到 JSON 里写（见下面的折叠块）。</p>
        {/if}
      {/each}
    {/if}
  {/if}

  {#if note}
    <label class="field">
      <span class="label">
        {label(note)}
        {#if helpOf(note)}<span class="info" title={helpOf(note)}>i</span>{/if}
      </span>
      <input value={readText(step, 'note')} on:input={(event) => onchange('note', event.currentTarget.value)} />
    </label>
  {/if}
{/if}

<style>
  .side-head {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    margin-bottom: 4px;
  }

  h2 {
    margin: 0;
    font-size: 15px;
    font-weight: 600;
    letter-spacing: 0.02em;
  }

  .section-head {
    margin: 18px 0 8px;
    padding-bottom: 6px;
    border-bottom: 1px solid var(--kp-rule);
    font-size: 14px;
    font-weight: 600;
  }

  .hint {
    font-size: 11px;
    color: var(--kp-ink3);
  }

  .hint.warn {
    color: var(--kp-signal);
  }

  .field {
    display: grid;
    grid-template-columns: 96px 1fr;
    align-items: center;
    gap: 12px;
    margin: 8px 0;
  }

  /* 坐标 / 区域这种"几个短字段凑一块"的，标签单独一行，字段横排——
     挤在 96px 的标签列右边会变成一列数字，看不出来哪个是宽哪个是高。 */
  .field.wide {
    grid-template-columns: 1fr;
    align-items: start;
    gap: 6px;
  }

  .field.wide > .label {
    text-align: left;
  }

  .shape-block {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(76px, 1fr));
    align-items: end;
    gap: 8px;
  }

  .field.compact {
    display: flex;
    flex-direction: column;
    align-items: stretch;
    gap: 4px;
    margin: 0;
  }

  .field.compact > .label {
    text-align: left;
    font-size: 12px;
  }


  .field.compact {
    grid-template-columns: 64px 1fr;
    margin: 4px 0;
  }

  .label {
    font-size: 13px;
    color: var(--kp-ink2);
    text-align: right;
  }

  .info {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 13px;
    height: 13px;
    margin-left: 4px;
    border: 1px solid var(--kp-ink3);
    border-radius: 50%;
    font-size: 9px;
    font-style: normal;
    color: var(--kp-ink3);
    cursor: help;
  }

  .need {
    margin-left: 6px;
    padding: 0 5px;
    border: 1px solid var(--kp-signal);
    border-radius: 3px;
    font-size: 10px;
    color: var(--kp-signal);
  }

  input,
  select,
  textarea {
    box-sizing: border-box;
    width: 100%;
    min-height: 30px;
    padding: 6px 9px;
    border: 1px solid var(--kp-rule);
    border-radius: 4px;
    background: var(--kp-field);
    font: inherit;
    font-size: 13px;
    color: var(--kp-ink);
  }

  input:focus,
  select:focus,
  textarea:focus {
    outline: 2px solid var(--kp-signal);
    outline-offset: -1px;
    background: var(--kp-card);
  }

  textarea {
    resize: vertical;
    line-height: 1.5;
  }

  .toggle-row {
    display: flex;
    align-items: center;
    gap: 10px;
    margin: 8px 0;
    font-size: 13px;
    color: var(--kp-ink);
  }

  .switch {
    flex: none;
    position: relative;
    width: 38px;
    height: 22px;
    padding: 0;
    border: 1px solid var(--kp-rule);
    border-radius: 999px;
    background: var(--kp-field);
    cursor: pointer;
    transition: background 120ms;
  }

  .switch .knob {
    position: absolute;
    top: 2px;
    left: 2px;
    width: 16px;
    height: 16px;
    border-radius: 50%;
    background: var(--kp-ink3);
    transition:
      left 120ms,
      background 120ms;
  }

  .switch.on {
    background: var(--kp-signal);
    border-color: var(--kp-signal);
  }

  .switch.on .knob {
    left: 18px;
    background: #fff;
  }

  .shape-block {
    display: grid;
    grid-template-columns: 1fr auto;
    align-items: end;
    gap: 10px;
  }

  .pick {
    height: 30px;
    padding: 0 12px;
    border: 1px solid var(--kp-signal);
    border-radius: 4px;
    background: transparent;
    font: inherit;
    font-size: 12px;
    color: var(--kp-signal);
    cursor: pointer;
    white-space: nowrap;
    justify-self: end;
  }

  .pick:hover {
    background: var(--kp-signal);
    color: #fff;
  }

  .readonly {
    margin: 0;
    font-size: 12px;
    color: var(--kp-ink3);
  }

  .waiting {
    margin: 12px 0;
    font-size: 13px;
    color: var(--kp-ink3);
  }
</style>
