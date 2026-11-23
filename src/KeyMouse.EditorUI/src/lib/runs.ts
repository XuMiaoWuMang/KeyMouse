/**
 * 画布上的"连续操作"：把一段相邻的鼠标移动 / 滚轮并成一个节点。
 *
 * 录制把轨迹写成一步一个 `move`，一段鼠标从左上到右下的过程能铺出几十个卡片。
 * 读流程的人先关心的是"从哪儿到哪儿、滚了多远"，所以 **3 步以上**相邻的
 * `move` / `wheel`（两种可以混着）在显示层并成一个节点，点开才逐步看。
 *
 * 这里只造"视图树"：`steps` 一个字节都不改，选中、编辑、撤销仍然作用在真实路径上。
 * 画布在视图树上算几何与连线，再用 `remapLayout` 把节点、空槽、落点缝换回真实路径。
 * 这样外面（App）完全不用知道"哪些步被藏起来了"。
 *
 * 为什么不在 `fold.ts` 上改：那一套是给旧清单视图/结构视图按"同种类型"折行用的，
 * 阈值与接口都绑在行号上；画布需要混合类型、嵌套分支、视图路径映射，规则不一样。
 */

import type { CanvasLayout } from './canvas';
import { branchesOf, type BranchKey, type FlowStep } from './model';
import type { StepResult } from './run';
import { originOf, pathKey, samePath, sequenceOf, type NodePath } from './tree';

/** 少于这个数就不并：两次移动并起来反而更难读。 */
export const MIN_RUN = 3;

/** 会被并成"连续操作"的步骤类型。 */
export type RunKind = 'move' | 'wheel';

/** 一组连续鼠标操作。路径都是**真实**路径，和 `steps` 对齐。 */
export interface RunInfo {
  /** 组头的真实路径，视图里就是这个节点的路径。 */
  head: NodePath;
  /** 组内每一步的真实路径，按文档顺序。 */
  paths: NodePath[];
  /** 与 `paths` 一一对应的步骤对象。 */
  steps: FlowStep[];
  /** 组里各类型的个数。 */
  moves: number;
  wheels: number;
  /** 组头 / 组尾在命令行报告里的位置（`7`、`2.e1.3`）。 */
  fromOrigin: string;
  toOrigin: string;
}

/** 视图路径 → 真实步骤。 */
export interface ViewItem {
  path: NodePath;
  origin: string;
  /** 组头才有：这一组被并起来了。展开时组头仍然带着它，用来画「收起」。 */
  run?: RunInfo;
}

export interface ViewModel {
  /** 交给 `layoutFlow` / `buildGraph` 的视图树；块节点是浅拷贝，run 用组头占位。 */
  steps: FlowStep[];
  /** 视图路径键 → 真实步骤。 */
  items: Map<string, ViewItem>;
  /** `parentKey|branch|index`（视觉下标）→ 真实下标；给拖动落点用。 */
  slots: Map<string, number>;
  /** 真实组头路径键 → 这一组。 */
  runs: Map<string, RunInfo>;
}

export function isRunStep(step: FlowStep | undefined): step is FlowStep & { type: RunKind } {
  return step?.type === 'move' || step?.type === 'wheel';
}

function slotKey(parent: NodePath, branch: BranchKey, index: number): string {
  return `${pathKey(parent)}|${branch}|${index}`;
}

function pointLabel(step: FlowStep): string {
  const at = step.at;
  if (!at || typeof at.x !== 'number' || typeof at.y !== 'number') return '?';
  return `${at.x},${at.y}`;
}

/** 从 `head` 起的最长连续鼠标段长度。 */
function runLength(items: readonly FlowStep[], head: number): number {
  let end = head;
  while (end + 1 < items.length && isRunStep(items[end + 1])) end += 1;
  return end - head + 1;
}

