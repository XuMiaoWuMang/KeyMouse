import { describe, expect, it } from 'vitest';

import { pickSpaceLabel, shouldAttachWindow } from './pick';

describe('pick result rules', () => {
  it('客户区/窗口坐标绑定进程，屏幕坐标不绑定', () => {
    expect(shouldAttachWindow('client')).toBe(true);
    expect(shouldAttachWindow('window')).toBe(true);
    expect(shouldAttachWindow('screen')).toBe(false);
  });

  it('坐标空间有中文说明', () => {
    expect(pickSpaceLabel('client')).toBe('客户区');
    expect(pickSpaceLabel('window')).toBe('窗口');
    expect(pickSpaceLabel('screen')).toBe('屏幕');
  });
});
