import { describe, expect, it } from 'vitest';

import { removeChildAt, moveChild, appendChild, duplicateAt, insertAfter, moveDown, moveTo, moveUp, removeAt, selectionAfterRemove } from './edit';
import type { FlowStep } from './model';
import { missingHint, missingOf } from './steps';

const a: FlowStep = { type: 'focus' };
const b: FlowStep = { type: 'sleep', ms: 10 };
const c: FlowStep = { type: 'key', combo: 'Ctrl+S' };

describe('appendChild', () => {
  const group: FlowStep = { type: 'repeat', times: 2, steps: [{ type: 'sleep', ms: 100 }] };
  const plain: FlowStep = { type: 'sleep', ms: 200 };

  it('往分组末尾加一个子步骤，别的行不动', () => {
    const next = appendChild([group, plain], 0, { type: 'wheel', delta: -120 });
    expect(next).toHaveLength(2);
    expect(next[0].steps).toEqual([
      { type: 'sleep', ms: 100 },
      { type: 'wheel', delta: -120 },
    ]);
    expect(next[1]).toBe(plain);
  });

  it('空组也能被填上，原本没有 steps 时就建一个', () => {
    const empty: FlowStep = { type: 'foreach', in: 'items' };
    const next = appendChild([empty], 0, { type: 'sleep', ms: 50 });
    expect(next[0].steps).toEqual([{ type: 'sleep', ms: 50 }]);
  });

  it('不原地改：原对象与原子数组都不受影响', () => {
    const before = [group, plain];
    appendChild(before, 0, { type: 'sleep', ms: 1 });
    expect(group.steps).toHaveLength(1);
    expect(before[0]).toBe(group);
  });

  it('下标越界时原样返回', () => {
    expect(appendChild([plain], 5, { type: 'sleep', ms: 1 })).toEqual([plain]);
    expect(appendChild([plain], -1, { type: 'sleep', ms: 1 })).toEqual([plain]);
  });
});

describe('insertAfter', () => {
  it('插在指定项之后', () => {
    expect(insertAfter([a, b], 0, c)).toEqual([a, c, b]);
  });

  it('插到最后一项之后就是追加', () => {
    expect(insertAfter([a, b], 1, c)).toEqual([a, b, c]);
  });

  it('空列表也能插', () => {
    expect(insertAfter([], -1, c)).toEqual([c]);
  });

  it('不改原数组', () => {
    const source = [a, b];
    insertAfter(source, 0, c);
    expect(source).toEqual([a, b]);
  });
});

describe('removeAt', () => {
  it('删掉指定的那一项', () => {
    expect(removeAt([a, b, c], 1)).toEqual([a, c]);
  });

  it('越界的下标原样返回，不抛错', () => {
    expect(removeAt([a, b], 5)).toEqual([a, b]);
    expect(removeAt([a, b], -1)).toEqual([a, b]);
  });
});

describe('moveUp / moveDown', () => {
  it('上移交换相邻两项', () => {
    expect(moveUp([a, b, c], 2)).toEqual([a, c, b]);
  });

  it('第一项上移不动', () => {
    expect(moveUp([a, b], 0)).toEqual([a, b]);
  });

  it('下移交换相邻两项', () => {
    expect(moveDown([a, b, c], 0)).toEqual([b, a, c]);
  });

  it('最后一项下移不动', () => {
    expect(moveDown([a, b], 1)).toEqual([a, b]);
  });
});

describe('selectionAfterRemove', () => {
  it('停在原位置', () => {
    expect(selectionAfterRemove(1, 5)).toBe(1);
  });

  it('删的是最后一步就往前收一格', () => {
    expect(selectionAfterRemove(4, 4)).toBe(3);
  });

  it('删空了回到 0', () => {
    expect(selectionAfterRemove(0, 0)).toBe(0);
  });
});

describe('missingOf / missingHint', () => {
  it('focus 没给目标窗口就是缺一项', () => {
    expect(missingOf({ type: 'focus' })).toEqual(['target']);
    expect(missingHint({ type: 'focus' })).toBe('还缺 目标窗口');
  });

  it('目标窗口三个字段全空也算没填', () => {
    expect(missingOf({ type: 'focus', target: { process: '  ' } })).toEqual(['target']);
  });

  it('给了进程名就算填了', () => {
    expect(missingOf({ type: 'focus', target: { process: 'notepad' } })).toEqual([]);
    expect(missingHint({ type: 'focus', target: { process: 'notepad' } })).toBe('齐全');
  });

  it('type 的模板带 intervalMs，但缺文字仍然算缺', () => {
    expect(missingOf({ type: 'type', intervalMs: 30 })).toEqual(['text']);
  });

  it('sleep 没有必填项', () => {
    expect(missingOf({ type: 'sleep' })).toEqual([]);
  });

  it('repeat 的空子步骤算缺', () => {
    expect(missingOf({ type: 'repeat', times: 3, steps: [] })).toEqual(['steps']);
  });
});