function makeRun(
  steps: readonly FlowStep[],
  items: readonly FlowStep[],
  head: number,
  count: number,
  parent: NodePath,
  branch: BranchKey,
): RunInfo {
  const paths: NodePath[] = [];
  const members: FlowStep[] = [];
  let moves = 0;
  let wheels = 0;
  for (let offset = 0; offset < count; offset += 1) {
    const step = items[head + offset];
    paths.push([...parent, { branch, index: head + offset }]);
    members.push(step);
    if (step.type === 'wheel') wheels += 1;
    else moves += 1;
  }
  return {
    head: paths[0],
    paths,
    steps: members,
    moves,
    wheels,
    fromOrigin: originOf(steps, paths[0]),
    toOrigin: originOf(steps, paths[paths.length - 1]),
  };
}

/**
 * 真实步骤树 → 视图树。
 *
 * `open` 里放的是组头真实路径的 `pathKey`：展开的组不合并，但组头仍然带 `run`，
 * 画布据此在组头卡片上画「收起」。
 */
export function buildView(steps: readonly FlowStep[], open: Iterable<string> = []): ViewModel {
  const ctx = {
    real: steps,
    open: new Set(open),
    items: new Map<string, ViewItem>(),
    slots: new Map<string, number>(),
    runs: new Map<string, RunInfo>(),
  };

  const walk = (
    items: readonly FlowStep[],
    parentView: NodePath,
    parentReal: NodePath,
    branch: BranchKey,
  ): FlowStep[] => {
    const out: FlowStep[] = [];
    let index = 0;

    while (index < items.length) {
      const step = items[index];
      const realPath = [...parentReal, { branch, index }];
      const realKey = pathKey(realPath);
      const isHead = isRunStep(step) && (index === 0 || !isRunStep(items[index - 1]));
      const count = isHead ? runLength(items, index) : 0;
      const run = isHead && count >= MIN_RUN
        ? makeRun(steps, items, index, count, parentReal, branch)
        : null;

      if (run) {
        ctx.runs.set(realKey, run);
        if (!ctx.open.has(realKey)) {
          // 收起：一个节点占掉整段，后面的视觉下标跟着前移；真实下标记在 slots 里。
          const viewIndex = out.length;
          const viewPath: NodePath = [...parentView, { branch, index: viewIndex }];
          ctx.items.set(pathKey(viewPath), { path: realPath, origin: run.fromOrigin, run });
          ctx.slots.set(slotKey(parentView, branch, viewIndex), index);
          out.push(step);
          index += count;
          continue;
        }
      }

      const viewIndex = out.length;
      const viewPath: NodePath = [...parentView, { branch, index: viewIndex }];
      const branches = branchesOf(step);
      let viewStep = step;
      if (branches.length > 0) {
        // 块的子序列也要做同样的合并；浅拷贝只换分支数组。
        viewStep = { ...step };
        for (const child of branches) {
          const childView = walk(child.steps, viewPath, realPath, child.key);
          if (child.key === 'steps') viewStep.steps = childView;
          else viewStep.else = childView;
        }
      }

      ctx.items.set(pathKey(viewPath), {
        path: realPath,
        origin: originOf(steps, realPath),
        run: run ?? undefined,
      });
      ctx.slots.set(slotKey(parentView, branch, viewIndex), index);
      out.push(viewStep);
      index += 1;
    }

    // 末尾落点（插到这一步之后）也要能换回真实下标。
    ctx.slots.set(slotKey(parentView, branch, out.length), items.length);
    return out;
  };

  const viewSteps = walk(steps, [], [], 'steps');
  return { steps: viewSteps, items: ctx.items, slots: ctx.slots, runs: ctx.runs };
}

/** 视图路径 → 真实路径；视图里没有的（例如顶层 `[]`）原样返回。 */
export function realPathOf(view: ViewModel, path: NodePath): NodePath {
  if (path.length === 0) return [];
  return view.items.get(pathKey(path))?.path ?? path;
}

/**
 * 把画布按视图树算出来的几何换回真实路径。
 *
 * 节点、分支标题、空分支的 `parentPath` 可能落在"块前有合并"的序列上，
 * 视觉下标与真实下标不同；落点缝（gaps）的下标必须换回真实下标，否则拖到
 * 合并卡片后面会插进组里面。
 */
