import { describe, expect, it } from 'vitest';

import { branchesOf, childSteps, stepSummary } from './model';
import { makeStep, missingHint, missingOf } from './steps';
import type { FlowStep } from './model';

describe('control-flow step templates', () => {
  it('新的 if 自带一个身体，只缺条件', () => {
    const step = makeStep('if');
    expect(step.steps).toHaveLength(1);
    expect(missingOf(step)).toEqual(['cond']);
    expect(missingHint(step)).toBe('还缺 条件');
  });

  it('while / do 也自带身体和默认上界', () => {
    expect(makeStep('while')).toMatchObject({ cond: '', maxRounds: 200 });
    expect(makeStep('do')).toMatchObject({ cond: '', maxRounds: 200 });
  });

  it('for 的三段式一开始就有可改的初值、条件与步进', () => {
    expect(makeStep('for')).toMatchObject({ init: { i: '0' }, cond: 'i < 3', step: { i: 'i + 1' } });
  });

  it('set / assert / break / continue / return 有各自的必填项', () => {
    expect(missingOf(makeStep('set'))).toEqual([]);
    expect(missingOf(makeStep('assert'))).toEqual([]);
    expect(missingOf(makeStep('break'))).toEqual([]);
    expect(missingOf(makeStep('return'))).toEqual([]);
    expect(missingHint({ type: 'assert', cond: '' })).toBe('还缺 条件');
  });

  it('拖动只缺起点和终点，不要求目标窗口', () => {
    const step = makeStep('drag');
    expect(missingOf(step)).toEqual(['from', 'to']);
    expect(missingHint(step)).toBe('还缺 起点、终点');
    expect(missingOf({ ...step, from: { space: 'screen', x: 1, y: 2 }, to: { space: 'screen', x: 3, y: 4 } })).toEqual([]);
  });

  it('拖动任一端显式为客户区时才提示目标窗口，目标必须有有效选择器', () => {
    const from = { space: 'screen', x: 1, y: 2 } as const;
    const to = { space: 'screen', x: 3, y: 4 } as const;
    for (const points of [
      { from: { ...from, space: 'client' as const }, to },
      { from, to: { ...to, space: 'client' as const } },
      { from: { ...from, space: 'client' as const }, to: { ...to, space: 'client' as const } },
    ]) {
      const step: FlowStep = { type: 'drag', ...points };
      expect(missingOf(step)).toEqual(['target']);
      expect(missingHint(step)).toBe('还缺 目标窗口');
      expect(missingOf({ ...step, target: { process: '  ', title: '' } })).toEqual(['target']);
      expect(missingOf({ ...step, target: { process: 'notepad' } })).toEqual([]);
      expect(missingOf({ ...step, target: { class: 'Notepad' } })).toEqual([]);
    }
    expect(missingOf({ type: 'drag', from, to })).toEqual([]);
  });

  it('目标窗口只认进程名/类名：只有标题不算有效选择器', () => {
    expect(missingOf({ type: 'focus', target: { title: '无标题 - 记事本' } })).toEqual(['target']);
    expect(missingOf({ type: 'focus', target: { process: 'notepad', title: '无关' } })).toEqual([]);
  });

  it('坐标省略空间时按客户区算，需要目标窗口', () => {
    expect(missingOf({ type: 'click', at: { x: 10, y: 20 } })).toEqual(['target']);
    expect(missingOf({ type: 'click', at: { x: 10, y: 20 }, target: { process: 'notepad' } })).toEqual([]);
    expect(missingOf({ type: 'click', at: { space: 'screen', x: 10, y: 20 } })).toEqual([]);
  });

  it('读屏的屏幕区域不要目标窗口，相对区域才要', () => {
    const screen: FlowStep = {
      type: 'wait-text',
      text: '保存',
      region: { space: 'screen', x: 0, y: 0, width: 200, height: 32 },
    };
    expect(missingOf(screen)).toEqual([]);
    expect(missingOf({ ...screen, region: { ...screen.region!, space: 'client' } })).toEqual(['target']);
    expect(missingOf({ ...screen, region: { ...screen.region!, space: 'window' } })).toEqual(['target']);
  });

  it('text() 的屏幕区域同样不需要目标窗口', () => {
    const step: FlowStep = {
      type: 'if',
      cond: 'text("保存")',
      steps: [{ type: 'sleep', ms: 1 }],
      probe: { region: { space: 'screen', x: 0, y: 0, width: 200, height: 32 } },
    };
    expect(missingOf(step)).not.toContain('probe');
  });

  it('text() 条件必须同时给窗口和区域', () => {
    const step: FlowStep = {
      type: 'if',
      cond: 'text("保存")',
      steps: [{ type: 'sleep', ms: 1 }],
    };
    expect(missingOf(step)).toContain('probe');
    expect(missingHint(step)).toContain('条件读哪里');

    const ready: FlowStep = {
      ...step,
      probe: {
        target: { process: 'notepad' },
        region: { space: 'client', x: 0, y: 0, width: 600, height: 32 },
      },
    };
    expect(missingOf(ready)).not.toContain('probe');
  });
});

describe('branchesOf / childSteps', () => {
  const block: FlowStep = {
    type: 'if',
    cond: 'n > 0',
    steps: [{ type: 'sleep', ms: 1 }],
    else: [{ type: 'sleep', ms: 2 }],
  };

  it('if 有两条分支，否则可以还没写', () => {
    const branches = branchesOf(block);
    expect(branches.map((b) => b.label)).toEqual(['条件成立', '条件不符']);
    expect(branches[1].steps).toHaveLength(1);
    expect(branchesOf({ type: 'if', cond: 'true' }).map((b) => b.steps.length)).toEqual([0, 0]);
  });

  it('childSteps 把两条分支合起来，用于计数与过滤', () => {
    expect(childSteps(block)).toHaveLength(2);
    expect(childSteps({ type: 'while', cond: 'true', steps: [{ type: 'sleep' }] })).toHaveLength(1);
    expect(childSteps({ type: 'break' })).toHaveLength(0);
  });
});

describe('control-flow summaries', () => {
  it('摘要说条件，不把字段名念一遍', () => {
    expect(stepSummary({ type: 'if', cond: 'n > 0' })).toContain('如果 n > 0');
    expect(stepSummary({ type: 'while', cond: 'n < 3' })).toContain('当 n < 3 时重复');
    expect(stepSummary({ type: 'for', cond: 'i < 3' })).toContain('计数 i < 3');
    expect(stepSummary({ type: 'set', name: 'n', value: 'n + 1' })).toBe('设 n = n + 1');
    expect(stepSummary({ type: 'break' })).toBe('跳出当前循环');
    expect(stepSummary({ type: 'return' })).toBe('结束当前流程，调用方继续');
  });
});
