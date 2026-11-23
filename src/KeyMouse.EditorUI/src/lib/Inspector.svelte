<script lang="ts">
  /**
   * 这一步的设置面板。
   *
   * 字段不在这里写死：形状、标签、必填、取值范围全部来自命令行给出的契约
   * （`KeyMouse schema`，见 `docs/reference/interface.md`）。老外壳按契约渲染，
   * 新外壳照做才对得上——否则契约加一个字段，两边就得各改一次，迟早走样。
   *
   * 目标窗口只在该出现的地方出现：
   * - `focus` / `wait-window` 这类硬性必填的步骤，放在最上面；
   * - 坐标/区域一旦落在客户区（或整个窗口）里，换算需要窗口，就摆在坐标块下方。
   * 屏幕绝对坐标不需要窗口，这里不显示它，免得填了也没用。
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
  import { branchesOf, type BranchKey, type FlowStep } from './model';
  import type { NodePath } from './tree';
  import InspectorShape from './InspectorShape.svelte';
  import KeyValueEditor from './KeyValueEditor.svelte';
  import ConditionEditor from './ConditionEditor.svelte';

  export let schema: FlowSchema | null = null;
  export let step: object;
  export let path: NodePath;
  export let onchange: (path: string, value: unknown) => void;
  export let onpick: (kind: 'point' | 'region', path: string) => void;
  export let onaddbranch: (parent: NodePath, branch: BranchKey) => void = () => undefined;

  $: type = String((step as Record<string, unknown>).type ?? '');
  // 契约把 delayMs / note / when / shot 放在 common 里，每种步骤都自带——老外壳也是拼起来渲染的。
  // shot 不在这里画：证据图有自己的块（要读文件、要能"在文件夹里看"）。
  $: fields = schema
    ? [...fieldsOf(schema, type), ...schema.common]
        .filter((field) => field.name !== 'shot')
        .filter((field) => visibleBy(field, step as Record<string, unknown>))
    : [];
  $: target = fields.find((field) => field.kind === 'target') ?? null;
  // 前提条件不再在这里填：流程控制交给 if；引擎仍然读 `when`，只是界面不提供。
  $: note = fields.find((field) => field.name === 'note') ?? null;
  $: plain = fields.filter(
    (field) =>
      field.kind !== 'target' &&
      field.kind !== 'when' &&
      field.kind !== 'steps' &&
      field.name !== 'note' &&
      (field.kind !== 'probe' || showProbe),
  );
  $: condText = readText(step, 'cond');
  // probe 只在条件真的读屏（text(...)）或已经填过时出现：`while (i < 3)` 不该逼人填窗口。
  $: showProbe = readPath(step, 'probe') !== undefined || /\btext\s*\(/.test(condText);
  $: targetEmpty =
    target !== null &&
    schema !== null &&
    shapeFields(schema, 'target').every((field) => !hasValue(readPath(step, `target.${field.name}`)));
  $: coords = plain.filter((field) => field.kind === 'point' || field.kind === 'region');
  // 至少有一处"真的在用相对坐标"的坐标，才在坐标下方给"目标窗口"：
  // 刚插入的空白步骤还没有坐标，不该先催窗口；滑钮一旦拨到客户区、或取点/填了数，就出现。
  $: relativeCoords = coords.some((field) => isRelativeShape(field) && coordFilled(field));
  $: targetHard = target?.required === true;
  $: showTargetTop = target !== null && targetHard;
  $: showTargetBelow = target !== null && !targetHard && relativeCoords;
  $: branches = branchesOf(step as FlowStep);
  // 旧文件里的 when 仍会被引擎执行；界面不再提供填写，但必须把它"演"出来——否则用户看见的
  // 是一步普通的点击，运行时却可能因为旧前提条件跳过，排查会绕大圈。
  $: hasWhen = hasValue(readPath(step, 'when'));

  function hasValue(value: unknown): boolean {
    if (value === null || value === undefined) return false;
    if (typeof value === 'string') return value.trim() !== '';
    if (Array.isArray(value)) return value.length > 0;
    return true;
  }

  /** `space` 没写就是契约里的默认值——引擎和界面必须按同一个默认解释。 */
  function shapeSpace(field: FieldContract): string {
    const written = readText(step, `${field.name}.space`);
    if (written !== '') return written;
    const space = schema ? shapeFields(schema, field.kind).find((item) => item.name === 'space') : undefined;
    return asText(space?.default ?? 'client');
  }

  /** 客户区坐标要窗口坐标换算；整窗区域也要窗口；屏幕区域什么都不要。 */
  function isRelativeShape(field: FieldContract): boolean {
    return field.kind === 'region' ? shapeSpace(field) !== 'screen' : shapeSpace(field) === 'client';
  }

  /** 坐标是否已经"在用了"：写了空间、或填了 x/y。空白的步骤不催目标窗口。 */
  function coordFilled(field: FieldContract): boolean {
    if (readText(step, `${field.name}.space`) !== '') return true;
    return typeof readPath(step, `${field.name}.x`) === 'number' || typeof readPath(step, `${field.name}.y`) === 'number';
  }

  function spaceContract(field: FieldContract): FieldContract | undefined {
    return schema ? shapeFields(schema, field.kind).find((item) => item.name === 'space') : undefined;
  }

  function toggleSpace(field: FieldContract) {
    const contract = spaceContract(field);
    if (!contract) return;
    const next = isRelativeShape(field) ? 'screen' : 'client';
    commitEnum(`${field.name}.space`, next, contract);
  }

  function commitSpace(field: FieldContract, value: string) {
    const contract = spaceContract(field);
    if (contract) commitEnum(`${field.name}.space`, value, contract);
  }

  function kindOf(field: FieldContract): 'point' | 'region' {
    return field.kind === 'region' ? 'region' : 'point';
  }

  function asText(value: unknown): string {
    return value === null || value === undefined ? '' : String(value);
  }

  /** `vars` 字段在 JSON 里是对象，Svelte 模板里不能直接写 TS 的 `as`，收在这里。 */
  function varsOf(target: object, name: string): Record<string, string> | undefined {
    const value = readPath(target, name);
    return value !== null && typeof value === 'object' && !Array.isArray(value)
      ? (value as Record<string, string>)
      : undefined;
  }

  /**
   * 下拉框显示什么：步骤里写了就用它，没写就用契约里的默认值。
   *
   * 左键点击是最常见的例子——文件里根本不写 `button`，契约说默认 `left`，
   * 结果下拉框一片空白，看着像"这一项没了"。显示默认值不等于写进文件：
   * 改动时仍然只在**不同于默认值**的时候才落键（见 commitEnum）。
   */
  function enumText(step: unknown, field: FieldContract): string {
    const written = readText(step, field.name);
    if (written !== '') return written;
    return asText(field.default);
  }

  /** 枚举框：选回默认值就把这个键删掉，别让默认值混进流程文件。 */
  function commitEnum(path: string, picked: string, field: FieldContract) {
    if (picked === '') {
      onchange(path, undefined);
      return;
    }
    onchange(path, picked === asText(field.default) ? undefined : picked);
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
  <p class="waiting">正在读契约…</p>
{:else}
  {#if showTargetTop && target}
    <div class="side-head">
      <h2>
        {label(target)}
        {#if targetEmpty}<span class="need" title="必填">*</span>{/if}
      </h2>
    </div>
    {#each shapeFields(schema, 'target') as field}
      <label class="field">
        <span class="label">{label(field)}</span>
        <input
          value={asText(readPath(step, `target.${field.name}`))}
          title={helpOf(field)}
          on:input={(event) => onchange(`target.${field.name}`, event.currentTarget.value)}
        />
      </label>
    {/each}
  {/if}

  {#if plain.length > 0}
    <h2 class="section-head">这一步</h2>
    {#each plain as field}
      {#if field.kind === 'probe'}
        <div class="field wide">
          <span class="label">
            {label(field)}
            {#if helpOf(field)}<span class="info" title={helpOf(field)}>i</span>{/if}
          </span>
          <div class="probe-card">
            <InspectorShape
              {schema}
              {step}
              fields={shapeFields(schema, 'probe')}
              prefix="probe"
              {onchange}
              {onpick}
            />
          </div>
        </div>
      {:else if field.kind === 'vars'}
        <div class="field">
          <span class="label">
            {label(field)}
            {#if helpOf(field)}<span class="info" title={helpOf(field)}>i</span>{/if}
          </span>
          <KeyValueEditor
            value={varsOf(step, field.name)}
            onchange={(value) => onchange(field.name, value)}
            keyPlaceholder="名字"
            valuePlaceholder={type === 'call' ? '值或 {{占位符}}' : '表达式'}
            addLabel="加一项"
          />
        </div>
      {:else if field.kind === 'bool'}
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
            {#if field.required && !hasValue(readPath(step, field.name))}<span class="need" title="必填">*</span>{/if}
          </span>
        </div>
      {:else}
        <div
          class="field"
          class:wide={field.kind === 'point' || field.kind === 'region' || field.name === 'cond'}
        >
          <span class="label">
            {label(field)}
            {#if helpOf(field)}<span class="info" title={helpOf(field)}>i</span>{/if}
            {#if field.required && !hasValue(readPath(step, field.name))}<span class="need" title="必填">*</span>{/if}
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
              placeholder={field.name === 'delayMs' && readPath(step, 'atMs') != null ? '沿用旧时间线' : asText(field.default)}
              min={field.min}
              max={field.max}
              value={asText(readPath(step, field.name))}
              on:input={(event) => commitInt(field.name, event.currentTarget.value, field)}
            />
          {:else if field.kind === 'enum'}
            <select
              value={enumText(step, field)}
              on:change={(event) => commitEnum(field.name, event.currentTarget.value, field)}
            >
              {#each field.values ?? [] as value}
                <option value={value}>{field.valueLabels?.[value]?.zh ?? value}</option>
              {/each}
            </select>
          {:else if field.kind === 'point' || field.kind === 'region'}
            <div class="coord">
              {#if spaceContract(field)}
                {#if (spaceContract(field)?.values ?? []).length === 2}
                  <!-- 两选项的空间用滑钮：左"屏幕"、右"客户区"，并且把当前值写出来。 -->
                  <div class="space-row">
                    <span class="space-side" class:on={!isRelativeShape(field)}>屏幕</span>
                    <button
                      class="switch"
                      class:on={isRelativeShape(field)}
                      role="switch"
                      aria-checked={isRelativeShape(field)}
                      aria-label="客户区相对坐标"
                      title={isRelativeShape(field) ? '后台客户区：直接操作或读取目标窗口，不移动系统鼠标' : '屏幕绝对坐标'}
                      on:click={() => toggleSpace(field)}
                    >
                      <span class="knob"></span>
                    </button>
                    <span class="space-side" class:on={isRelativeShape(field)}>客户区 · 后台</span>
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
                      value={asText(readPath(step, `${field.name}.${sub.name}`))}
                      on:input={(event) =>
                        commitInt(`${field.name}.${sub.name}`, event.currentTarget.value, sub)}
                    />
                  </label>
                {/each}
              </div>

              <button
                class="pick"
                on:click={() => onpick(kindOf(field), field.name)}
              >
                {field.kind === 'point' ? '取点' : '取一块'}
              </button>
            </div>
          {:else if field.kind === 'text' && field.name === 'cond'}
            <ConditionEditor
              value={readText(step, 'cond')}
              onchange={(value) => onchange('cond', value)}
            />
          {:else if field.kind === 'text' && field.name === 'value'}
            <input
              class="expression"
              spellcheck="false"
              value={readText(step, field.name)}
              placeholder={'例如 n + 1 或 "你好" + name'}
              on:input={(event) => onchange(field.name, event.currentTarget.value)}
            />
          {:else if field.kind === 'text'}
            <input
              value={readText(step, field.name)}
              on:input={(event) => onchange(field.name, event.currentTarget.value)}
            />
          {:else if field.kind === 'stringList'}
            <p class="readonly">先写 JSON。</p>
          {:else}
            <p class="readonly">
              {asText(readPath(step, field.name)) || '——'}
            </p>
          {/if}
        </div>
      {/if}
    {/each}
  {/if}

  {#if branches.length > 0}
    <h2 class="section-head">分支</h2>
    <div class="branches">
      {#each branches as branch}
        <div class="branch-row">
          <span class="branch-name">{branch.label}</span>
          <span class="branch-count">{branch.steps.length}</span>
          <button class="branch-add" type="button" on:click={() => onaddbranch(path, branch.key)}>
            添加
          </button>
        </div>
      {/each}
    </div>
  {/if}

  {#if showTargetBelow && target}
    <h2 class="section-head">{label(target)}</h2>
    <p class="target-note">
      {coords.some((field) => field.kind === 'region' && shapeSpace(field) === 'window')
        ? '整窗区域需要窗口。'
        : '客户区坐标需要窗口。'}
    </p>
    {#each shapeFields(schema, 'target') as field}
      <label class="field">
        <span class="label">{label(field)}</span>
        <input
          value={asText(readPath(step, `target.${field.name}`))}
          title={helpOf(field)}
          on:input={(event) => onchange(`target.${field.name}`, event.currentTarget.value)}
        />
      </label>
    {/each}
  {/if}

  {#if hasWhen}
    <div class="legacy">
      <p>
        旧版前提条件仍在生效。
      </p>
      <button class="pick" type="button" on:click={() => onchange('when', undefined)}>
        删掉
      </button>
    </div>
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

  /* 检查器的段标题：衬线、字距略开，和列表里的步骤名同一套字 */
  h2 {
    margin: 0;
    font: 500 16px/1.4 var(--kp-font-serif);
    letter-spacing: 0.03em;
  }

  .section-head {
    margin: 18px 0 8px;
    padding-bottom: 6px;
    border-bottom: 1px solid var(--kp-rule);
    font-size: 14px;
    font-weight: 600;
  }

  .target-note {
    margin: 0 0 6px;
    font-size: 12px;
    color: var(--kp-ink3);
    line-height: 1.5;
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

  /* 坐标块：空间开关在上，x|y 与 dx|dy 两列排开，取点按钮靠右收尾。 */
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
    font-size: 12px;
  }

  .space-side {
    color: var(--kp-ink3);
    transition: color 120ms;
  }

  .space-side.on {
    color: var(--kp-ink);
    font-weight: 600;
  }

  .space-select {
    flex: 1;
    min-height: 28px;
    padding: 3px 8px;
    font-size: 12px;
    border: 1px solid var(--kp-rule);
    border-radius: 4px;
    background: var(--kp-field);
    color: var(--kp-ink);
  }

  .coords {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 6px 10px;
  }

  .field.compact {
    display: flex;
    flex-direction: column;
    align-items: stretch;
    gap: 3px;
    margin: 0;
  }

  .field.compact > .label {
    text-align: left;
    font-size: 12px;
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
    margin-left: 5px;
    color: var(--kp-signal);
    font-size: 14px;
    line-height: 1;
    font-weight: 600;
  }

  .branches {
    display: grid;
    gap: 4px;
  }

  .branch-row {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 6px 8px;
    border: 1px solid var(--kp-rule);
    border-radius: 4px;
    background: var(--kp-field);
    font-size: 12.5px;
  }

  .branch-name {
    color: var(--kp-ink);
  }

  .branch-count {
    color: var(--kp-ink3);
    font-family: var(--kp-font-mono);
    font-size: 11px;
  }

  .branch-add {
    margin-left: auto;
    padding: 2px 9px;
    border: 1px solid var(--kp-rule);
    border-radius: 3px;
    background: var(--kp-card);
    color: var(--kp-ink2);
    font: 500 11.5px/1.5 var(--kp-font);
    cursor: pointer;
  }

  .branch-add:hover {
    border-color: var(--kp-signal);
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

  /* 条件表达式用等宽字：`i < 3` 里的空格和括号该看得出来，衬线正文会把它们糊在一起。 */
  .expression {
    font-family: var(--kp-font-mono);
    font-size: 12.5px;
    letter-spacing: 0;
  }

  /* 嵌套的观察点：只用一道发丝线表示从属，不套第二层卡片。 */
  .probe-card {
    margin: 2px 0 4px;
    padding: 2px 0 2px 12px;
    border-left: 1px solid var(--kp-rule);
  }

  .pick {
    align-self: flex-end;
    height: 28px;
    padding: 0 12px;
    border: 1px solid var(--kp-signal);
    border-radius: 4px;
    background: transparent;
    font: inherit;
    font-size: 12px;
    color: var(--kp-signal);
    cursor: pointer;
    white-space: nowrap;
  }

  /* 坐标块里的"取点/取一块"横跨整行：它是这一步的主操作，不该缩在右下角。 */
  .coord > .pick {
    align-self: stretch;
    width: 100%;
    height: 30px;
  }

  .pick:hover {
    background: var(--kp-signal);
    color: #fff;
  }

  /* 旧版前提条件的提示：不是填写区，只负责让"看不见的条件"露出来，并给一个删掉的出口。 */
  .legacy {
    margin-top: 16px;
    padding: 10px 12px;
    border: 1px solid var(--kp-rule);
    border-left: 3px solid var(--kp-signal);
    background: var(--kp-paper);
    font-size: 12px;
    line-height: 1.55;
    color: var(--kp-ink2);
  }

  .legacy p {
    margin: 0 0 8px;
  }

  .legacy .pick {
    display: block;
    margin-left: auto;
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
