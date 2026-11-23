<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import { invoke } from '@tauri-apps/api/core';
  import FlowCanvas from './FlowCanvas.svelte';
  import Inspector from './Inspector.svelte';
  import { ChildFlowSession, adoptChildSession, retainChildSession, retainedChildSession, childEditorIdentity, childErrorMessage, type OpenChildFlow } from './childflow';
  import { currentSchema, pendingSchema, schemaFrom, type FlowSchema } from './schema';
  import { stepTitle, type BranchKey, type FlowStep, type StepType } from './model';
  import { STEP_TEMPLATES, makeStep } from './steps';
  import { insertAt, moveNodes, parentPath as nodeParent, replaceAt, sequenceOf, stepAt, type NodePath } from './tree';
  import { writePath } from './fieldpath';
  import { shouldAttachWindow } from './pick';

  export let parentPath: string | null;
  export let flow: string;
  export let ancestorPaths: string[] = [];
  /** Parent should supply an identity stable across temporary remounts. */
  export let identity: string = childEditorIdentity();
  export let ondirty: (key: string, dirty: boolean) => void = () => undefined;

  let mounted = false;
  let requestKey = '';
  let generation = 0;
  let loading = false;
  let error = '';
  let schemaError = '';
  let session: ChildFlowSession | null = null;
  let subscribedSession: ChildFlowSession | null = null;
  let unsubscribeSession = (): void => undefined;
  $: if (session !== subscribedSession) {
    unsubscribeSession();
    subscribedSession = session;
    const active = session;
    unsubscribeSession = active?.subscribe(() => {
      retainChildSession(active);
      if (mounted) session = active;
      else reportDirty(identity, active.pendingChanges, ondirty);
    }) ?? (() => undefined);
  }
  let schema: FlowSchema | null = currentSchema();
  let selected: NodePath | null = null;
  let addType: StepType = 'sleep';
  let fitToken = 0;
  let reportedKey = '';
  let reportedDirty = false;
  let reportedCallback = ondirty;

  $: desiredKey = JSON.stringify([identity, parentPath, flow, ancestorPaths]);
  $: dirty = session?.dirty ?? false;
  $: pendingChanges = session?.pendingChanges ?? false;
  $: path = session?.path ?? '';
  $: steps = session?.document.steps ?? [];
  $: current = selected ? stepAt(steps, selected) : undefined;
  $: changedReference = requestKey !== '' && requestKey !== desiredKey;
  // A changed call reference cannot replace an unsaved child document.
  $: if (mounted && desiredKey !== requestKey && !loading && !pendingChanges) {
    void load(desiredKey);
  }
  $: if (session || error) reportDirty(identity, pendingChanges, ondirty);

  function reportDirty(key: string, value: boolean, callback: typeof ondirty): void {
    if (key === reportedKey && value === reportedDirty && callback === reportedCallback) return;
    if (reportedKey && (reportedKey !== key || callback !== reportedCallback)) reportedCallback(reportedKey, false);
    reportedKey = key;
    reportedDirty = value;
    reportedCallback = callback;
    if (key) callback(key, value);
  }

  async function load(key = desiredKey): Promise<void> {
    if (session?.pendingChanges) return;
    const ticket = ++generation;
    requestKey = key;
    loading = true;
    error = '';
    const retained = retainedChildSession(identity);
    if (retained) {
      session = retained;
      selected = null;
      fitToken += 1;
      loading = false;
      return;
    }
    try {
      const opened = await invoke<OpenChildFlow>('open_child_flow', { parentPath, flow, ancestors: ancestorPaths });
      if (ticket !== generation || !mounted) return;
      session = adoptChildSession(opened, identity);
      selected = null;
      fitToken += 1;
    } catch (cause) {
      if (ticket === generation && mounted) {
        session = null;
        error = `无法打开子流程：${childErrorMessage(cause)}`;
      }
    } finally {
      if (ticket === generation) loading = false;
    }
  }

  async function loadSchema(): Promise<void> {
    schemaError = '';
    try {
      schema = currentSchema() ?? await (pendingSchema() ?? invoke<{ json: string } | string>('flow_schema').then((payload) => schemaFrom(typeof payload === 'string' ? payload : payload.json)));
    } catch (cause) {
      schemaError = `无法读取字段格式：${childErrorMessage(cause)}`;
    }
  }

  function updateSteps(next: FlowStep[]): void {
    if (!session || session.saving) return;
    session.document = { ...session.document, steps: next };
    session = session;
  }

  function applyField(field: string, value: unknown): void {
    if (!session || !selected || session.saving) return;
    try {
      session.editField(selected, field, value);
      session = session;
    } catch (cause) { error = childErrorMessage(cause); }
  }

  function add(parent: NodePath = [], branch: BranchKey = 'steps', index?: number): void {
    if (!session || session.saving) return;
    const at = index ?? sequenceOf(steps, parent, branch).length;
    updateSteps(insertAt(steps, parent, branch, at, makeStep(addType)));
    selected = [...parent, { branch, index: at }];
  }

  function quickAdd(target: NodePath, position: 'before' | 'after'): void {
    const last = target[target.length - 1];
    if (last) add(nodeParent(target), last.branch, last.index + (position === 'after' ? 1 : 0));
  }

  /** 检查器「分支 · 添加」：插到该分支末尾并选中。 */
  function addBranch(parent: NodePath, branch: BranchKey): void {
    add(parent, branch);
  }

  function remove(): void {
    if (!selected || !session || session.saving) return;
    try {
      session.remove(selected);
      session = session;
      selected = null;
    } catch (cause) { error = childErrorMessage(cause); }
  }

  const childCallbacks = new Map<string, { step: FlowStep; callback: typeof ondirty }>();
  function childCallback(step: FlowStep): typeof ondirty {
    const active = session!;
    const childKey = active.childIdentity(step);
    let record = childCallbacks.get(childKey);
    if (!record) {
      const entry = { step, callback: (_key: string, _value: boolean): void => undefined };
      entry.callback = (key: string, value: boolean): void => {
        if (!active.reportChild(key, entry.step, value)) return;
        retainChildSession(active);
        if (session === active) session = active;
        if (!mounted) reportDirty(identity, active.pendingChanges, ondirty);
      };
      record = entry;
      childCallbacks.set(childKey, record);
    }
    record.step = step;
    return record.callback;
  }

  function handleKeydown(event: KeyboardEvent): void {
    if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 's') return;
    event.preventDefault();
    event.stopPropagation();
    void save();
  }

  function move(sources: NodePath[], parent: NodePath, branch: BranchKey, index: number): void {
    if (!session || session.saving) return;
    const moved = moveNodes(steps, sources, parent, branch, index);
    updateSteps(moved.steps);
    selected = moved.moved[0] ?? selected;
  }

  async function save(): Promise<void> {
    const active = session;
    if (!active || active.saving) return;
    error = '';
    const pending = active.save((args) => invoke('save_child_flow', args));
    session = active;
    try {
      await pending;
    } catch (cause) {
      error = `子流程未保存，编辑已保留。若文件已被外部修改，请先协调文件内容再保存：${childErrorMessage(cause)}`;
    } finally {
      retainChildSession(active);
      if (session === active) session = active;
      if (!mounted) reportDirty(identity, active.pendingChanges, ondirty);
    }
  }

  async function pickField(kind: 'point' | 'region', field: string): Promise<void> {
    const active = session;
    const target = selected;
    const targetStep = target ? stepAt(steps, target) : undefined;
    if (!active || !target || active.saving) return;
    try {
      const picked = await invoke<{ space: string; x: number; y: number; width: number; height: number; process?: string; title?: string } | null>('pick_region', { point: kind === 'point' });
      if (!picked || session !== active || active.saving || stepAt(active.document.steps, target) !== targetStep) return;
      const step = stepAt(active.document.steps, target);
      if (!step) return;
      let next = writePath(step, field, { space: picked.space, x: picked.x, y: picked.y, ...(kind === 'region' ? { width: picked.width, height: picked.height } : {}) });
      if (shouldAttachWindow(picked.space) && picked.process) {
        const owner = field.includes('.') ? field.slice(0, field.lastIndexOf('.') + 1) : '';
        next = writePath(next, `${owner}target.process`, picked.process);
        if (picked.title) next = writePath(next, `${owner}target.title`, picked.title);
      }
      active.document = { ...active.document, steps: replaceAt(active.document.steps, target, next as unknown as FlowStep) };
      session = active;
    } catch (cause) {
      error = `取点失败：${childErrorMessage(cause)}`;
    }
  }

  onMount(() => { mounted = true; void loadSchema(); });
  onDestroy(() => {
    mounted = false;
    generation += 1;
    if (session) retainChildSession(session);
    unsubscribeSession();
    // A dirty unmounted descendant remains reported until remount/save or explicit discard.
    if (reportedKey && !session?.pendingChanges) reportedCallback(reportedKey, false);
  });
