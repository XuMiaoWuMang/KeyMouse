import { describe, expect, it } from 'vitest';
import type { FlowStep } from './model';
import { childVisible, filterSteps, matchesFilter, searchText } from './filter';

const steps: FlowStep[] = [
  { type: 'focus', target: { process: 'notepad' } },
  { type: 'type', text: '你好，世界' },
  { type: 'repeat', times: 2, steps: [{ type: 'sleep', ms: 150 }, { type: 'wheel', delta: -120 }] },
];

describe('searchText', () => {
  it('把类型名、标题和摘要一起放进可搜文本', () => {
    const text = searchText(steps[1]);
    expect(text).toContain('输入文字');
    expect(text).toContain('你好，世界');
    expect(text).toContain('type');
  });
});

describe('matchesFilter', () => {
  it('空串的时候全部通过', () => {
    expect(steps.every((step) => matchesFilter(step, ''))).toBe(true);
    expect(matchesFilter(steps[0], '   ')).toBe(true);
  });

  it('按摘要里的文字命中', () => {
    expect(matchesFilter(steps[1], '世界')).toBe(true);
    expect(matchesFilter(steps[1], '不存在')).toBe(false);
  });

  it('父命中时整组露出来（否则展开了也看不见内容）', () => {
    expect(matchesFilter(steps[2], '重复')).toBe(true);
    expect(matchesFilter(steps[2], '滚轮')).toBe(true);
  });

  it('大小写不敏感', () => {
    expect(matchesFilter(steps[1], 'TYPE')).toBe(true);
  });
});

describe('childVisible', () => {
  it('父命中就显示全部子步骤', () => {
    const group = steps[2];
    expect(childVisible(group, (group.steps ?? [])[0], '重复')).toBe(true);
    expect(childVisible(group, (group.steps ?? [])[1], '重复')).toBe(true);
  });

  it('父没命中时只显示自己命中的那个', () => {
    const group = steps[2];
    const sleep = (group.steps ?? [])[0];
    const wheel = (group.steps ?? [])[1];
    expect(childVisible(group, sleep, '滚轮')).toBe(false);
    expect(childVisible(group, wheel, '滚轮')).toBe(true);
  });
});

describe('filterSteps', () => {
  it('保持原有顺序，编号不因为过滤而变', () => {
    const kept = filterSteps(steps, '等');
    expect(kept.map((step) => step.type)).toEqual(['repeat']);
    expect(filterSteps(steps, '')).toHaveLength(3);
  });
});
