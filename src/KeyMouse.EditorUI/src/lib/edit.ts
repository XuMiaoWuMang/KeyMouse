/**
 * 对步骤列表的五种改动：插入、删除、上移、下移、复制。
 *
 * 都是纯函数、都返回新数组：界面把它们的结果塞回文档，单测可以直接调。
 * 不做"带副作用"的原地改——撤销栈就是先把文档序列化一份存起来，原地改会让快照
 * 和数组指向同一份对象。
 */

import type { FlowStep } from './model';

/** 在 `index` 之后插入；`index` 为 -1 时放到最前面。 */
export function insertAfter(steps: readonly FlowStep[], index: number, step: FlowStep): FlowStep[] {
  const next = [...steps];
  next.splice(index + 1, 0, step);
  return next;
}

/** 删掉第 `index` 步。下标越界时原样返回，不抛错——界面上的按钮不该因为竞态炸掉。 */
export function removeAt(steps: readonly FlowStep[], index: number): FlowStep[] {
  if (index < 0 || index >= steps.length) return [...steps];
  return steps.filter((_, i) => i !== index);
}

/**
 * 复制第 `index` 步，副本插在它后面（老外壳的「复制这一步」）。
 *
 * 深拷贝一份：直接放同一个对象引用的话，改副本会连带改原件——撤销栈是序列化快照，
 * 两份"看起来一样"的步骤共用一个对象时，那条历史就说不清了。
 */
export function duplicateAt(steps: readonly FlowStep[], index: number): FlowStep[] {
  if (index < 0 || index >= steps.length) return [...steps];
  const copy = JSON.parse(JSON.stringify(steps[index])) as FlowStep;
  const next = [...steps];
  next.splice(index + 1, 0, copy);
  return next;
}
/** 上移一位；已经在最前面就不动。 */
export function moveUp(steps: readonly FlowStep[], index: number): FlowStep[] {
  if (index <= 0 || index >= steps.length) return [...steps];
  const next = [...steps];
  [next[index - 1], next[index]] = [next[index], next[index - 1]];
  return next;
}

/** 下移一位；已经在最后就不动。 */
export function moveDown(steps: readonly FlowStep[], index: number): FlowStep[] {
  if (index < 0 || index >= steps.length - 1) return [...steps];
  const next = [...steps];
  [next[index + 1], next[index]] = [next[index], next[index + 1]];
  return next;
}

/** 删除之后选中项应该落在哪：优先保持原位置，删的是最后一步就往前收一格。 */
/**
 * 把第 `from` 步挪到第 `to` 个位置（拖拽排序用）。目标位置按"挪完之后的下标"算。
 * 越界或原地不动时原样返回，调用方可以据此判断"没发生什么"。
 */
export function moveTo(steps: readonly FlowStep[], from: number, to: number): FlowStep[] {
  if (from === to) return steps.slice();
  if (from < 0 || from >= steps.length) return steps.slice();
  if (to < 0 || to >= steps.length) return steps.slice();
  const next = steps.slice();
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}

export function selectionAfterRemove(index: number, length: number): number {
  if (length <= 0) return 0;
  return Math.min(Math.max(index, 0), length - 1);
}
