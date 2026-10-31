import { describe, expect, it } from 'vitest';

import { resultsFromReport, runConclusion, runHint, runIsBad } from './run';

describe('runConclusion', () => {
  it('被停止掐掉的不算失败', () => {
    expect(runConclusion(1, true)).toBe('已停止。');
    expect(runIsBad(1, true)).toBe(false);
  });

  it('退出码 0 是全部成功', () => {
    expect(runConclusion(0, false)).toContain('每一步都成功');
    expect(runIsBad(0, false)).toBe(false);
  });

  it('拿不到退出码要说清楚', () => {
    expect(runConclusion(null, false)).toContain('没拿到退出码');
    expect(runIsBad(null, false)).toBe(true);
  });

  it('常见退出码带上原因，不常见的只报数字', () => {
    expect(runConclusion(3, false)).toContain('窗口不满足条件');
    expect(runConclusion(3, false)).toContain('退出码 3');
    expect(runConclusion(42, false)).toBe('没跑通（退出码 42）。');
  });
});

describe('runHint', () => {
  it('试运行与播放的区别要说出来', () => {
    expect(runHint(true)).toContain('不发送任何输入');
    expect(runHint(false)).toContain('真的操作');
  });
});

describe('resultsFromReport', () => {
  const report = JSON.stringify({
    commands: [
      { index: 1, origin: '1', command: 'KeyMouse window focus --process notepad', exitCode: 0 },
      { index: 2, origin: '2', command: 'KeyMouse key type 你好', exitCode: 0 },
      { index: 3, origin: '6.1', command: 'KeyMouse mouse wheel -120', exitCode: 0 },
      { index: 4, origin: '6.1', command: 'KeyMouse mouse wheel -120', exitCode: 3 },
      { index: 5, origin: '7', command: '等 300 ms', exitCode: 0, skipped: true },
    ],
  });

  it('按行位置给结果，分组步最后一次执行留在那一行上', () => {
    const results = resultsFromReport(report, true);
    expect(results['1']).toEqual({ ok: true, text: 'KeyMouse window focus --process notepad', dry: true });
    expect(results['2'].ok).toBe(true);
    expect(results['2'].text).toBe('KeyMouse key type 你好');
    expect(results['6.1'].ok).toBe(false);
    expect(results['6.1'].text).toBe('退出码 3：KeyMouse mouse wheel -120');
  });

  it('跳过的步骤不算成功，但也不是失败', () => {
    const results = resultsFromReport(report, false);
    expect(results['7'].ok).toBe(false);
    expect(results['7'].text).toBe('跳过：等 300 ms');
    expect(results['7'].dry).toBe(false);
  });

  it('空报告、坏 JSON、没有 origin 的都被忽略', () => {
    expect(resultsFromReport('', true)).toEqual({});
    expect(resultsFromReport('{ not json', true)).toEqual({});
    expect(resultsFromReport(JSON.stringify({ commands: [{ index: 1, exitCode: 0 }] }), true)).toEqual({});
    expect(resultsFromReport(JSON.stringify({ nothing: true }), true)).toEqual({});
  });
});
