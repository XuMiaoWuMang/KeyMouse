import { describe, expect, it } from 'vitest';
import {
  addSection,
  moveSection,
  normalizeSections,
  removeSection,
  sectionOf,
  shiftAfterDelete,
  shiftAfterInsert,
  spanOf,
} from './sections';

const steps = ['a', 'b', 'c', 'd', 'e'];

describe('normalizeSections', () => {
  it('排序、去重、越过末尾的丢掉', () => {
    const out = normalizeSections(
      [
        { at: 3, title: '后' },
        { at: 0, title: '前' },
        { at: 3, title: '重复' },
        { at: 99, title: '越界' },
        { at: -1, title: '负数' },
      ],
      5,
    );
    expect(out).toEqual([
      { at: 0, title: '前' },
      { at: 3, title: '后' },
    ]);
  });

  it('空标题保留为空：那是一条不带字的分隔线', () => {
    expect(normalizeSections([{ at: 1, title: '   ' }], 5)[0].title).toBe('');
  });
});

describe('sectionOf / spanOf', () => {
  const sections = normalizeSections(
    [
      { at: 0, title: '开头' },
      { at: 2, title: '中间' },
    ],
    5,
  );

  it('按边界判断归属', () => {
    expect(sectionOf(sections, 0)).toBe(0);
    expect(sectionOf(sections, 1)).toBe(0);
    expect(sectionOf(sections, 2)).toBe(2);
    expect(sectionOf(sections, 4)).toBe(2);
    expect(sectionOf([], 3)).toBe(null);
  });

  it('区间到下一个边界或末尾为止', () => {
    expect(spanOf(sections, 0, 5)).toEqual([0, 2]);
    expect(spanOf(sections, 2, 5)).toEqual([2, 5]);
  });
});

describe('插入与删除后的边界', () => {
  const sections = normalizeSections(
    [
      { at: 2, title: 'b 开始' },
      { at: 4, title: 'd 开始' },
    ],
    5,
  );

  it('插在边界之前，边界右移', () => {
    expect(shiftAfterInsert(sections, 0).map((s) => s.at)).toEqual([3, 5]);
  });

  it('插在边界处算进这个分组，边界不动', () => {
    expect(shiftAfterInsert(sections, 2).map((s) => s.at)).toEqual([2, 5]);
  });

  it('删在边界之前，边界左移；删掉的正好是边界就保持不变', () => {
    expect(shiftAfterDelete(sections, 1).map((s) => s.at)).toEqual([1, 3]);
    expect(shiftAfterDelete(sections, 2).map((s) => s.at)).toEqual([2, 3]);
  });
});

describe('moveSection', () => {
  const sections = normalizeSections([{ at: 0, title: '第一组' }, { at: 2, title: '第二组' }], 5);

  it('拖分组头就是把整段搬走，组内顺序不变', () => {
    const moved = moveSection(steps, sections, 0, 4);
    expect(moved.steps).toEqual(['c', 'd', 'a', 'b', 'e']);
    // 原第二组（c、d）现在从第一行开始；被拖走的第一组落到它后面。
    expect(moved.sections).toEqual([
      { at: 0, title: '第二组' },
      { at: 2, title: '第一组' },
    ]);
  });

  it('把末尾那一组拖到最前面，不会顺手吞掉后面的行', () => {
    // 只有一个分组、而且它管到最后：搬到前面后要在新结尾补一条空标题分隔线。
    const alone = normalizeSections([{ at: 2, title: '尾巴' }], 5);
    const moved = moveSection(steps, alone, 2, 0);
    expect(moved.steps).toEqual(['c', 'd', 'e', 'a', 'b']);
    expect(moved.sections).toEqual([
      { at: 0, title: '尾巴' },
      { at: 3, title: '' },
    ]);
  });

  it('放到自己这一段里等于没动', () => {
    const moved = moveSection(steps, sections, 0, 1);
    expect(moved.steps).toEqual(steps);
    expect(moved.sections).toEqual(sections);
  });

  it('挪到最后一段之后，边界跟着落到末尾', () => {
    const moved = moveSection(steps, sections, 2, 5);
    expect(moved.steps).toEqual(['a', 'b', 'c', 'd', 'e']);
    expect(moved.sections.map((s) => s.at)).toEqual([0, 2]);
  });
});

describe('addSection / removeSection', () => {
  it('加一个边界是幂等的：同一个位置只留一个', () => {
    const once = addSection([], 2, '新组');
    const twice = addSection(once, 2, '改名了');
    expect(twice).toEqual([{ at: 2, title: '改名了' }]);
  });

  it('去掉分组头，步骤留在原地', () => {
    const before = normalizeSections([{ at: 1, title: 'x' }, { at: 3, title: 'y' }], 5);
    expect(removeSection(before, 1)).toEqual([{ at: 3, title: 'y' }]);
  });
});