export function remapLayout(layout: CanvasLayout, view: ViewModel): CanvasLayout {
  const nodes = layout.nodes.map((node) => {
    const item = view.items.get(pathKey(node.path));
    if (!item) return node;
    const last = item.path[item.path.length - 1];
    return {
      ...node,
      path: item.path,
      parentPath: item.path.slice(0, -1),
      branch: last.branch,
      index: last.index,
      origin: item.origin,
    };
  });

  const labels = layout.labels.map((label) => ({
    ...label,
    parentPath: realPathOf(view, label.parentPath),
  }));

  const empties = layout.empties.map((empty) => ({
    ...empty,
    parentPath: realPathOf(view, empty.parentPath),
  }));

  const gaps = layout.gaps.map((gap) => ({
    ...gap,
    parentPath: realPathOf(view, gap.parentPath),
    index: view.slots.get(slotKey(gap.parentPath, gap.branch, gap.index)) ?? gap.index,
  }));

  return { ...layout, nodes, labels, empties, gaps };
}

/** 一张合并卡片的一行摘要：先说"几步、什么动作"，再说"从哪儿到哪儿 / 共滚了多少"。 */
export function runSummary(run: RunInfo): string {
  if (run.wheels === 0) {
    return `${run.steps.length} 次移动 · ${pointLabel(run.steps[0])} → ${pointLabel(run.steps[run.steps.length - 1])}`;
  }
  if (run.moves === 0) {
    let total = 0;
    for (const step of run.steps) {
      if (typeof step.delta !== 'number') return `${pointLabel(run.steps[0])} · 共 ?`;
      total += step.delta;
    }
    const sign = total > 0 ? '+' : '';
    return `滚轮 ${run.steps.length} 下 · 共 ${sign}${total}`;
  }
  return `移动 ${run.moves} · 滚轮 ${run.wheels} · 共 ${run.steps.length} 步`;
}

/** 角标上的"第几步到第几步"；只合并了一步时退回单号。 */
export function runOriginLabel(run: RunInfo): string {
  return run.fromOrigin === run.toOrigin ? run.fromOrigin : `${run.fromOrigin}–${run.toOrigin}`;
}

/**
 * 如果 `path` 正是一段连续操作的头（且长度够），返回这一组；否则 `null`。
 *
 * App 用它把"点中合并卡片"翻译成"选中整组"：删除、复制、拖动因此天然作用于整组，
 * 而不用给画布加一套"组"的编辑原语。
 */
export function runAtHead(steps: readonly FlowStep[], path: NodePath): RunInfo | null {
  if (path.length === 0) return null;
  const last = path[path.length - 1];
  const parent = path.slice(0, -1);
  const items = sequenceOf(steps, parent, last.branch);
  if (!isRunStep(items[last.index])) return null;
  if (last.index > 0 && isRunStep(items[last.index - 1])) return null;
  const count = runLength(items, last.index);
  if (count < MIN_RUN) return null;
  return makeRun(steps, items, last.index, count, parent, last.branch);
}

/** 当前选中的这些路径是否恰好就是一组连续操作（抽屉里据此换一句人话）。 */
export function runSelection(steps: readonly FlowStep[], paths: readonly NodePath[]): RunInfo | null {
  if (paths.length < MIN_RUN) return null;
  for (const path of paths) {
    const run = runAtHead(steps, path);
    if (!run || run.paths.length !== paths.length) continue;
    if (run.paths.every((member) => paths.some((candidate) => samePath(candidate, member)))) {
      return run;
    }
  }
  return null;
}

/**
 * 把组内每一步的结果卷成一句：有失败就报第一个失败，全成功就说"N 步都成功"。
 * 和 `run.ts` 里"父行用孩子结果汇总"的规则一致——合并显示不该把失败吞掉。
 */
export function rollupRunResult(
  run: RunInfo,
  steps: readonly FlowStep[],
  results: Record<string, StepResult>,
): StepResult | null {
  const parts = run.paths
    .map((path) => results[originOf(steps, path)])
    .filter((result): result is StepResult => result !== undefined);
  if (parts.length === 0) return null;
  const failed = parts.find((result) => !result.ok);
  if (failed) return failed;
  return { ok: true, text: `${parts.length} 步都成功`, dry: parts.every((result) => result.dry) };
}
