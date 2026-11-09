import { describe, expect, it } from 'vitest';

import { EMPTY_FIELDS, buildCondition, parseCondition, type ConditionFields } from './conditions';

const fields = (patch: Partial<ConditionFields> = {}): ConditionFields => ({ ...EMPTY_FIELDS, ...patch });

describe('buildCondition', () => {
  it('总是成立 / 从不成立', () => {
    expect(buildCondition('true', fields())).toBe('true');
    expect(buildCondition('false', fields())).toBe('false');
  });

  it('比较两个值，必填缺一个就返回空串', () => {
    expect(buildCondition('compare', fields({ left: 'n', operator: '>=', right: '3' }))).toBe('n >= 3');
    expect(buildCondition('compare', fields({ left: '', right: '3' }))).toBe('');
    expect(buildCondition('compare', fields({ left: 'n', right: '' }))).toBe('');
  });

  it('读屏文字要引号转义', () => {
    expect(buildCondition('text', fields({ text: '保存' }))).toBe('text("保存")');
    expect(buildCondition('text', fields({ text: '他说"好"' }))).toBe('text("他说\\"好\\"")');
    expect(buildCondition('text', fields({ text: '   ' }))).toBe('');
  });

  it('窗口/前台/上次读字都要求名字或文字', () => {
    expect(buildCondition('window', fields({ process: 'notepad' }))).toBe('window("notepad")');
    expect(buildCondition('front', fields({ process: '' }))).toBe('');
    expect(buildCondition('found', fields({ text: '副本' }))).toBe('found("副本")');
    expect(buildCondition('foundAny', fields())).toBe('found()');
  });

  it('运行时长必须为正整数', () => {
    expect(buildCondition('elapsed', fields({ ms: '5000' }))).toBe('elapsed() > 5000');
    expect(buildCondition('elapsed', fields({ ms: '0' }))).toBe('');
    expect(buildCondition('elapsed', fields({ ms: '' }))).toBe('');
  });

  it('自定义表达式原样写回', () => {
    expect(buildCondition('custom', fields({ custom: 'n > 1 && text("好")' }))).toBe('n > 1 && text("好")');
    expect(buildCondition('custom', fields({ custom: '  ' }))).toBe('');
  });
});

describe('parseCondition', () => {
  it('识别常用选项', () => {
    expect(parseCondition('true').mode).toBe('true');
    expect(parseCondition('false').mode).toBe('false');
    expect(parseCondition('n >= 3')).toMatchObject({ mode: 'compare', fields: { left: 'n', operator: '>=', right: '3' } });
    expect(parseCondition('text("保存")')).toMatchObject({ mode: 'text', fields: { text: '保存' } });
    expect(parseCondition('window("notepad")')).toMatchObject({ mode: 'window', fields: { process: 'notepad' } });
    expect(parseCondition('front("notepad")')).toMatchObject({ mode: 'front', fields: { process: 'notepad' } });
    expect(parseCondition('found("副本")')).toMatchObject({ mode: 'found', fields: { text: '副本' } });
    expect(parseCondition('found()').mode).toBe('foundAny');
    expect(parseCondition('elapsed() > 5000')).toMatchObject({ mode: 'elapsed', fields: { ms: '5000' } });
  });

  it('空串是"还没选"，复杂表达式退回自定义', () => {
    expect(parseCondition('   ').mode).toBe('');
    expect(parseCondition('text("保存", 0, 0, 600, 32)')).toMatchObject({
      mode: 'custom',
      fields: { custom: 'text("保存", 0, 0, 600, 32)' },
    });
    expect(parseCondition('n > 1 && m < 2').mode).toBe('custom');
  });

  it('生成后再解析不丢字段', () => {
    const generated = buildCondition('compare', fields({ left: 'i', operator: '!=', right: 'j' }));
    expect(parseCondition(generated)).toMatchObject({ mode: 'compare', fields: { left: 'i', operator: '!=', right: 'j' } });
    const text = buildCondition('text', fields({ text: '保存' }));
    expect(parseCondition(text)).toMatchObject({ mode: 'text', fields: { text: '保存' } });
  });
});
