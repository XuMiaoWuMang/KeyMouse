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
    applyNodePositions,
    layoutFlow,
    type CanvasGap,
    type CanvasNode,
    type NodePosition,
  } from './canvas';
  import { buildGraph, graphEdgeKey, type GraphEdge } from './graph';
  import {
    nearestPort,
    routeEdge,
    type EdgePort,
    type EdgePorts,
    type RoutingRect,
    type RoutedEdge,
  } from './routing';
  import { branchesOf, stepSummary, stepTitle, type BranchKey, type FlowStep } from './model';
  import { missingHint, missingOf } from './steps';
  import { isPathPrefix, pathKey, samePath, type NodePath } from './tree';
  import type { StepResult } from './run';

  export let steps: FlowStep[] = [];
  export let selected: NodePath | null = null;
  /** Ctrl/⌘ 多选出来的节点；拖动其中任意一个时会一起搬。 */
  export let selectedPaths: NodePath[] = [];
  export let results: Record<string, StepResult> = {};
  /** 换一份文件/录完一次时 +1，让画布把内容铺到适合阅读的位置。 */
  export let fitToken = 0;
  /** 自由布局：拖动节点改变位置；关掉以后恢复结构视图，拖动用于换排序。 */
  export let freeLayout = true;
  /** 自由布局的手工坐标；没有坐标的节点沿用自动布局。 */
  export let positions: Record<string, NodePosition> = {};

  /** 按边的语义键保存的手工接口；缺省时按两个节点的夹角自动选边。 */
  export let ports: Record<string, EdgePorts> = {};
  export let onportchange: (key: string, end: EdgeEnd, port: EdgePort) => void = () => undefined;
  export let onportreset: (key: string) => void = () => undefined;

  /** 选中节点四向箭头：before = 插在它前面，after = 插在它后面（同父、同分支）。 */
  export let onquickadd: (path: NodePath, position: 'before' | 'after') => void = () => undefined;

  export let onselect: (path: NodePath | null, additive?: boolean, range?: boolean) => void = () => undefined;
  export let onmove: (
    from: NodePath[],
    parent: NodePath,
    branch: BranchKey,
    index: number,
  ) => void = () => undefined;
  export let onmovefree: (moves: { path: NodePath; x: number; y: number }[]) => void = () => undefined;
  export let onadd: (parent: NodePath, branch: BranchKey) => void = () => undefined;
  export let onrange: (paths: NodePath[]) => void = () => undefined;

  type EdgeEnd = 'from' | 'to';
  type DrawnEdge = GraphEdge & RoutedEdge & {
    key: string;
    fromRect: RoutingRect;
    toRect: RoutingRect;
  };

  /**
   * 逻辑坐标的 0 点左边/上边预留一块空间，再把这整块画布平移进视口。
   * 这样节点可以拖到负数坐标，而不会被 `.canvas` 的 overflow:hidden 裁掉。
   * 它只影响渲染和视口换算；流程 JSON 和 localStorage 里仍然是逻辑坐标。
   */
  const WORLD_ORIGIN = 4096;

  let viewport: HTMLDivElement;
  let scale = 1;
  let ox = 0;
  let oy = 0;
  let lastFitToken = -1;
  let fittedOnce = false;
  // 拖动状态要和连线几何放在同一组响应式更新里：拖动过程中边也按预览坐标重画，
  // 否则卡片已经挪走、箭头还停在旧位置，松手时才"跳"过去。
  let draggingPaths: NodePath[] = [];
  let dragOffset = { x: 0, y: 0 };
  /** 点中的线会露出两个端点圆点；拖圆点改接口，双击线恢复自动。 */
  let activeEdgeKey: string | null = null;
  let portDrag: {
    key: string;
    end: EdgeEnd;
    rect: RoutingRect;
    port: EdgePort;
  } | null = null;

  $: baseLayout = layoutFlow(steps);
  $: layout = freeLayout ? applyNodePositions(baseLayout, positions) : baseLayout;
  /**
   * 内容真实包围盒：负坐标节点也要算进「适配」里，否则拖到左边以后点适配，
   * 视图只围住正坐标那一半。空分支按钮也算，它跟着块走。
   */
  $: contentBounds = (() => {
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (const node of layout.nodes) {
      minX = Math.min(minX, node.x);
      minY = Math.min(minY, node.y);
      maxX = Math.max(maxX, node.x + node.w);
      maxY = Math.max(maxY, node.y + node.h);
    }
    for (const empty of layout.empties) {
      minX = Math.min(minX, empty.x);
      minY = Math.min(minY, empty.y);
      maxX = Math.max(maxX, empty.x + empty.w);
      maxY = Math.max(maxY, empty.y + empty.h);
    }
    if (!Number.isFinite(minX)) {
      return { minX: 0, minY: 0, maxX: layout.width, maxY: layout.height, width: layout.width, height: layout.height };
    }
    return {
      minX,
      minY,
      maxX,
      maxY,
      width: Math.max(1, maxX - minX),
      height: Math.max(1, maxY - minY),
    };
  })();
  /** 只给单选节点显示四向快速添加入口；多选时拖动/批量操作优先。 */
  $: quickTargetKey = selectedPaths.length === 1
    ? pathKey(selectedPaths[0])
    : selectedPaths.length === 0 && selected
      ? pathKey(selected)
      : null;
  /**
   * 拖一个块时，块里的子步骤跟着一起走——它们属于这个块，单独留在原地会让
   * 分支和箭头断开。选中的如果是普通步骤，集合里就只有它自己。
   */
  $: previewPaths = (() => {
    const keys = new Set<string>();
    if (!freeLayout || draggingPaths.length === 0) return keys;
    for (const node of layout.nodes) {
      if (draggingPaths.some((path) => isPathPrefix(path, node.path))) keys.add(pathKey(node.path));
    }
    return keys;
  })();
  $: nodeByPath = new Map(layout.nodes.map((node) => {
    const key = pathKey(node.path);
    const moved = previewPaths.has(key);
    return [key, moved ? { ...node, x: node.x + dragOffset.x, y: node.y + dragOffset.y } : node];
  }));
  $: graphEdges = (() => {
    // 直接读一次 props，确保 Svelte 把 ports 算进这条响应式语句的依赖里；
    // 只在 flatMap 回调里读时，props 更新不一定会触发重算。
    const portOverrides = ports;
    return buildGraph(steps).flatMap((edge): DrawnEdge[] => {
      const from = nodeByPath.get(pathKey(edge.from));
      const to = nodeByPath.get(pathKey(edge.to));
      if (!from || !to) return [];
      const key = graphEdgeKey(edge);
      const saved = portOverrides[key];
      const override = portDrag?.key === key
        ? { ...saved, [portDrag.end]: portDrag.port }
        : saved;
      const routed = routeEdge(from, to, {
        kind: edge.kind,
        fromPort: override?.from,
        toPort: override?.to,
        obstacles: Array.from(nodeByPath.values()),
      });
      return [{
        ...edge,
        key,
        fromRect: from,
        toRect: to,
        ...routed,
        labelY: routed.labelY + labelOffset(edge.kind),
      }];
    });
  })();
  $: activeEdge = activeEdgeKey
    ? graphEdges.find((edge) => edge.key === activeEdgeKey) ?? null
    : null;
  $: if (fitToken !== lastFitToken) {
    lastFitToken = fitToken;
    void homeSoon();
  }

  function labelOffset(kind: GraphEdge['kind']): number {
    if (kind === 'true' || kind === 'loop') return -8;
    if (kind === 'false' || kind === 'exit') return 10;
    return 0;
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
    const next = Math.min(0.9, usableW / Math.max(contentBounds.width, 1));
    scale = clamp(next, 0.5, 0.9);
    const left = Math.max(28, (rect.width - Math.min(contentBounds.width, rect.width / scale)) / 2);
    // 内容包在固定原点的 world-content 里；视口偏移要把原点一起扣掉，否则负数区域
    // 虽然存在，却会被推到屏幕外。
    ox = left - (WORLD_ORIGIN + contentBounds.minX) * scale;
    oy = 24 - (WORLD_ORIGIN + contentBounds.minY) * scale;
    fittedOnce = true;
  }

  function fit(): void {
    if (!viewport) return;
    const rect = viewport.getBoundingClientRect();
    if (rect.width < 40 || rect.height < 40) return;
    const pad = 56;
    const usableW = Math.max(80, rect.width - pad * 2);
    const usableH = Math.max(80, rect.height - pad * 2);
    const next = Math.min(1, usableW / contentBounds.width, usableH / contentBounds.height);
    scale = clamp(next, 0.2, 1.1);
    ox = (rect.width - contentBounds.width * scale) / 2 - (WORLD_ORIGIN + contentBounds.minX) * scale;
    oy = (rect.height - contentBounds.height * scale) / 2 - (WORLD_ORIGIN + contentBounds.minY) * scale;
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
    activeEdgeKey = null;
    portDrag = null;
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
    if (portDrag) {
      onPortMove(event);
      return;
    }
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
    if (portDrag) {
      onPortUp();
      return;
    }
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
    const wx1 = (x1 - rect.left - ox) / scale - WORLD_ORIGIN;
    const wx2 = (x2 - rect.left - ox) / scale - WORLD_ORIGIN;
    const wy1 = (y1 - rect.top - oy) / scale - WORLD_ORIGIN;
    const wy2 = (y2 - rect.top - oy) / scale - WORLD_ORIGIN;
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

  // ---------------------------------------------------------------- 连线接口

  function activateEdge(key: string): void {
    activeEdgeKey = key;
    portDrag = null;
    onselect(null);
  }

  function selectEdge(event: PointerEvent, key: string): void {
    event.stopPropagation();
    activateEdge(key);
  }

  function onPortDown(event: PointerEvent, edge: DrawnEdge, end: EdgeEnd): void {
    event.stopPropagation();
    activeEdgeKey = edge.key;
    portDrag = {
      key: edge.key,
      end,
      rect: end === 'from' ? edge.fromRect : edge.toRect,
      port: end === 'from' ? edge.fromPort : edge.toPort,
    };
  }

  function onPortMove(event: PointerEvent): void {
    if (!portDrag || !viewport) return;
    const rect = viewport.getBoundingClientRect();
    const wx = (event.clientX - rect.left - ox) / scale - WORLD_ORIGIN;
    const wy = (event.clientY - rect.top - oy) / scale - WORLD_ORIGIN;
    const port = nearestPort(portDrag.rect, wx, wy);
    if (port.side === portDrag.port.side && Math.abs(port.t - portDrag.port.t) < 0.001) return;
    portDrag = { ...portDrag, port };
  }

  function onPortUp(): void {
    if (!portDrag) return;
    const current = portDrag;
    portDrag = null;
    onportchange(current.key, current.end, current.port);
  }

  function onWindowPortUp(): void {
    if (portDrag) onPortUp();
  }

  // ---------------------------------------------------------------- 拖动排序

  let press: {
    node: CanvasNode;
    paths: NodePath[];
    startX: number;
    startY: number;
    pointerId: number;
  } | null = null;
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
    activeEdgeKey = null;
    portDrag = null;
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
    dropGap = freeLayout ? null : nearestGap(event, draggingPaths);
  }

  function onNodeUp(event: PointerEvent): void {
    if (!press) return;
    clearPressTimer();
    const current = press.node;
    const paths = press.paths;
    const movedKeys = new Set(previewPaths);
    const wasDragging = draggingPaths.length > 0;
    const target = dropGap;
    const offset = { ...dragOffset };
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
    if (freeLayout) {
      // 一个块连同它的所有后代一起落盘，箭头才能在下一帧继续接在正确的边上。
      const moves = layout.nodes.flatMap((node) => {
        if (!movedKeys.has(pathKey(node.path))) return [];
        return [{
          path: node.path,
          x: Math.round(node.x + offset.x),
          y: Math.round(node.y + offset.y),
        }];
      });
      if (moves.length > 0) onmovefree(moves);
      return;
    }
    if (target) {
      onmove(paths, target.parentPath, target.branch, target.index);
    }
  }

  function nearestGap(event: PointerEvent, paths: NodePath[]): CanvasGap | null {
    if (!viewport) return null;
    const rect = viewport.getBoundingClientRect();
    const wx = (event.clientX - rect.left - ox) / scale - WORLD_ORIGIN;
    const wy = (event.clientY - rect.top - oy) / scale - WORLD_ORIGIN;

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

<svelte:window on:pointerup={onWindowPortUp} on:pointercancel={onWindowPortUp} />

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
    style="width: {layout.width + WORLD_ORIGIN * 2}px; height: {layout.height + WORLD_ORIGIN * 2}px; transform: translate3d({ox}px, {oy}px, 0) scale({scale});"
  >
    <div
      class="world-content"
      style="width: {layout.width}px; height: {layout.height}px; transform: translate3d({WORLD_ORIGIN}px, {WORLD_ORIGIN}px, 0);"
    >
    <svg
      class="edges"
      width={layout.width + WORLD_ORIGIN * 2}
      height={layout.height + WORLD_ORIGIN * 2}
      viewBox={`${-WORLD_ORIGIN} ${-WORLD_ORIGIN} ${layout.width + WORLD_ORIGIN * 2} ${layout.height + WORLD_ORIGIN * 2}`}
      style={`left: ${-WORLD_ORIGIN}px; top: ${-WORLD_ORIGIN}px;`}
      aria-hidden="true"
    >
      <defs>
        <marker
          id="km-arrow"
          viewBox="0 0 8 8"
          refX="7"
          refY="4"
          markerWidth="5.5"
          markerHeight="5.5"
          orient="auto-start-reverse"
        >
          <path d="M 0 0 L 8 4 L 0 8 Z" fill="var(--kp-rule-strong)"></path>
        </marker>
        <marker
          id="km-arrow-active"
          viewBox="0 0 8 8"
          refX="7"
          refY="4"
          markerWidth="5.5"
          markerHeight="5.5"
          orient="auto-start-reverse"
        >
          <path d="M 0 0 L 8 4 L 0 8 Z" fill="var(--kp-signal)"></path>
        </marker>
      </defs>
      {#each graphEdges as edge}
        <g
          class="edge-group {edge.kind}"
          class:active={activeEdgeKey === edge.key}
          class:dragging-port={portDrag?.key === edge.key}
        >
          <path
            class="edge-hit"
            d={edge.d}
            role="button"
            tabindex="0"
            aria-label="选中这条连线，拖动两端圆点改接口"
            on:pointerdown={(event) => selectEdge(event, edge.key)}
            on:keydown={(event) => {
              if (event.key !== 'Enter' && event.key !== ' ') return;
              event.preventDefault();
              activateEdge(edge.key);
            }}
            on:dblclick|stopPropagation|preventDefault={() => onportreset(edge.key)}
          ></path>
          <path
            class="edge-path {edge.kind}"
            d={edge.d}
            marker-end={activeEdgeKey === edge.key ? 'url(#km-arrow-active)' : 'url(#km-arrow)'}
          ></path>
          {#if edge.label}
            <text class="edge-label {edge.kind}" x={edge.labelX} y={edge.labelY} text-anchor="middle" dominant-baseline="middle">{edge.label}</text>
          {/if}
        </g>
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
        style="left: {empty.x}px; top: {empty.y}px; width: {empty.w}px; height: {empty.h}px; {previewPaths.has(pathKey(empty.parentPath))
          ? `transform: translate(${dragOffset.x}px, ${dragOffset.y}px);`
          : ''}"
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
        class:dragging={previewPaths.has(pathKey(node.path))}
        class:marquee-hit={marqueeHits.some((path) => samePath(path, node.path))}
        class:failed={result !== undefined && !result.ok}
        role="button"
        tabindex="0"
        aria-label={`${stepTitle(node.step.type)}，${node.origin}`}
        style="left: {node.x}px; top: {node.y}px; width: {node.w}px; height: {node.h}px; {previewPaths.has(pathKey(node.path))
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

        {#if quickTargetKey === pathKey(node.path) && draggingPaths.length === 0}
          <div class="quick-add" aria-label="快速添加上一步或下一步">
            <button
              class="quick-step top"
              type="button"
              aria-label="在前面插入一步"
              title="在前面插入一步"
              on:pointerdown|stopPropagation={() => undefined}
              on:click|stopPropagation={() => onquickadd(node.path, 'before')}
            >
              <svg viewBox="0 0 12 12" width="11" height="11" aria-hidden="true">
                <path d="M6 1.6 10.4 7H7.6v3.4H4.4V7H1.6Z" fill="currentColor"></path>
              </svg>
            </button>
            <button
              class="quick-step left"
              type="button"
              aria-label="在前面插入一步"
              title="在前面插入一步"
              on:pointerdown|stopPropagation={() => undefined}
              on:click|stopPropagation={() => onquickadd(node.path, 'before')}
            >
              <svg viewBox="0 0 12 12" width="11" height="11" aria-hidden="true">
                <path d="M6 1.6 10.4 7H7.6v3.4H4.4V7H1.6Z" fill="currentColor"></path>
              </svg>
            </button>
            <button
              class="quick-step right"
              type="button"
              aria-label="在后面加一步"
              title="在后面加一步"
              on:pointerdown|stopPropagation={() => undefined}
              on:click|stopPropagation={() => onquickadd(node.path, 'after')}
            >
              <svg viewBox="0 0 12 12" width="11" height="11" aria-hidden="true">
                <path d="M6 1.6 10.4 7H7.6v3.4H4.4V7H1.6Z" fill="currentColor"></path>
              </svg>
            </button>
            <button
              class="quick-step bottom"
              type="button"
              aria-label="在后面加一步"
              title="在后面加一步"
              on:pointerdown|stopPropagation={() => undefined}
              on:click|stopPropagation={() => onquickadd(node.path, 'after')}
            >
              <svg viewBox="0 0 12 12" width="11" height="11" aria-hidden="true">
                <path d="M6 1.6 10.4 7H7.6v3.4H4.4V7H1.6Z" fill="currentColor"></path>
              </svg>
            </button>
          </div>
        {/if}
      </div>
    {/each}

    {#if activeEdge}
      <button
        class="port-handle from"
        class:dragging={portDrag?.end === 'from'}
        type="button"
        aria-label="拖动连线起点，改到图形边缘的其他位置"
        title="拖动改起点；双击线恢复自动"
        style="left: {activeEdge.start.x}px; top: {activeEdge.start.y}px;"
        on:pointerdown={(event) => onPortDown(event, activeEdge, 'from')}
      >
        <span></span>
      </button>
      <button
        class="port-handle to"
        class:dragging={portDrag?.end === 'to'}
        type="button"
        aria-label="拖动连线终点，改到图形边缘的其他位置"
        title="拖动改终点；双击线恢复自动"
        style="left: {activeEdge.end.x}px; top: {activeEdge.end.y}px;"
        on:pointerdown={(event) => onPortDown(event, activeEdge, 'to')}
      >
        <span></span>
      </button>
    {/if}

    {#if draggingPaths.length > 0 && dropGap}
      <div
        class="drop-line"
        style="left: {dropGap.x}px; top: {dropGap.y + dropGap.h / 2}px; width: {dropGap.w}px;"
      >
        <span></span>
      </div>
    {/if}
    </div>
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

  <div class="canvas-hint" aria-hidden="true">
    {freeLayout ? '自由布局 · 拖节点移动 · 滚轮缩放 · 左键空白框选 · 右键平移' : '滚轮缩放 · 左键拖空白框选 · 右键拖空白平移'}
  </div>

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

  /* 原点在内层，逻辑坐标可以为负数；外层只负责缩放/平移。 */
  .world-content {
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

  .edge-path {
    fill: none;
    stroke: var(--kp-rule-strong);
    stroke-width: 1.1;
    stroke-linecap: round;
    stroke-linejoin: round;
    opacity: 0.62;
  }

  .edge-path.false,
  .edge-path.back {
    stroke-dasharray: 4 4;
    opacity: 0.5;
  }

  .edge-path.loop {
    stroke: var(--kp-signal);
    opacity: 0.5;
  }

  .edge-label {
    fill: var(--kp-ink3);
    stroke: var(--kp-paper);
    stroke-width: 4px;
    paint-order: stroke fill;
    font: 500 8.5px/1 var(--kp-font-mono);
    letter-spacing: 0.04em;
    pointer-events: none;
  }

  .edge-label.true,
  .edge-label.loop {
    fill: var(--kp-signal);
  }

  /* 线的可点击区域比视觉线宽得多，但不改变图面密度。 */
  .edge-hit {
    fill: none;
    stroke: transparent;
    stroke-width: 14;
    pointer-events: stroke;
    cursor: pointer;
  }

  .edge-group.active .edge-path {
    stroke: var(--kp-signal);
    opacity: 1;
    stroke-width: 1.8;
  }

  .edge-group.active .edge-label {
    fill: var(--kp-signal);
    opacity: 1;
  }

  /* 端点圆点：选中线后出现，拖动时沿目标图形四边投影。 */
  .port-handle {
    position: absolute;
    display: grid;
    place-items: center;
    width: 14px;
    height: 14px;
    padding: 0;
    border: 1.5px solid var(--kp-signal);
    border-radius: 50%;
    background: var(--kp-card);
    box-shadow: 0 1px 0 color-mix(in srgb, var(--kp-ink) 12%, transparent);
    transform: translate(-50%, -50%);
    cursor: grab;
    touch-action: none;
    z-index: 20;
  }

  .port-handle span {
    width: 4px;
    height: 4px;
    border-radius: 50%;
    background: var(--kp-signal);
  }

  .port-handle:hover,
  .port-handle.dragging {
    background: var(--kp-signal);
    cursor: grabbing;
  }

  .port-handle:hover span,
  .port-handle.dragging span {
    background: var(--kp-card);
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

  /* drawio 式四向入口：只服务"选中一个节点"时，方向键负责前插/后插。 */
  .quick-add {
    position: absolute;
    inset: 0;
    z-index: 4;
    pointer-events: none;
  }

  .quick-step {
    position: absolute;
    display: grid;
    place-items: center;
    width: 22px;
    height: 22px;
    padding: 0;
    border: 1px solid var(--kp-cue);
    border-radius: 999px;
    background: var(--kp-card);
    color: var(--kp-cue);
    cursor: pointer;
    pointer-events: auto;
    /* WebView2 会给刚点过的按钮补一圈原生焦点框；它又黑又是方的，看起来像选中了
       一块画布。四向箭头不是表单控件，去掉原生框，只保留 hover/focus-visible 的底色。 */
    outline: none;
    -webkit-tap-highlight-color: transparent;
    transition: background 120ms ease, color 120ms ease;
  }

  .quick-step:focus {
    outline: none;
  }

  .quick-step:hover,
  .quick-step:focus-visible {
    background: var(--kp-cue);
    color: var(--kp-card);
    outline: none;
  }

  .quick-step.top {
    left: 50%;
    top: -11px;
    transform: translateX(-50%);
  }

  .quick-step.bottom {
    left: 50%;
    bottom: -11px;
    transform: translateX(-50%);
  }

  .quick-step.left {
    left: -11px;
    top: 50%;
    transform: translateY(-50%);
  }

  .quick-step.right {
    right: -11px;
    top: 50%;
    transform: translateY(-50%);
  }

  .quick-step.left svg {
    transform: rotate(-90deg);
  }

  .quick-step.right svg {
    transform: rotate(90deg);
  }

  .quick-step.bottom svg {
    transform: rotate(180deg);
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