describe('duplicateAt', () => {
  const steps: FlowStep[] = [{ type: 'sleep', ms: 100 }, { type: 'sleep', ms: 200 }];

  it('副本插在原件后面', () => {
    const next = duplicateAt(steps, 0);
    expect(next.map((s) => s.ms)).toEqual([100, 100, 200]);
  });

  it('副本是深拷贝，改它不影响原件', () => {
    // duplicateAt(steps, 1) 把第 2 步的副本插在它后面，所以副本在下标 2。
    const next = duplicateAt(steps, 1) as { type: string; ms: number }[];
    next[2].ms = 999;
    expect(steps[1].ms).toBe(200);
    expect(next[2].ms).toBe(999);
  });

  it('越界原样返回', () => {
    expect(duplicateAt(steps, 5)).toEqual(steps);
    expect(duplicateAt(steps, -1)).toEqual(steps);
  });
});

it('drag: moveTo 把一步挪到目标位置', () => {
  const steps: FlowStep[] = [{ type: 'sleep', ms: 1 }, { type: 'sleep', ms: 2 }, { type: 'sleep', ms: 3 }];
  const next = moveTo(steps, 0, 2);
  expect(next.map((step) => step.ms)).toEqual([2, 3, 1]);
});

it('drag: moveTo 原地、越界都原样返回', () => {
  const steps: FlowStep[] = [{ type: 'sleep', ms: 1 }, { type: 'sleep', ms: 2 }];
  expect(moveTo(steps, 1, 1)).toEqual(steps);
  expect(moveTo(steps, -1, 0)).toEqual(steps);
  expect(moveTo(steps, 0, 9)).toEqual(steps);
});

it('drag: moveTo 不改原数组', () => {
  const steps: FlowStep[] = [{ type: 'sleep', ms: 1 }, { type: 'sleep', ms: 2 }];
  moveTo(steps, 1, 0);
  expect(steps.map((step) => step.ms)).toEqual([1, 2]);
});

describe('child steps', () => {
  const group = (kids: FlowStep[]): FlowStep[] => [
    { type: 'sleep', ms: 100 },
    { type: 'repeat', times: 2, steps: kids },
    { type: 'sleep', ms: 200 },
  ];

  it('removes the child and leaves the group in place', () => {
    const next = removeChildAt(group([{ type: 'sleep', ms: 1 }, { type: 'sleep', ms: 2 }]), 1, 0);
    expect(next.length).toBe(3);
    expect(next[1].steps?.map((s) => s.ms)).toEqual([2]);
  });

  it('drops the steps field when the last child goes', () => {
    const next = removeChildAt(group([{ type: 'sleep', ms: 1 }]), 1, 0);
    expect(next[1].steps).toBeUndefined();
    expect(next[1].type).toBe('repeat');
  });

  it('ignores out-of-range children and non-groups', () => {
    const steps = group([{ type: 'sleep', ms: 1 }]);
    expect(removeChildAt(steps, 1, 5)).toEqual(steps);
    expect(removeChildAt(steps, 0, 0)).toEqual(steps);
    expect(removeChildAt(steps, 9, 0)).toEqual(steps);
  });

  it('swaps two children without touching the parent list', () => {
    const steps = group([{ type: 'sleep', ms: 1 }, { type: 'sleep', ms: 2 }]);
    const next = moveChild(steps, 1, 0, 1);
    expect(next[1].steps?.map((s) => s.ms)).toEqual([2, 1]);
    expect(next[0]).toBe(steps[0]);
    expect(steps[1].steps?.map((s) => s.ms)).toEqual([1, 2]);
  });

  it('keeps children put at the edges', () => {
    const steps = group([{ type: 'sleep', ms: 1 }, { type: 'sleep', ms: 2 }]);
    expect(moveChild(steps, 1, 0, -1)).toEqual(steps);
    expect(moveChild(steps, 1, 1, 2)).toEqual(steps);
    expect(moveChild(steps, 0, 0, 1)).toEqual(steps);
  });
});