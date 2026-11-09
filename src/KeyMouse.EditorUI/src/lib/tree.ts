/**
 * 流程树的点号路径编辑。
 *
 * 旧界面的选中状态是「第几组的第几个」，只够两层；`if` 里再放一个 `while` 就点不到里层。
 * 这里把一步在文档里的位置写成路径：每个元素选一条分支和分支里的下标。
 *
 *   [ {steps,0} ]                       顶层第 1 步
 *   [ {steps,0}, {steps,1} ]            第 1 步（if/循环）的成立时/循环体第 2 步
 *   [ {steps,0}, {else,0} ]             第 1 步的否则分支第 1 步
 *
 * 约定和引擎的 `origin` 同一套：只有 else 分支写成 `.e`，别的分支都写数字。
 * 所有函数都不原地改：返回新的顶层步骤数组，撤销栈存的 JSON 快照才不会被后续修改偷走。
 */

import { branchesOf, type BranchKey, type FlowStep } from './model';

export interface PathEntry {
  branch: BranchKey;
  index: number;
}

export type NodePath = PathEntry[];

export interface LocatedStep {
  path: NodePath;
  step: FlowStep;
  depth: number;
}

export function samePath(a: NodePath | null | undefined, b: NodePath | null | undefined): boolean {
  if (a === b) return true;
  if (!a || !b || a.length !== b.length) return false;
  return a.every((entry, index) => entry.branch === b[index].branch && entry.index === b[index].index);
}

export function pathKey(path: NodePath | null): string {
  return path === null ? '' : path.map((entry) => `${entry.branch}:${entry.index}`).join('/');
}

export function parentPath(path: NodePath): NodePath {
  return path.slice(0, -1);
}

function withBranch(parent: FlowStep, branch: BranchKey, children: FlowStep[]): FlowStep {
  const next = { ...parent } as FlowStep;
  if (children.length === 0) delete next[branch];
  else next[branch] = children;
  return next;
}

/** 某个父节点的一条分支上的数组；父路径为空表示顶层（只认 steps）。 */
export function sequenceOf(
  steps: readonly FlowStep[],
  parent: NodePath,
  branch: BranchKey,
): FlowStep[] {
  if (parent.length === 0) return [...steps];
  const host = stepAt(steps, parent);
  if (!host) return [];
  return [...(branchesOf(host).find((item) => item.key === branch)?.steps ?? [])];
}

export function stepAt(steps: readonly FlowStep[], path: NodePath): FlowStep | undefined {
  if (path.length === 0) return undefined;
  let list: FlowStep[] = [...steps];
  let current: FlowStep | undefined;
  for (let depth = 0; depth < path.length; depth += 1) {
    const entry = path[depth];
    if (depth > 0) {
      if (!current) return undefined;
      // 路径的每一节先选父节点的分支，再选分支里的下标；根节点没有"父分支"。
      const branch = branchesOf(current).find((item) => item.key === entry.branch);
      list = branch?.steps ?? [];
    }
    current = list[entry.index];
    if (!current) return undefined;
  }
  return current;
}

function replaceSequence(
  steps: readonly FlowStep[],
  parent: NodePath,
  branch: BranchKey,
  children: FlowStep[],
): FlowStep[] {
  if (parent.length === 0) return children;
  const host = stepAt(steps, parent);
  if (!host) return [...steps];
  const updated = withBranch(host, branch, children);
  return replaceAt(steps, parent, updated);
}

export function replaceAt(
  steps: readonly FlowStep[],
  path: NodePath,
  next: FlowStep,
): FlowStep[] {
  if (path.length === 0) return [...steps];
  const last = path[path.length - 1];
  if (path.length === 1) {
    const copy = [...steps];
    if (!copy[last.index]) return copy;
    copy[last.index] = next;
    return copy;
  }
  const parent = parentPath(path);
  const host = stepAt(steps, parent);
  if (!host) return [...steps];
  const children = sequenceOf(steps, parent, last.branch);
  if (!children[last.index]) return [...steps];
  children[last.index] = next;
  return replaceSequence(steps, parent, last.branch, children);
}

export function insertAt(
  steps: readonly FlowStep[],
  parent: NodePath,
  branch: BranchKey,
  index: number,
  step: FlowStep,
): FlowStep[] {
  const children = sequenceOf(steps, parent, branch);
  const at = Math.max(0, Math.min(index, children.length));
  children.splice(at, 0, step);
  return replaceSequence(steps, parent, branch, children);
}

/** 往一个块的某条分支末尾加一步；`host` 是那个块的路径。 */
export function appendToBranch(
  steps: readonly FlowStep[],
  host: NodePath,
  branch: BranchKey,
  step: FlowStep,
): FlowStep[] {
  const children = sequenceOf(steps, host, branch);
  children.push(step);
  return replaceSequence(steps, host, branch, children);
}

