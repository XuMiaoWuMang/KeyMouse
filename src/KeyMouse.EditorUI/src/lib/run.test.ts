import { describe, expect, it } from 'vitest';

import { parseStepResult, runConclusion, runHint, runIsBad } from './run';

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

describe('parseStepResult', () => {
  it('读成功那一步的编号与描述', () => {
    expect(parseStepResult('[  1/2] ok         等 6000 ms')).toEqual({
      index: 1,
      ok: true,
      text: '等 6000 ms',
    });
  });

  it('读失败那一步的退出码与原因', () => {
    expect(parseStepResult('[ 3/8] exit 3 没有窗口匹配 --process notepad')).toEqual({
      index: 3,
      ok: false,
      text: '退出码 3：没有窗口匹配 --process notepad',
    });
  });

  it('只有退出码没有原因也认', () => {
    expect(parseStepResult('[ 2/5] exit 1')).toEqual({ index: 2, ok: false, text: '退出码 1' });
  });

  it('不是这一步报告的行一律 null', () => {
    expect(parseStepResult('流程 D:\\a\\b.json')).toBeNull();
    expect(parseStepResult('录制于 2026-10-30T20:00:00+08:00')).toBeNull();
    expect(parseStepResult('[ 1/9] ???')).toBeNull();
  });

  it('编号不是正数的行不算', () => {
    expect(parseStepResult('[ 0/3] ok x')).toBeNull();
    expect(parseStepResult('[ 2/0] ok x')).toBeNull();
  });
});