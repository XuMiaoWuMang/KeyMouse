import { describe, expect, it } from 'vitest';

import type { FlowStep } from './model';
import { applyPickedField, editStepField, type PickResult } from './pickField';

const inFirstWindow: PickResult = {
  space: 'client', x: 10, y: 20, width: 0, height: 0,
  screen: { x: -900, y: 220, width: 0, height: 0 },
  process: 'first', title: '窗口一',
};
const inSecondWindow: PickResult = {
  space: 'client', x: 30, y: 40, width: 0, height: 0,
  screen: { x: 830, y: 440, width: 0, height: 0 },
  process: 'second', title: '窗口二',
};

describe('pick fields in drag steps', () => {
  it('后台拖动拒绝跨窗口拼接坐标', () => {
    const first = applyPickedField({ type: 'drag' }, 'point', 'from', inFirstWindow);
    expect(() => applyPickedField(first.step, 'point', 'to', inSecondWindow)).toThrow('同一个目标窗口');
    expect(applyPickedField(first.step, 'point', 'to', inFirstWindow).step.to).toEqual(first.step.from);
  });

  it('客户区拖动取点保留用户已有目标，绝不自动改成选中窗口', () => {
    const target = { process: 'explicit', class: 'ChosenClass', title: '手动选择器' };
    expect(() => applyPickedField({ type: 'drag', target }, 'point', 'from', inFirstWindow)).toThrow('同一个目标窗口');
    const result = applyPickedField({ type: 'drag', target }, 'point', 'from', { ...inFirstWindow, ...target });
    expect(result.step.from).toEqual({ space: 'client', x: 10, y: 20 });
    expect(result.step.target).toBe(target);
    expect(result.attachedProcess).toBeNull();
  });

  it('显式旧客户区坐标保持原空间，相对取点不改另一端和用户目标', () => {
    const old: FlowStep = {
      type: 'drag',
      target: { process: 'explicit', class: 'ChosenClass', title: '手动选择器' },
      from: { space: 'client', x: 1, y: 2 },
      to: { space: 'client', x: 3, y: 4 },
    };
    const result = applyPickedField(old, 'point', 'from', { ...inFirstWindow, process: 'explicit', class: 'ChosenClass', title: '手动选择器' });
    expect(result.step.from).toEqual({ space: 'client', x: 10, y: 20 });
    expect(result.step.to).toBe(old.to);
    expect(result.step.target).toBe(old.target);
    expect(old.from).toEqual({ space: 'client', x: 1, y: 2 });
  });

  it('缺失一端的旧客户区拖动沿用另一端空间，并在没有目标时补窗口', () => {
    const result = applyPickedField({ type: 'drag', to: { space: 'client', x: 3, y: 4 } }, 'point', 'from', inFirstWindow);
    expect(result.step.from).toEqual({ space: 'client', x: 10, y: 20 });
    expect(result.step.target).toEqual({ process: 'first', title: '窗口一' });
  });

  it('重新取显式屏幕点不会因为另一端client而悄悄转换空间', () => {
    const result = applyPickedField({
      type: 'drag',
      from: { space: 'screen', x: 1, y: 2 },
      to: { space: 'client', x: 3, y: 4 },
    }, 'point', 'from', inFirstWindow);
    expect(result.step.from).toEqual({ space: 'screen', x: -900, y: 220 });
    expect(result.step.to).toEqual({ space: 'client', x: 3, y: 4 });
  });

  it('取标题栏也使用屏幕坐标，旧client点取到非客户区时拒绝而非改空间', () => {
    const titleBar: PickResult = { ...inFirstWindow, space: 'window', x: 10, y: 5 };
    expect(applyPickedField({ type: 'drag' }, 'point', 'from', titleBar).step.from)
      .toEqual({ space: 'screen', x: -900, y: 220 });
    const old: FlowStep = { type: 'drag', from: { space: 'client', x: 1, y: 2 } };
    expect(() => applyPickedField(old, 'point', 'from', titleBar)).toThrow('客户区');
    expect(old.from).toEqual({ space: 'client', x: 1, y: 2 });
  });
});

describe('other picked fields', () => {
  it('切换拖动坐标系同步两端，撤销快照不变', () => {
    const original: FlowStep = { type: 'drag', from: { x: 10, y: 20 }, to: { x: 30, y: 40 } };
    const screen = editStepField(original, 'to.space', 'screen');
    expect([screen.from?.space, screen.to?.space]).toEqual(['screen', 'screen']);
    expect(editStepField(screen, 'from.space', undefined)).toEqual(original);
    expect(original.from?.space).toBeUndefined();
  });
  it('屏幕点击重新取点保留屏幕空间，标题栏不是客户区', () => {
    const old: FlowStep = { type: 'click', at: { space: 'screen', x: 5, y: 6 } };
    const expected = { space: 'screen', x: -900, y: 220 };
    expect(applyPickedField(old, 'point', 'at', inFirstWindow).step.at).toEqual(expected);
    expect(applyPickedField({ type: 'click' }, 'point', 'at', { ...inFirstWindow, space: 'window' }).step.at).toEqual(expected);
  });
  it('读屏嵌套区域保留相对坐标及区域所属的目标层级', () => {
    const picked = { ...inFirstWindow, width: 300, height: 40 };
    const result = applyPickedField({ type: 'if', cond: 'text("保存")' }, 'region', 'probe.region', picked);
    expect(result.step.probe).toEqual({
      region: { space: 'client', x: 10, y: 20, width: 300, height: 40 },
      target: { process: 'first', title: '窗口一' },
    });
    expect(result.step.target).toBeUndefined();
  });

  it('非拖动屏幕取点也不新增或删除显式目标', () => {
    const target = { process: 'explicit' };
    const picked: PickResult = { ...inFirstWindow, ...inFirstWindow.screen, space: 'screen' };
    const result = applyPickedField({ type: 'click', target }, 'point', 'at', picked);
    expect(result.step.at).toEqual({ space: 'screen', x: -900, y: 220 });
    expect(result.step.target).toBe(target);
  });
});