export function removeAt(steps: readonly FlowStep[], path: NodePath): FlowStep[] {
  if (path.length === 0) return [...steps];
  const last = path[path.length - 1];
  const parent = parentPath(path);
  const children = sequenceOf(steps, parent, last.branch);
  if (!children[last.index]) return [...steps];
  children.splice(last.index, 1);
  return replaceSequence(steps, parent, last.branch, children);
}

export function duplicateAt(steps: readonly FlowStep[], path: NodePath): FlowStep[] {
  if (path.length === 0) return [...steps];
  const last = path[path.length - 1];
  const parent = parentPath(path);
  const children = sequenceOf(steps, parent, last.branch);
  const source = children[last.index];
  if (!source) return [...steps];
  const copy = JSON.parse(JSON.stringify(source)) as FlowStep;
  children.splice(last.index + 1, 0, copy);
  return replaceSequence(steps, parent, last.branch, children);
}

/**
 * 把一步挪到同一父节点、同一分支的另一处。
 *
 * 目标 `index` 是**移动前**数组里的插入位置；同一分支里从前往后挪时，
 * 移走自己会让后面的下标少 1，这里先减掉再插。
 */
export function moveAt(
  steps: readonly FlowStep[],
  from: NodePath,
  parent: NodePath,
  branch: BranchKey,
  index: number,
): FlowStep[] {
  if (from.length === 0) return [...steps];
  const last = from[from.length - 1];
  const sourceParent = parentPath(from);
  if (!samePath(sourceParent, parent) || last.branch !== branch) {
    // 跨分支/跨父节点的移动没有可靠的语义：宁可不动，也不要悄悄丢掉一步。
    return [...steps];
  }

  const sourceChildren = sequenceOf(steps, sourceParent, branch);
  const target = Math.max(0, Math.min(index, sourceChildren.length));
  if (target === last.index || target === last.index + 1) return [...steps];
  const adjusted = target > last.index ? target - 1 : target;

  const without = removeAt(steps, from);
  return insertAt(without, parent, branch, adjusted, stepAt(steps, from)!);
}

export function isPathPrefix(prefix: NodePath, path: NodePath): boolean {
  if (prefix.length > path.length) return false;
  return prefix.every((entry, index) => entry.branch === path[index].branch && entry.index === path[index].index);
}

export interface MoveResult {
  steps: FlowStep[];
  /** 移动后在目标分支里的新路径；没有发生移动时为空。 */
  moved: NodePath[];
}

/** 删除一组节点；父与子同时选中时只删父，避免重复删。 */
export function removeNodes(steps: readonly FlowStep[], paths: readonly NodePath[]): FlowStep[] {
  const order = new Map(allNodes(steps).map((node, at) => [pathKey(node.path), at]));
  const chosen = new Map<string, NodePath>();
  for (const path of paths) {
    if (path.length === 0 || !stepAt(steps, path)) continue;
    chosen.set(pathKey(path), path);
  }
  const selected = [...chosen.values()].sort((a, b) => (order.get(pathKey(a)) ?? 0) - (order.get(pathKey(b)) ?? 0));
  const normalized = selected.filter(
    (path) => !selected.some((other) => !samePath(other, path) && isPathPrefix(other, path)),
  );
  let next = [...steps];
  for (const path of [...normalized].reverse()) next = removeAt(next, path);
  return next;
}

function cloneStep(step: FlowStep): FlowStep {
  return JSON.parse(JSON.stringify(step)) as FlowStep;
}

/**
 * 复制选中的节点（深拷贝，按文档顺序）。
 * 父与子同时选中时只复制父，避免粘贴出两份重叠的子树。
 */
export function copyNodes(steps: readonly FlowStep[], paths: readonly NodePath[]): FlowStep[] {
  const order = new Map(allNodes(steps).map((node, at) => [pathKey(node.path), at]));
  const chosen = new Map<string, NodePath>();
  for (const path of paths) {
    if (path.length === 0 || !stepAt(steps, path)) continue;
    chosen.set(pathKey(path), path);
  }
  const selected = [...chosen.values()].sort((a, b) => (order.get(pathKey(a)) ?? 0) - (order.get(pathKey(b)) ?? 0));
  const normalized = selected.filter(
    (path) => !selected.some((other) => !samePath(other, path) && isPathPrefix(other, path)),
  );
  return normalized.map((path) => cloneStep(stepAt(steps, path)!));
}

/** 在一条分支的指定位置连续插入一组节点。 */
export function insertManyAt(
  steps: readonly FlowStep[],
  parent: NodePath,
  branch: BranchKey,
  index: number,
  copies: readonly FlowStep[],
): FlowStep[] {
  let next = [...steps];
  copies.forEach((step, offset) => {
    next = insertAt(next, parent, branch, index + offset, cloneStep(step));
  });
  return next;
}


