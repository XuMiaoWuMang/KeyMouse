import { describe, expect, it } from 'vitest';

import { firstFailure, firstLine, resultsFromReport, runConclusion, runHint, runIsBad } from './run';

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

  it('退出码按 cli.md 那张表说，别编', () => {
    expect(runConclusion(3, false)).toContain('没找到符合条件的窗口或文字');
    expect(runConclusion(4, false)).toContain('目标窗口不可用');
    expect(runConclusion(5, false)).toContain('焦点没抢到');
    expect(runConclusion(7, false)).toContain('被停止或取消');
    expect(runConclusion(3, false)).toContain('退出码 3');
    expect(runConclusion(42, false)).toBe('没跑通（退出码 42）。');
  });

  it('报告里那一步自己说的话优先于通用解释', () => {
    const text = runConclusion(4, false, false, '目标窗口不可用：HYP(27036) 米哈游启动器 已最小化');
    expect(text).toContain('目标窗口不可用（最小化、隐藏或被系统挡住）');
    expect(text).toContain('HYP(27036)');
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

  it('子流程内联的结果往上卷到「调用另一个流程」那一行', () => {
    const report = JSON.stringify({
      commands: [
        { index: 1, origin: '1', command: '等 80 ms', exitCode: 0 },
        { index: 2, origin: '2.1', command: '等 120 ms', exitCode: 0 },
        { index: 3, origin: '2.2', command: 'KeyMouse mouse wheel -120', exitCode: 0 },
        { index: 4, origin: '3', command: '等 60 ms', exitCode: 0 },
      ],
    });
    const results = resultsFromReport(report, false);
    expect(results['2']).toEqual({ ok: true, text: '2 步都成功', dry: false });
    expect(results['2.1'].ok).toBe(true);
  });

  it('卷起来的那句带上第一个失败的原因', () => {
    const report = JSON.stringify({
      commands: [
        { index: 1, origin: '2.1', command: '等 120 ms', exitCode: 0 },
        { index: 2, origin: '2.2', command: 'KeyMouse window focus --process nope', exitCode: 3 },
      ],
    });
    const results = resultsFromReport(report, false);
    expect(results['2'].ok).toBe(false);
    expect(results['2'].text).toContain('退出码 3');
  });
  it('空报告、坏 JSON、没有 origin 的都被忽略', () => {
    expect(resultsFromReport('', true)).toEqual({});
    expect(resultsFromReport('{ not json', true)).toEqual({});
    expect(resultsFromReport(JSON.stringify({ commands: [{ index: 1, exitCode: 0 }] }), true)).toEqual({});
    expect(resultsFromReport(JSON.stringify({ nothing: true }), true)).toEqual({});
  });
});

describe('firstLine', () => {
  it('剥掉「错误：」前缀，只留第一行', () => {
    expect(firstLine('错误：没有窗口匹配 --process HYP\n\n当前可见的窗口：…')).toBe('没有窗口匹配 --process HYP');
    expect(firstLine('   ')).toBe('');
  });
});

describe('firstFailure', () => {
  it('挑最小的行号，去掉「退出码 N：」前缀', () => {
    const results = {
      '2': { ok: false, text: '退出码 4：目标窗口不可用：HYP 已最小化' },
      '10': { ok: false, text: '退出码 1：别的错' },
      '1': { ok: true, text: '等 100 ms' },
    };
    expect(firstFailure(results)).toBe('目标窗口不可用：HYP 已最小化');
    expect(firstFailure({ '1': { ok: true, text: 'x' } })).toBe('');
  });
});