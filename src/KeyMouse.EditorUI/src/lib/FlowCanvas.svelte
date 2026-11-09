<script lang="ts">
  /**
   * 无限画布。
   *
   * 不显示"左边一条列表 + 右边检查器"，而是把整份流程铺开：从上到下是顺序，往右一格是
   * 进入某个块。三种轮廓对应三组操作——流程控制（六边形）、循环控制（胶囊）、操作指令
   * （纸卡）——眼睛先认形状，再读字。
   *
   * 交互只有三件：滚轮缩放、拖空白平移、拖节点换位置；点节点由外面的抽屉展示详情。
   */
  import { onMount } from 'svelte';
  import StepIcon from './StepIcon.svelte';
  import {
    layoutFlow,
    type CanvasGap,
    type CanvasNode,
  } from './canvas';
  import { branchesOf, stepSummary, stepTitle, type BranchKey, type FlowStep } from './model';
  import { missingHint, missingOf } from './steps';
  import { isPathPrefix, samePath, type NodePath } from './tree';
  import type { StepResult } from './run';

  export let steps: FlowStep[] = [];
  export let selected: NodePath | null = null;
  /** Ctrl/⌘ 多选出来的节点；拖动其中任意一个时会一起搬。 */
  export let selectedPaths: NodePath[] = [];
  export let results: Record<string, StepResult> = {};
  /** 换一份文件/录完一次时 +1，让画布把内容铺到适合阅读的位置。 */
  export let fitToken = 0;

  export let onselect: (path: NodePath | null, additive?: boolean, range?: boolean) => void = () => undefined;
  export let onmove: (
    from: NodePath[],
    parent: NodePath,
    branch: BranchKey,
    index: number,
  ) => void = () => undefined;
  export let onadd: (parent: NodePath, branch: BranchKey) => void = () => undefined;
  export let onrange: (paths: NodePath[]) => void = () => undefined;

  let viewport: HTMLDivElement;
  let scale = 1;
  let ox = 0;
  let oy = 0;
  let lastFitToken = -1;
  let fittedOnce = false;

  $: layout = layoutFlow(steps);
  $: if (fitToken !== lastFitToken) {
    lastFitToken = fitToken;
    void homeSoon();
  }

  function clamp(value: number, min: number, max: number): number {
    return Math.max(min, Math.min(max, value));
  }

  async function homeSoon(): Promise<void> {
    await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
    home();
  }

  /**
   * 首屏：先让人看清前几个动作，而不是为了"全都塞进来"缩到 30%。
   * 整份流程始终在同一张画布上；要看全貌点左下角「适配」。
   */
  function home(): void {
    if (!viewport) return;
    const rect = viewport.getBoundingClientRect();
    if (rect.width < 40 || rect.height < 40) return;
    const usableW = Math.max(80, rect.width - 96);
    const next = Math.min(0.9, usableW / Math.max(layout.width, 1));
    scale = clamp(next, 0.5, 0.9);
    ox = Math.max(28, (rect.width - Math.min(layout.width, rect.width / scale)) / 2);
    oy = 24;
    fittedOnce = true;
  }

  function fit(): void {
    if (!viewport) return;
    const rect = viewport.getBoundingClientRect();
    if (rect.width < 40 || rect.height < 40) return;
    const pad = 56;
    const usableW = Math.max(80, rect.width - pad * 2);
    const usableH = Math.max(80, rect.height - pad * 2);
    const next = Math.min(1, usableW / layout.width, usableH / layout.height);
    scale = clamp(next, 0.2, 1.1);
    ox = (rect.width - layout.width * scale) / 2;
    oy = (rect.height - layout.height * scale) / 2;
    fittedOnce = true;
  }

  function zoomAround(factor: number, px?: number, py?: number): void {
    if (!viewport) return;
    const rect = viewport.getBoundingClientRect();
    const cx = px ?? rect.width / 2;
    const cy = py ?? rect.height / 2;
    const next = clamp(scale * factor, 0.25, 1.8);
    const wx = (cx - ox) / scale;
    const wy = (cy - oy) / scale;
    scale = next;
    ox = cx - wx * next;
    oy = cy - wy * next;
  }

  function onWheel(event: WheelEvent): void {
    event.preventDefault();
    if (!viewport) return;
    const rect = viewport.getBoundingClientRect();
    const factor = Math.exp(-event.deltaY * 0.0012);
    zoomAround(factor, event.clientX - rect.left, event.clientY - rect.top);
  }

  // ---------------------------------------------------------------- 平移 / 框选

  let pan: { startX: number; startY: number; baseX: number; baseY: number; moved: boolean } | null =
    null;
  let marquee: {
    startX: number;
    startY: number;
    currentX: number;
    currentY: number;
    active: boolean;
  } | null = null;
  let marqueeHits: NodePath[] = [];
  let suppressContextMenuUntil = 0;

  function isControl(target: EventTarget | null): boolean {
    return target instanceof Element &&
      target.closest('button, input, .node, .canvas-control') !== null;
  }

  function onViewportDown(event: PointerEvent): void {
    if (isControl(event.target)) return;
    // 右键/中键按住拖动 = 平移画布；左键按住拖动 = 框选。
    if (event.button === 1 || event.button === 2) {
      pan = {
        startX: event.clientX,
        startY: event.clientY,
        baseX: ox,
        baseY: oy,
        moved: false,
      };
      (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
      return;
    }
    if (event.button !== 0) return;
    marquee = {
      startX: event.clientX,
      startY: event.clientY,
      currentX: event.clientX,
      currentY: event.clientY,
      active: false,
    };
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  }

  function onViewportMove(event: PointerEvent): void {
    if (pan) {
      const dx = event.clientX - pan.startX;
      const dy = event.clientY - pan.startY;
      if (Math.hypot(dx, dy) > 3) pan.moved = true;
      ox = pan.baseX + dx;
      oy = pan.baseY + dy;
      return;
    }
    if (!marquee) return;
    marquee.currentX = event.clientX;
    marquee.currentY = event.clientY;
    if (!marquee.active && Math.hypot(event.clientX - marquee.startX, event.clientY - marquee.startY) > 3) {
      marquee.active = true;
    }
    if (marquee.active) marqueeHits = nodesInMarquee(marquee);
  }

  function onViewportUp(event: PointerEvent): void {
    if (pan) {
      const moved = pan.moved;
      pan = null;
      // 右键拖完松在节点上时，浏览器还会补一个 contextmenu；别让它把节点选中。
      suppressContextMenuUntil = Date.now() + 350;
      (event.currentTarget as HTMLElement).releasePointerCapture?.(event.pointerId);
      if (!moved) onselect(null);
      return;
    }
    if (!marquee) return;
    const finished = marquee;
    const hits = marqueeHits;
    marquee = null;
    marqueeHits = [];
    (event.currentTarget as HTMLElement).releasePointerCapture?.(event.pointerId);
    if (finished.active && hits.length > 0) {
      onrange(hits);
      return;
    }
    // 空白处轻点或拉了一个空框：收起抽屉，不改文档。
    onselect(null);
  }

  function nodesInMarquee(box: { startX: number; startY: number; currentX: number; currentY: number }): NodePath[] {
    if (!viewport) return [];
    const rect = viewport.getBoundingClientRect();
    const x1 = Math.min(box.startX, box.currentX);
    const x2 = Math.max(box.startX, box.currentX);
    const y1 = Math.min(box.startY, box.currentY);
    const y2 = Math.max(box.startY, box.currentY);
    const wx1 = (x1 - rect.left - ox) / scale;
    const wx2 = (x2 - rect.left - ox) / scale;
    const wy1 = (y1 - rect.top - oy) / scale;
    const wy2 = (y2 - rect.top - oy) / scale;
    return layout.nodes
      .filter((node) => node.x < wx2 && node.x + node.w > wx1 && node.y < wy2 && node.y + node.h > wy1)
      .map((node) => node.path);
  }

  function marqueeStyle(box: { startX: number; startY: number; currentX: number; currentY: number }): string {
    if (!viewport) return '';
    const rect = viewport.getBoundingClientRect();
    const left = Math.min(box.startX, box.currentX) - rect.left;
    const top = Math.min(box.startY, box.currentY) - rect.top;
    const width = Math.abs(box.currentX - box.startX);
    const height = Math.abs(box.currentY - box.startY);
    return `left: ${left}px; top: ${top}px; width: ${width}px; height: ${height}px;`;
  }

  // ---------------------------------------------------------------- 拖动排序

  let press: {
    node: CanvasNode;
    paths: NodePath[];
    startX: number;
    startY: number;
    pointerId: number;
  } | null = null;
  let draggingPaths: NodePath[] = [];
  let dragOffset = { x: 0, y: 0 };
  let dropGap: CanvasGap | null = null;
  let pressTimer: ReturnType<typeof setTimeout> | null = null;
  let longPressed = false;
  const LONG_PRESS_MS = 450;

  function clearPressTimer(): void {
    if (pressTimer !== null) {
      clearTimeout(pressTimer);
      pressTimer = null;
    }
  }

  function onNodeDown(event: PointerEvent, node: CanvasNode): void {
    if (event.button !== 0) return;
    // 节点自己处理拖动，别让背景跟着平移。
    event.stopPropagation();
    longPressed = false;
    clearPressTimer();
    const inSelection = selectedPaths.some((path) => samePath(path, node.path));
    const paths = inSelection && selectedPaths.length > 0
      ? selectedPaths.map((path) => path.map((entry) => ({ ...entry })))
      : [node.path];
    press = {
      node,
      paths,
      startX: event.clientX,
      startY: event.clientY,
      pointerId: event.pointerId,
    };
    // Windows 图标那套：按住不动先进入选择，再拖就整组搬；轻点才是打开抽屉。
    pressTimer = setTimeout(() => {
      pressTimer = null;
      const current = press;
      if (!current) return;
      longPressed = true;
      const already = selectedPaths.some((path) => samePath(path, current.node.path));
      if (!already) {
        const base = selectedPaths.map((path) => path.map((entry) => ({ ...entry })));
        current.paths = [...base, current.node.path];
        onselect(current.node.path, true);
      }
    }, LONG_PRESS_MS);
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  }

  function onNodeMove(event: PointerEvent): void {
    if (!press) return;
    const dx = event.clientX - press.startX;
    const dy = event.clientY - press.startY;
    if (Math.hypot(dx, dy) >= 5) clearPressTimer();
    if (draggingPaths.length === 0 && Math.hypot(dx, dy) < 5) return;
    if (draggingPaths.length === 0) draggingPaths = press.paths;
    dragOffset = { x: dx / scale, y: dy / scale };
    dropGap = nearestGap(event, draggingPaths);
  }

  function onNodeUp(event: PointerEvent): void {
    if (!press) return;
    clearPressTimer();
    const current = press.node;
    const paths = press.paths;
    const wasDragging = draggingPaths.length > 0;
    const target = dropGap;
    const held = longPressed;
    press = null;
    draggingPaths = [];
    dropGap = null;
    dragOffset = { x: 0, y: 0 };
    longPressed = false;
    (event.currentTarget as HTMLElement).releasePointerCapture?.(event.pointerId);

    if (!wasDragging) {
      if (!held) onselect(current.path, event.ctrlKey || event.metaKey, event.shiftKey);
      return;
    }
    if (target) {
      onmove(paths, target.parentPath, target.branch, target.index);
    }
  }

  function nearestGap(event: PointerEvent, paths: NodePath[]): CanvasGap | null {
    if (!viewport) return null;
    const rect = viewport.getBoundingClientRect();
    const wx = (event.clientX - rect.left - ox) / scale;
    const wy = (event.clientY - rect.top - oy) / scale;

    // 拖到任意节点卡片上 = 放到这个节点后面（同父、同分支）。
    // 这样二级拖到一级、三级拖到一级、一级拖到三级都能用同一个手势完成；
    // 只把"落进被搬节点自己的子树"排除掉，避免成环。
    const host = layout.nodes.find((node) => {
      if (paths.some((path) => isPathPrefix(path, node.path))) return false;
      const bottom = node.depth === 0 ? 24 : 6;
      return wx >= node.x && wx <= node.x + node.w &&
        wy >= node.y - 4 && wy <= node.y + node.h + bottom;
    });
    if (host) {
      return {
        parentPath: host.parentPath,
        branch: host.branch,
        index: host.index + 1,
        x: host.x,
        y: host.y + host.h + 4,
        w: host.w,
        h: 18,
      };
    }

    // 任何分支的缝隙都可以落；唯一不允许的是落进被搬节点自己的子树。
    const candidates = layout.gaps.filter(
      (gap) => !paths.some((path) => isPathPrefix(path, gap.parentPath)),
    );
    let best: CanvasGap | null = null;
    let bestDistance = Infinity;
    for (const gap of candidates) {
      const dx = wx - (gap.x + gap.w / 2);
      const dy = wy - (gap.y + gap.h / 2);
      const distance = Math.hypot(dx, dy);
      if (distance < bestDistance) {
        bestDistance = distance;
        best = gap;
      }
    }
    return bestDistance < 320 ? best : null;
  }

  function onNodeKey(event: KeyboardEvent, node: CanvasNode): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onselect(node.path);
    }
  }

  function onNodeContext(event: MouseEvent, node: CanvasNode): void {
    if (Date.now() < suppressContextMenuUntil) return;
    onselect(node.path);
  }

  function childCount(node: CanvasNode): number {
    return branchesOf(node.step).reduce((count, branch) => count + branch.steps.length, 0);
  }

  // 第一次拿到真实尺寸时再适配一次：启动瞬间 viewport 还是 0×0，fit 会算出负偏移。
  onMount(() => {
    const observer = new ResizeObserver(() => {
      if (!fittedOnce) home();
    });
    if (viewport) observer.observe(viewport);
    void homeSoon();
    return () => {
      observer.disconnect();
      clearPressTimer();
    };
  });
