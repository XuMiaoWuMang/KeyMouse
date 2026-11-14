import { describe, expect, it } from 'vitest';
import { HUD_ROWS, compactDetail, hudRows, parseHudLine, statusText, stepName } from './hud';

describe('parseHudLine', () => {
  it('把流程步骤行读成编号、步骤名、内容和状态', () => {
    expect(parseHudLine('  [  3/8] ok       KeyMouse click 100, 200')).toEqual({
      index: 3,
      total: 8,
      name: '点击',
      detail: 'click 100, 200',
      status: 'done',
    });
  });

  it('脚本输出的第一个词就是步骤名', () => {
    expect(parseHudLine('  [  1/12] 等待  sleep 400')).toEqual({
      index: 1,
      total: 12,
      name: '等待',
      detail: 'sleep 400',
      status: 'done',
    });
  });

  it('区分跳过与失败', () => {
    expect(parseHudLine('  [  4/8] skip     KeyMouse key enter')?.status).toBe('skipped');
    expect(parseHudLine('  [  5/8] exit 3   KeyMouse click 9, 9')?.status).toBe('failed');
  });

  it('把“执行中”的开始行标成当前操作', () => {
    expect(parseHudLine('  [  3/8] 执行中  等 400 ms')).toEqual({
      index: 3,
      total: 8,
      name: '等待时间',
      detail: '等 400 ms',
      status: 'current',
    });
  });

  it('忽略没有总数、初始化行和格式不对的行', () => {
    expect(parseHudLine('  [  3] ok       KeyMouse click 1, 2')).toBeNull();
    expect(parseHudLine('流程结束：3 步成功')).toBeNull();
    expect(parseHudLine('  [  0/8] ok       x')).toBeNull();
    expect(parseHudLine('  [  9/8] ok       x')).toBeNull();
  });

  it('超过 54 字的内容截断，但不丢前面的关键信息', () => {
    const long = `KeyMouse type ${'很长的输入内容'.repeat(20)}`;
    const detail = compactDetail(long);
    expect(detail.length).toBeLessThanOrEqual(54);
    expect(detail.endsWith('…')).toBe(true);
  });
});

describe('stepName', () => {
  it('把常见命令归成编辑器里的名字', () => {
    expect(stepName('KeyMouse mouse move 10 20')).toBe('移动鼠标');
    expect(stepName('KeyMouse window 记事本')).toBe('切换窗口');
    expect(stepName('等 80 ms')).toBe('等待时间');
    expect(stepName('设 x = 1')).toBe('设变量');
    expect(stepName('确认 x == 1')).toBe('断言');
    expect(stepName('找「保存」并点它的中心')).toBe('找字并点它');
    expect(stepName('???')).toBe('操作');
  });
});

describe('hudRows', () => {
  it('只保留最近 7 条步骤', () => {
    const lines = Array.from({ length: 9 }, (_, index) => `  [${index + 1}/9] ok       KeyMouse click ${index}, 0`);
    const rows = hudRows(lines);
    expect(rows).toHaveLength(HUD_ROWS);
    expect(rows[0].index).toBe(3);
    expect(rows.at(-1)?.index).toBe(9);
  });

  it('同一步的开始行与完成行合并，后到的状态覆盖前面的', () => {
    const rows = hudRows([
      '  [1/2] 执行中  KeyMouse click 10, 20',
      '  [1/2] ok       KeyMouse click 10, 20',
      '  [2/2] 执行中  等 80 ms',
    ]);
    expect(rows).toEqual([
      { index: 1, total: 2, name: '点击', detail: 'click 10, 20', status: 'done' },
      { index: 2, total: 2, name: '等待时间', detail: '等 80 ms', status: 'current' },
    ]);
  });

  it('状态文案直接给人看', () => {
    expect(statusText('done')).toBe('完成');
    expect(statusText('skipped')).toBe('跳过');
    expect(statusText('failed')).toBe('失败');
    expect(statusText('current')).toBe('执行中');
  });
});
