/**
 * 连续的鼠标移动折成一组。
 *
 * 录制会把整段轨迹写成"一步一个 `move`"——一段四十行的 `(x,y)` 能把真正要看的步骤挤出视野。
 * 读流程的人要的是"从哪儿到哪儿"，所以 3 次以上连续 `move` 在**显示层**折成一行：
 * 题头写 `起点 → 终点`，点开才逐条看。阈值与 WinUI 版一致（3 次）。
 *
 * 折的只是显示，不是文档：`steps` 一个字节都不改，`rowKeys`（结果落行用的下标）
 * 也不变；变的只有"第几号"这串标签——编号按**看得见的顺序**走，折起来的一组占一个号，
 * 后面的行跟着前移。这样做的好处是列表里不会出现 1、2、7 这种跳号。
 */

/** 每一步属于哪个折叠组：不是组员时 `head = -1`。 */
export interface MoveRun {
  /** 组头在 `steps` 里的下标（组内每个成员都指向同一个头）。 */
  head: number;
  /** 组内第几个（组头是 0）。 */
  offset: number;
  /** 组里有几步。 */
  count: number;
}

/** 少于这个数就不折：两次移动折起来反而更难读。 */
export const MIN_MOVE_RUN = 3;

export function foldMoves(steps: readonly { type?: string }[]): MoveRun[] {
  const runs: MoveRun[] = steps.map(() => ({ head: -1, offset: 0, count: 0 }));
  let k = 0;
  while (k < steps.length) {
    if (steps[k]?.type !== 'move') {
      k += 1;
      continue;
    }
    let end = k;
    while (end + 1 < steps.length && steps[end + 1]?.type === 'move') end += 1;
    const count = end - k + 1;
    if (count >= MIN_MOVE_RUN) {
      for (let m = k; m <= end; m += 1) runs[m] = { head: k, offset: m - k, count };
    }
    k = end + 1;
  }
  return runs;
}

/** 是不是要画成折叠题头的那一行。 */
export function isFoldHead(runs: readonly MoveRun[], index: number): boolean {
  const run = runs[index];
  return run !== undefined && run.head === index && run.count >= MIN_MOVE_RUN;
}

/** 是不是"被折进去的组员"：它自己那一行不画，跟着题头一起出现。 */
export function isFoldMember(runs: readonly MoveRun[], index: number): boolean {
  const run = runs[index];
  return run !== undefined && run.head >= 0 && run.head !== index;
}

/**
 * 显示编号：折起来的一组占一个号，组员借用组头的号。
 *
 * 注意它只影响**标签**。结果落行、右键菜单里的增删移动仍然按 `steps` 的下标，
 * 也就是命令行报告里 `origin` 的那套写法——两套编号各管各的，混着用就会错行。
 */
export function displayNumbers(runs: readonly MoveRun[]): number[] {
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

/**
 * 题头摘要：起点 → 终点。两个端点都来自真实步骤，不猜。
 * 缺 `at` 的那种半成品移动就写 `?`，让"这一组里有一步还没填坐标"露出来。
 *
 * 参数收 `unknown[]`：模板里传进来的是 `FlowStep`（字段是 `[key: string]: unknown`），
 * 在函数这一头收窄，比让调用方到处写断言干净。
 */
export function moveRunSummary(steps: readonly unknown[], head: number, count: number): string {
  const point = (step: unknown): string => {
    const at = (step as { at?: unknown } | null | undefined)?.at;
    if (typeof at !== 'object' || at === null) return '?';
    const { x, y } = at as { x?: unknown; y?: unknown };
    if (typeof x !== 'number' || typeof y !== 'number') return '?';
    return `${x},${y}`;
  };
  return `${point(steps[head])} → ${point(steps[head + count - 1])}`;
}

/** 折叠题头在 `steps` 里的全部下标（升序），给键盘导航与"全部展开"用。 */
export function foldHeads(runs: readonly MoveRun[]): number[] {
  const heads: number[] = [];
  for (let i = 0; i < runs.length; i += 1) {
    if (isFoldHead(runs, i)) heads.push(i);
  }
  return heads;
}
