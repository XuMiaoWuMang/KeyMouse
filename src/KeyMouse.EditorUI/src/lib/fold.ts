/**
 * 连续的鼠标移动 / 滚轮折成一组。
 *
 * 录制会把整段轨迹写成"一步一个 `move`"——一段四十行的 `(x,y)` 能把真正要看的步骤挤出视野。
 * 读流程的人要的是"从哪儿到哪儿"，所以 3 次以上连续 `move` 在**显示层**折成一行：
 * 题头写 `起点 → 终点`，点开才逐条看。阈值与 WinUI 版一致（3 次）。
 *
 * 滚轮同理：连着滚十下写成十步 `wheel`，谁都不想一行行读。滚轮组头写
 * `位置 · 共 +840`（位移合计，正数往下、负数往上），点开才看到每一格是多少。
 *
 * 折的只是显示，不是文档：`steps` 一个字节都不改，`rowKeys`（结果落行用的下标）
 * 也不变；变的只有"第几号"这串标签——编号按**看得见的顺序**走，折起来的一组占一个号，
 * 后面的行跟着前移。这样做的好处是列表里不会出现 1、2、7 这种跳号。
 */

/** 能折成一组、并且组头怎么念的步骤类型。 */
export type RunKind = 'move' | 'wheel';

/** 每一步属于哪个折叠组：不是组员时 `head = -1`。 */
export interface StepRun {
  /** 哪一类组（`move` 或 `wheel`）。 */
  kind: RunKind;
  /** 组头在 `steps` 里的下标（组内每个成员都指向同一个头）。 */
  head: number;
  /** 组内第几个（组头是 0）。 */
  offset: number;
  /** 组里有几步。 */
  count: number;
}

/** 少于这个数就不折：两次移动折起来反而更难读。 */
export const MIN_MOVE_RUN = 3;

/** 滚轮的阈值同样取 3：滚一下是"翻一屏"，连滚三下才值得折。 */
export const MIN_WHEEL_RUN = 3;

const KIND_MINIMUM: Record<RunKind, number> = {
  move: MIN_MOVE_RUN,
  wheel: MIN_WHEEL_RUN,
};

function kindOf(step: { type?: string } | undefined): RunKind | null {
  const type = step?.type;
  return type === 'move' || type === 'wheel' ? type : null;
}

/** 把相邻的同种步骤归成组；不够阈值的组保持原样（每一步自己一行）。 */
export function foldRuns(steps: readonly { type?: string }[]): StepRun[] {
  const blank = (): StepRun => ({ kind: 'move', head: -1, offset: 0, count: 0 });
  const runs: StepRun[] = steps.map(blank);
  let k = 0;
  while (k < steps.length) {
    const kind = kindOf(steps[k]);
    if (kind === null) {
      k += 1;
      continue;
    }
    let end = k;
    while (end + 1 < steps.length && steps[end + 1]?.type === kind) end += 1;
    const count = end - k + 1;
    if (count >= KIND_MINIMUM[kind]) {
      for (let m = k; m <= end; m += 1) runs[m] = { kind, head: k, offset: m - k, count };
    }
    k = end + 1;
  }
  return runs;
}

/** 只折移动的那套老接口：`foldRuns` 是它的推广，保留名字是为了让调用点读得明白。 */
export function foldMoves(steps: readonly { type?: string }[]): StepRun[] {
  return foldRuns(steps);
}

/** 是不是要画成折叠题头的那一行。 */
export function isFoldHead(runs: readonly StepRun[], index: number): boolean {
  const run = runs[index];
  return run !== undefined && run.head === index && run.count >= KIND_MINIMUM[run.kind];
}

/** 是不是"被折进去的组员"：它自己那一行不画，跟着题头一起出现。 */
export function isFoldMember(runs: readonly StepRun[], index: number): boolean {
  const run = runs[index];
  return run !== undefined && run.head >= 0 && run.head !== index;
}

/** 这一组的类型（不是组头时返回 null），题头据此决定图标与标题。 */
export function runKind(runs: readonly StepRun[], index: number): RunKind | null {
  const run = runs[index];
  if (run === undefined || run.head !== index || run.count < KIND_MINIMUM[run.kind]) return null;
  return run.kind;
}

/**
 * 显示编号：折起来的一组占一个号，组员借用组头的号。
 *
 * 注意它只影响**标签**。结果落行、右键菜单里的增删移动仍然按 `steps` 的下标，
 * 也就是命令行报告里 `origin` 的那套写法——两套编号各管各的，混着用就会错行。
 */
export function displayNumbers(runs: readonly StepRun[]): number[] {
  const numbers = runs.map(() => 0);
  let display = 0;
  for (let i = 0; i < runs.length; i += 1) {
    const run = runs[i];
    if (run.head >= 0 && run.head !== i) {
      numbers[i] = numbers[run.head];
      continue;
    }
    display += 1;
    numbers[i] = display;
  }
  return numbers;
}

function pointOf(step: unknown): string {
  const at = (step as { at?: unknown } | null | undefined)?.at;
  if (typeof at !== 'object' || at === null) return '?';
  const { x, y } = at as { x?: unknown; y?: unknown };
  if (typeof x !== 'number' || typeof y !== 'number') return '?';
  return `${x},${y}`;
}

/**
 * 题头摘要：起点 → 终点。两个端点都来自真实步骤，不猜。
 * 缺 `at` 的那种半成品移动就写 `?`，让"这一组里有一步还没填坐标"露出来。
 *
 * 参数收 `unknown[]`：模板里传进来的是 `FlowStep`（字段是 `[key: string]: unknown`），
 * 在函数这一头收窄，比让调用方到处写断言干净。
 */
export function moveRunSummary(steps: readonly unknown[], head: number, count: number): string {
  return `${pointOf(steps[head])} → ${pointOf(steps[head + count - 1])}`;
}

/**
 * 滚轮组头摘要：位置 · 共 ±N。
 *
 * 合计而不是"第一下是多少"：连着滚 +240、+120、+600 三步，读者关心的是总共滚了多远。
 * 正数往下、负数往上，跟命令行参数同号；缺 `delta` 就写 `?`，别把半成品藏起来。
 */
export function wheelRunSummary(steps: readonly unknown[], head: number, count: number): string {
  let total = 0;
  for (let i = head; i < head + count; i += 1) {
    const delta = (steps[i] as { delta?: unknown } | null | undefined)?.delta;
    if (typeof delta !== 'number') return `${pointOf(steps[head])} · ?`;
    total += delta;
  }
  const sign = total > 0 ? '+' : '';
  return `${pointOf(steps[head])} · 共 ${sign}${total}`;
}

/** 组头摘要，按类型分派。 */
export function runSummary(steps: readonly unknown[], head: number, count: number): string {
  const kind = (steps[head] as { type?: unknown } | null | undefined)?.type;
  return kind === 'wheel'
    ? wheelRunSummary(steps, head, count)
    : moveRunSummary(steps, head, count);
}

/** 折叠题头在 `steps` 里的全部下标（升序），给键盘导航与"全部展开"用。 */
export function foldHeads(runs: readonly StepRun[]): number[] {
  const heads: number[] = [];
  for (let i = 0; i < runs.length; i += 1) {
    if (isFoldHead(runs, i)) heads.push(i);
  }
  return heads;
}