/**
 * 把一组节点搬到任意父节点/分支的指定位置。
 *
 * - 多个节点按文档顺序保留相对次序；
 * - 选中祖先时，它子树里的后代不再重复搬；
 * - 落点在某个被搬节点的子树里时拒绝，避免把节点塞进自己；
 * - 目标下标是**搬之前**的数组下标；同序列里被搬走的节点会让下标先减掉。
 */
export function moveNodes(
  steps: readonly FlowStep[],
  sources: readonly NodePath[],
  parent: NodePath,
  branch: BranchKey,
  index: number,
): MoveResult {
  const order = new Map(allNodes(steps).map((node, at) => [pathKey(node.path), at]));
  const chosen = new Map<string, NodePath>();
  for (const path of sources) {
    if (path.length === 0 || !stepAt(steps, path)) continue;
    chosen.set(pathKey(path), path);
  }
  const selected = [...chosen.values()].sort((a, b) => (order.get(pathKey(a)) ?? 0) - (order.get(pathKey(b)) ?? 0));
  // 父与子同时选中时只搬父：否则子会先被搬走，父的那份已经不是原来的内容。
  const normalized = selected.filter(
    (path) => !selected.some((other) => !samePath(other, path) && isPathPrefix(other, path)),
  );
  if (normalized.length === 0) return { steps: [...steps], moved: [] };

  // 落点不能在被搬节点的子树里（包括节点自己）。
  if (normalized.some((path) => isPathPrefix(path, parent))) {
    return { steps: [...steps], moved: [] };
  }
  if (parent.length > 0 && !stepAt(steps, parent)) {
    return { steps: [...steps], moved: [] };
  }

  const countBefore = normalized.reduce((count, path) => {
    const last = path[path.length - 1];
    if (samePath(parentPath(path), parent) && last.branch === branch && last.index < index) return count + 1;
    return count;
  }, 0);

  // 从后往前删：先删后面的下标，前面节点的路径不会失效。
  let next = [...steps];
  for (const path of [...normalized].reverse()) {
    next = removeAt(next, path);
  }

  // 删除会克隆祖先对象，不能用对象身份找目标父节点；按"前面被删掉的兄弟"逐层减下标。
  const adjustedParent = parent.map((entry, depth) => {
    const prefix = parent.slice(0, depth);
    const removedBefore = normalized.filter((source) => {
      const last = source[source.length - 1];
      return samePath(parentPath(source), prefix) && last.branch === entry.branch && last.index < entry.index;
    }).length;
    return { branch: entry.branch, index: entry.index - removedBefore };
  });
  if (adjustedParent.length > 0 && !stepAt(next, adjustedParent)) {
    return { steps: [...steps], moved: [] };
  }

  const targetLength = sequenceOf(next, adjustedParent, branch).length;
  const at = Math.max(0, Math.min(index - countBefore, targetLength));
  const moved: NodePath[] = [];
  normalized.forEach((source, offset) => {
    next = insertAt(next, adjustedParent, branch, at + offset, stepAt(steps, source)!);
    moved.push([...adjustedParent, { branch, index: at + offset }]);
  });

  return { steps: next, moved };
}


/** 一步在引擎报告里的位置：`1`、`2.1`、`2.e1`、`2.e1.3`。 */
export function originOf(steps: readonly FlowStep[], path: NodePath): string {
  if (path.length === 0) return '';
  const step = stepAt(steps, path);
  if (!step) return '';
  let origin = String(path[0].index + 1);
  for (let index = 1; index < path.length; index += 1) {
    const entry = path[index];
    origin += entry.branch === 'else' ? `.e${entry.index + 1}` : `.${entry.index + 1}`;
  }
  return origin;
}

/** 深度优先的全部步骤：父在子前，成立时在否则前。用来找第一条没填完的步骤。 */
export function allNodes(steps: readonly FlowStep[]): LocatedStep[] {
  const out: LocatedStep[] = [];
  const walk = (items: readonly FlowStep[], prefix: NodePath, depth: number, branch: BranchKey) => {
    items.forEach((step, index) => {
      const path = [...prefix, { branch, index }];
      out.push({ path, step, depth });
      for (const child of branchesOf(step)) {
        walk(child.steps, path, depth + 1, child.key);
      }
    });
  };
  walk(steps, [], 0, 'steps');
  return out;
}

/** 一个节点直属的子步骤（两条分支都算），给画布计数/提示用。 */
export function childrenOfPath(steps: readonly FlowStep[], path: NodePath): LocatedStep[] {
  const host = stepAt(steps, path);
  if (!host) return [];
  return branchesOf(host).flatMap((branch) =>
    branch.steps.map((step, index) => ({
      path: [...path, { branch: branch.key, index }],
      step,
      depth: path.length,
    })),
  );
}
