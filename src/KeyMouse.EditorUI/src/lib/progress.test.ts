import { describe, expect, it } from 'vitest';

import { parseRunProgress, progressText } from './progress';

describe('parseRunProgress', () => {
  it('认得出流程输出里的编号', () => {
    expect(parseRunProgress('    [  1/8] ok       切到 notepad')).toEqual({ step: 1, total: 8 });
  });

  it('认得出脚本输出里的编号', () => {
    expect(parseRunProgress('[  2/12] 失败  window focus --process notepad')).toEqual({
      step: 2,
      total: 12,
    });
  });

  it('不是进度行就返回 null', () => {
    expect(parseRunProgress('流程结束：0 步成功，1 步失败，退出码 3')).toBeNull();
    expect(parseRunProgress('')).toBeNull();
  });

  it('编号越界时不猜', () => {
    expect(parseRunProgress('[  0/8] ok')).toBeNull();
    expect(parseRunProgress('[  9/8] ok')).toBeNull();
    expect(parseRunProgress('[  1/0] ok')).toBeNull();
  });

  it('行里同时出现两个编号时取第一个', () => {
    expect(parseRunProgress('[  3/4] 跳到 [  1/2]')).toEqual({ step: 3, total: 4 });
  });
});

describe('progressText', () => {
  it('有进度才说话', () => {
    expect(progressText({ step: 3, total: 8 })).toBe('第 3/8 步');
    expect(progressText(null)).toBeNull();
  });
});
