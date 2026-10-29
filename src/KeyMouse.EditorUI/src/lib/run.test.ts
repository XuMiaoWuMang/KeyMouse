import { describe, expect, it } from 'vitest';

import { runConclusion, runHint, runIsBad } from './run';

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
