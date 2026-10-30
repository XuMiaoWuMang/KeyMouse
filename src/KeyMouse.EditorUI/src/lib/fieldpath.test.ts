// 路径读写的单测：这些规矩（空值删键、删空的嵌套对象、不原地改）在界面上到处用，
// 靠点击去验证太慢，也不容易验证"父键有没有被一起删掉"。

import { describe, expect, it } from 'vitest';

import { readBool, readInt, readPath, readText, writePath } from './fieldpath';

describe('readPath', () => {
  it('按点号路径取值', () => {
    expect(readPath({ at: { x: 12 } }, 'at.x')).toBe(12);
  });

  it('中途断了给 undefined，不抛错', () => {
    expect(readPath({}, 'at.x')).toBeUndefined();
    expect(readPath({ at: null }, 'at.x')).toBeUndefined();
  });
});

describe('writePath', () => {
  it('写值会一路建对象', () => {
    expect(writePath({}, 'when.text', '保存')).toEqual({ when: { text: '保存' } });
  });

  it('写空串等于删键', () => {
    expect(writePath({ note: '旧' }, 'note', '')).toEqual({});
  });

  it('删空的嵌套对象连父键一起删', () => {
    expect(writePath({ when: { text: '旧' } }, 'when.text', '')).toEqual({});
  });

  it('不原地改原对象', () => {
    const before = { at: { x: 1 }, steps: [] as unknown[] };
    const after = writePath(before, 'at.x', 2);
    expect(before.at.x).toBe(1);
    expect(after.at).toEqual({ x: 2 });
  });

  it('兄弟字段不受影响', () => {
    expect(writePath({ at: { x: 1, y: 2 } }, 'at.x', 9)).toEqual({ at: { x: 9, y: 2 } });
  });
});

describe('读出来的类型', () => {
  it('数字与文本按需转换', () => {
    expect(readInt({ ms: 30 }, 'ms')).toBe(30);
    expect(readInt({ ms: '30' }, 'ms')).toBe(30);
    expect(readInt({}, 'ms', 5)).toBe(5);
    expect(readText({ text: 42 }, 'text')).toBe('42');
  });

  it('开关只认 true', () => {
    expect(readBool({ confirm: true }, 'confirm')).toBe(true);
    expect(readBool({ confirm: 'yes' }, 'confirm')).toBe(false);
  });
});