</script>

<!-- svelte-ignore a11y-no-noninteractive-element-interactions -->
<section class="child-flow" aria-label="子流程编辑器" aria-busy={loading || session?.saving} on:keydown={handleKeydown}>
  <header>
    <div class="file"><strong>子流程</strong><span title={path || flow}>{path || flow || '尚未指定流程文件'}</span></div>
    <span class="status" class:dirty aria-live="polite">{session?.saving ? '保存中…' : dirty ? '未保存' : session ? '已保存' : ''}</span>
    <button class="save" disabled={!session || !dirty || session.saving} on:click={save}>保存子流程</button>
  </header>
  {#if loading}
    <p class="message" role="status">正在加载子流程…</p>
  {:else if session}
    {#if changedReference}<p class="message warning">调用路径已改变；请先保存当前子流程及其未保存的孙流程，再打开新路径。</p>{/if}
    <div class="toolbar">
      <label>新增节点 <select bind:value={addType} disabled={session.saving}>{#each STEP_TEMPLATES as template}<option value={template.type}>{stepTitle(template.type)}</option>{/each}</select></label>
      <button disabled={session.saving} on:click={() => add()}>添加到末尾</button>
      <button disabled={!selected || session.saving} on:click={remove}>删除选中节点</button>
    </div>
    <div class="workspace" class:has-inspector={current !== undefined}>
      <div class="child-canvas">
        <FlowCanvas {steps} {selected} {fitToken} freeLayout={false}
          onselect={(target) => selected = target} onquickadd={quickAdd}
          onmove={move}>
          <svelte:fragment slot="child" let:node>
            {#key session.childIdentity(node.step)}
              <svelte:self identity={session.childIdentity(node.step)} parentPath={path} flow={node.step.flow ?? ''} ancestorPaths={[...ancestorPaths, path]} ondirty={childCallback(node.step)} />
            {/key}
          </svelte:fragment>
        </FlowCanvas>
      </div>
      {#if current}
        <aside aria-label="子流程节点设置">
          <h3>{stepTitle(current.type)}</h3>
          {#if schemaError}<p class="message error">{schemaError}</p><button on:click={loadSchema}>重试读取字段</button>{/if}
          <fieldset disabled={session.saving}><Inspector {schema} step={current} path={selected ?? []} onchange={applyField} onpick={pickField} onaddbranch={addBranch} /></fieldset>
        </aside>
      {/if}
    </div>
  {/if}
  {#if error}<div class="message error" role="alert"><p>{error}</p>{#if !session}<button on:click={() => load()}>重试打开</button>{/if}</div>{/if}
</section>

<style>
  .child-flow { display: flex; flex-direction: column; width: 100%; height: 100%; min-width: 0; min-height: 0; box-sizing: border-box; overflow: hidden; border: 1px solid var(--kp-rule-strong); background: var(--kp-card); color: var(--kp-ink); text-align: left; }
  header { flex: none; display: flex; align-items: center; gap: 12px; padding: 10px 12px; border-bottom: 1px solid var(--kp-rule-strong); }
  .file { display: flex; flex: 1; align-items: baseline; gap: 12px; min-width: 0; }
  .file strong { flex: none; font: 500 16px var(--kp-font-serif); }
  .file span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font: 11px var(--kp-font-mono); color: var(--kp-ink2); }
  .status { white-space: nowrap; font-size: 11px; color: var(--kp-ink3); }
  .status.dirty, .warning { color: var(--kp-signal); }
  button, select { min-height: 30px; padding: 4px 10px; border: 1px solid var(--kp-rule); border-radius: 2px; background: var(--kp-card); color: var(--kp-ink); font: 12px var(--kp-font); }
  button { cursor: pointer; }
  button:disabled { opacity: .5; cursor: default; }
  button:not(:disabled):hover { border-color: var(--kp-signal); }
  button:focus-visible, select:focus-visible { outline: 2px solid var(--kp-signal); outline-offset: 2px; }
  .save { border-color: var(--kp-signal); color: var(--kp-signal); white-space: nowrap; }
  .toolbar { flex: none; display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 8px 12px; border-bottom: 1px solid var(--kp-rule); }
  .toolbar label { display: flex; align-items: center; gap: 8px; font-size: 12px; }
  .workspace { flex: 1; display: grid; grid-template-columns: minmax(0, 1fr); min-height: 0; overflow: hidden; }
  .workspace.has-inspector { grid-template-columns: minmax(240px, 1fr) minmax(280px, 340px); }
  .child-canvas { position: relative; min-width: 0; min-height: 0; overflow: hidden; }
  aside { min-width: 0; min-height: 0; overflow: auto; padding: 12px; border-left: 1px solid var(--kp-rule-strong); }
  h3 { margin: 0 0 12px; font: 500 16px var(--kp-font-serif); }
  fieldset { min-width: 0; padding: 0; margin: 0; border: 0; }
  .message { flex: none; max-height: 110px; overflow-y: auto; margin: 0; padding: 12px; font-size: 12px; line-height: 1.6; overflow-wrap: anywhere; }
  .message p { margin: 0 0 8px; }
  .error { color: var(--kp-critical); border-top: 1px solid var(--kp-rule); }
  @media (max-width: 680px) {
    header { flex-wrap: wrap; }
    .file { flex-basis: 100%; }
    .workspace.has-inspector { grid-template-columns: minmax(0, 1fr); grid-template-rows: minmax(120px, 1fr) minmax(0, 1fr); }
    aside { border-left: 0; border-top: 1px solid var(--kp-rule-strong); }
  }
</style>