</script>

<div
  class="canvas"
  class:panning={pan !== null}
  bind:this={viewport}
  role="presentation"
  style="background-position: {ox}px {oy}px; background-size: {Math.max(15, 24 * scale)}px {Math.max(15, 24 * scale)}px;"
  on:pointerdown={onViewportDown}
  on:pointermove={onViewportMove}
  on:pointerup={onViewportUp}
  on:pointercancel={onViewportUp}
  on:contextmenu|preventDefault
  on:wheel|nonpassive={onWheel}
>
  <div
    class="world"
    style="width: {layout.width}px; height: {layout.height}px; transform: translate3d({ox}px, {oy}px, 0) scale({scale});"
  >
    <svg class="edges" width={layout.width} height={layout.height} aria-hidden="true">
      {#each layout.edges as edge}
        <path d={edge.d} class={edge.kind} />
      {/each}
    </svg>

    {#each layout.labels as label}
      <div
        class="branch-label"
        class:else-branch={label.branch === 'else'}
        style="left: {label.x}px; top: {label.y}px; width: {label.w}px; height: {label.h}px;"
      >
        <span>{label.text}</span>
      </div>
    {/each}

    {#each layout.empties as empty}
      <button
        class="empty-branch"
        style="left: {empty.x}px; top: {empty.y}px; width: {empty.w}px; height: {empty.h}px;"
        on:pointerdown|stopPropagation={() => undefined}
        on:click|stopPropagation={() => onadd(empty.parentPath, empty.branch)}
        title={empty.branch === 'else' ? '给否则分支加第一步' : '给循环体加第一步'}
      >
        <span class="plus" aria-hidden="true">＋</span>
        <span>{empty.text}里还没有步骤</span>
      </button>
    {/each}

    {#each layout.nodes as node}
      {@const result = results[node.origin]}
      {@const missing = missingOf(node.step)}
      {@const count = childCount(node)}
      <div
        class="node shape-{node.category}"
        class:selected={selectedPaths.some((path) => samePath(path, node.path)) ||
          (selectedPaths.length === 0 && samePath(selected, node.path))}
        class:primary={samePath(selected, node.path)}
        class:dragging={draggingPaths.some((path) => samePath(path, node.path))}
        class:marquee-hit={marqueeHits.some((path) => samePath(path, node.path))}
        class:failed={result !== undefined && !result.ok}
        role="button"
        tabindex="0"
        aria-label={`${stepTitle(node.step.type)}，${node.origin}`}
        style="left: {node.x}px; top: {node.y}px; width: {node.w}px; height: {node.h}px; {draggingPaths.some((path) => samePath(path, node.path))
          ? `transform: translate(${dragOffset.x}px, ${dragOffset.y}px);`
          : ''}"
        on:pointerdown={(event) => onNodeDown(event, node)}
        on:pointermove={onNodeMove}
        on:pointerup={onNodeUp}
        on:pointercancel={onNodeUp}
        on:keydown={(event) => onNodeKey(event, node)}
        on:contextmenu|preventDefault|stopPropagation={(event) => onNodeContext(event, node)}
      >
        <span class="plate" aria-hidden="true"></span>
        <div class="node-inner">
          <div class="node-head">
            <span class="node-cat">{node.origin}</span>
          </div>
          <div class="node-title">
            <span class="node-icon" aria-hidden="true"><StepIcon name={node.step.type} size={12} /></span>
            <span>{stepTitle(node.step.type)}</span>
          </div>
          <div class="node-summary">{stepSummary(node.step)}</div>
          <div class="node-tags">
            {#if result}
              <span class="node-result" class:bad={!result.ok} title={result.text}>
                {result.ok ? '✓' : '✗'}{#if result.dry}<span class="dry">检查</span>{/if}
                <span class="result-text">{result.text}</span>
              </span>
            {/if}
            {#if missing.length > 0}
              <span class="node-missing" title={missingHint(node.step)}>待补</span>
            {/if}
            {#if count > 0}
              <span class="node-count">{count} 子步</span>
            {/if}
            {#if node.step.shot}
              <span class="node-shot" title="这一步有录制时的证据图" aria-label="有证据图">▣</span>
            {/if}
          </div>
        </div>
      </div>
    {/each}

    {#if draggingPaths.length > 0 && dropGap}
      <div
        class="drop-line"
        style="left: {dropGap.x}px; top: {dropGap.y + dropGap.h / 2}px; width: {dropGap.w}px;"
      >
        <span></span>
      </div>
    {/if}
  </div>

  {#if marquee && marquee.active}
    <div class="marquee" style={marqueeStyle(marquee)} aria-hidden="true"></div>
  {/if}

  {#if steps.length === 0}
    <div class="empty-canvas">
      <p class="kicker">空白画布</p>
      <p>从上面「＋ 添加」里选一个操作，或先加载一份流程。</p>
    </div>
  {/if}

  <div class="canvas-hint" aria-hidden="true">滚轮缩放 · 左键拖空白框选 · 右键拖空白平移</div>

  {#if draggingPaths.length > 1}
    <div class="drag-badge" aria-live="polite">拖动 {draggingPaths.length} 个节点</div>
  {/if}

  <div class="zoom-controls canvas-control" aria-label="画布缩放">
    <button on:click={() => zoomAround(1 / 1.15)} aria-label="缩小" title="缩小">−</button>
    <button class="zoom-value" on:click={fit} title="适配全部">{Math.round(scale * 100)}%</button>
    <button on:click={() => zoomAround(1.15)} aria-label="放大" title="放大">＋</button>
    <span class="zoom-sep" aria-hidden="true"></span>
    <button class="fit" on:click={fit} title="把全部步骤放回视野">适配</button>
  </div>
</div>

<style>
  .canvas {
    position: absolute;
    inset: 0;
    overflow: hidden;
    background-color: var(--kp-paper);
    background-image: radial-gradient(circle, var(--kp-rule) 1px, transparent 1.1px);
    cursor: grab;
    touch-action: none;
  }

  .canvas.panning {
    cursor: grabbing;
  }

  .world {
    position: absolute;
    left: 0;
    top: 0;
    transform-origin: 0 0;
  }

  .edges {
    position: absolute;
    left: 0;
    top: 0;
    overflow: visible;
    pointer-events: none;
  }

  .edges path {
    fill: none;
    stroke: var(--kp-rule-strong);
    stroke-width: 1.1;
    stroke-linecap: round;
    stroke-linejoin: round;
    opacity: 0.62;
  }

  .edges path.branch {
    stroke-dasharray: 3 4;
    opacity: 0.48;
  }

  /* ── 分支标题：一条极轻的线把"进入哪条路"说明白 ─────────────── */
  .branch-label {
    position: absolute;
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 2px 10px 0;
    font: 500 10.5px/1 var(--kp-font-mono);
    letter-spacing: 0.12em;
    color: var(--kp-ink2);
    pointer-events: none;
  }

  .branch-label span {
    white-space: nowrap;
    background: var(--kp-paper);
    padding-right: 7px;
  }

  .branch-label::after {
    content: '';
    flex: 1;
    height: 1px;
    background: var(--kp-rule);
  }

  .branch-label.else-branch {
    color: var(--kp-ink2);
  }

  /* ── 空分支：虚线槽，点一下就有第一步 ────────────────────── */
  .empty-branch {
    position: absolute;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 7px;
    border: 1px dashed var(--kp-rule-strong);
    border-radius: 10px;
    background: color-mix(in srgb, var(--kp-card) 55%, transparent);
    color: var(--kp-ink3);
    font: 500 11px/1.2 var(--kp-font);
    cursor: pointer;
  }

  .empty-branch:hover {
    border-color: var(--kp-signal);
    color: var(--kp-signal);
    background: var(--kp-card);
  }

  .plus {
    font-family: var(--kp-font-mono);
    font-size: 13px;
  }

  /* ── 三种轮廓 ───────────────────────────────────────────── */
  .node {
    position: absolute;
    cursor: grab;
    outline: none;
    user-select: none;
    z-index: 1;
    transition: filter 120ms ease;
  }

  .node:active {
    cursor: grabbing;
  }

  .node.dragging {
    z-index: 8;
    opacity: 0.72;
    transition: none;
  }

  .plate,
  .node-inner {
    position: absolute;
    inset: 0;
  }

  .plate {
    background: var(--kp-rule-strong);
  }

  .node-inner {
    inset: 1px;
    display: flex;
    flex-direction: column;
    gap: 2px;
    overflow: hidden;
    background: var(--kp-card);
    color: var(--kp-ink);
  }

  .shape-flow .plate,
  .shape-flow .node-inner {
    clip-path: polygon(7% 0, 93% 0, 100% 50%, 93% 100%, 7% 100%, 0 50%);
  }

  .shape-flow .plate {
    background: var(--kp-ink);
  }

  .shape-flow .node-inner {
    padding: 8px 24px;
  }

  .shape-loop .plate,
  .shape-loop .node-inner {
    border-radius: 999px;
  }

  .shape-loop .plate {
    background: var(--kp-ink);
  }

  .shape-loop .node-inner {
    padding: 8px 20px;
  }

  .shape-action .plate,
  .shape-action .node-inner {
    border-radius: 10px;
  }

  .shape-action .plate {
    background: var(--kp-ink);
  }

  .shape-action .node-inner {
    padding: 8px 14px 8px 16px;
  }

  .node:hover .plate {
    background: var(--kp-ink);
  }

  .node.selected .plate {
    background: var(--kp-signal);
  }

  .node.selected .node-inner {
    inset: 2px;
    background: var(--kp-selected);
  }

  .node.marquee-hit .plate {
    background: var(--kp-signal);
  }

  .node.marquee-hit .node-inner {
    inset: 2px;
    background: var(--kp-selected);
  }

  .node.primary .node-inner {
    box-shadow: inset 0 0 0 1px var(--kp-signal);
  }

  .node.failed .plate {
    background: var(--kp-critical);
  }

  .node.failed.selected .plate {
    background: var(--kp-signal);
  }

  .node:not(.selected):focus-visible .plate {
    background: var(--kp-ink);
  }

  .node-head {
    display: flex;
    justify-content: flex-end;
    align-items: baseline;
    gap: 10px;
    min-height: 9px;
    font: 500 9px/1 var(--kp-font-mono);
    letter-spacing: 0.08em;
    color: var(--kp-ink3);
    text-transform: uppercase;
  }

  .shape-flow .node-head {
    padding: 0 8px;
  }

  .node-cat {
    font-weight: 400;
    letter-spacing: 0.04em;
    color: var(--kp-ink3);
  }

  .node-title {
    display: flex;
    align-items: center;
    gap: 7px;
    font: 500 14px/1.18 var(--kp-font-serif);
    letter-spacing: 0.02em;
    color: var(--kp-ink);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .node-icon {
    display: inline-flex;
    color: var(--kp-signal);
  }

  .node-summary {
    min-height: 16px;
    overflow: hidden;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    line-clamp: 2;
    -webkit-box-orient: vertical;
    font-size: 10px;
    line-height: 1.25;
    color: var(--kp-ink2);
    word-break: break-word;
  }

  .node-tags {
    margin-top: auto;
    display: flex;
    align-items: center;
    gap: 9px;
    min-height: 13px;
    font: 500 9.5px/1 var(--kp-font-mono);
    color: var(--kp-ink3);
    white-space: nowrap;
    overflow: hidden;
  }

  .node-result {
    display: inline-flex;
    align-items: baseline;
    gap: 4px;
    max-width: 150px;
    color: var(--kp-ink2);
    overflow: hidden;
  }

  .node-result.bad {
    color: var(--kp-critical);
  }

  .node-result .dry {
    font-size: 8px;
    color: var(--kp-signal);
  }

  .node-result .result-text {
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .node-missing {
    color: var(--kp-signal);
    letter-spacing: 0.04em;
  }

  .node-count,
  .node-shot {
    color: var(--kp-ink3);
  }

  .node-shot {
    color: var(--kp-signal);
  }

  /* ── 拖动落点 ───────────────────────────────────────────── */
  .marquee {
    position: absolute;
    border: 1px solid var(--kp-ink2);
    background: transparent;
    pointer-events: none;
    z-index: 30;
  }

  .drop-line {
    position: absolute;
    height: 2px;
    transform: translateY(-1px);
    background: var(--kp-signal);
    pointer-events: none;
    z-index: 12;
  }

  .drop-line span {
    position: absolute;
    left: -4px;
    top: -3px;
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--kp-signal);
  }

  /* ── 画布上的固定控件 ───────────────────────────────────── */
  .canvas-hint {
    position: absolute;
    right: 18px;
    bottom: 18px;
    padding: 6px 9px;
    border: 1px solid var(--kp-rule);
    border-radius: 4px;
    background: color-mix(in srgb, var(--kp-card) 86%, transparent);
    font: 500 9.5px/1 var(--kp-font-mono);
    letter-spacing: 0.04em;
    color: var(--kp-ink3);
    pointer-events: none;
  }

  .drag-badge {
    position: absolute;
    left: 50%;
    top: 16px;
    transform: translateX(-50%);
    padding: 7px 11px;
    border: 1px solid var(--kp-signal);
    border-radius: 999px;
    background: var(--kp-card);
    color: var(--kp-signal);
    font: 500 11px/1 var(--kp-font-mono);
    letter-spacing: 0.04em;
    pointer-events: none;
    z-index: 50;
  }

  .zoom-controls {
    position: absolute;
    left: 18px;
    bottom: 18px;
    display: flex;
    align-items: center;
    gap: 2px;
    padding: 3px;
    border: 1px solid var(--kp-rule);
    border-radius: 5px;
    background: var(--kp-card);
  }

  .zoom-controls button {
    min-width: 26px;
    height: 26px;
    padding: 0 7px;
    border-radius: 3px;
    font: 500 11px/1 var(--kp-font-mono);
    color: var(--kp-ink2);
    cursor: pointer;
  }

  .zoom-controls button:hover {
    background: var(--kp-field);
    color: var(--kp-ink);
  }

  .zoom-controls .zoom-value {
    min-width: 48px;
    color: var(--kp-ink3);
    font-size: 10px;
  }

  .zoom-controls .fit {
    font-family: var(--kp-font);
    font-size: 10px;
  }

  .zoom-sep {
    width: 1px;
    height: 14px;
    margin: 0 3px;
    background: var(--kp-rule);
  }

  .empty-canvas {
    position: absolute;
    left: 50%;
    top: 47%;
    transform: translate(-50%, -50%);
    width: min(340px, 70vw);
    text-align: center;
    color: var(--kp-ink3);
    pointer-events: none;
  }

  .empty-canvas .kicker {
    margin: 0 0 8px;
    font: 500 10px/1 var(--kp-font-mono);
    letter-spacing: 0.18em;
    text-transform: uppercase;
    color: var(--kp-signal);
  }

  .empty-canvas p:last-child {
    margin: 0;
    font-size: 12px;
    line-height: 1.7;
  }
</style>
