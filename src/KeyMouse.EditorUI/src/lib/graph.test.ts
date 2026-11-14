import { describe, expect, it } from 'vitest';

import { buildGraph } from './graph';
import type { FlowStep } from './model';
import { pathKey, type NodePath } from './tree';

function p(...entries: [('steps' | 'else'), number][]): NodePath {
  return entries.map(([branch, index]) => ({ branch, index }));
}

function keys(steps: FlowStep[]): string[] {
  return buildGraph(steps).map((edge) =>
    `${pathKey(edge.from)}->${pathKey(edge.to)}:${edge.kind}${edge.label ? `:${edge.label}` : ''}`,
  );
}

describe('buildGraph', () => {
  it('普通步骤按顺序连成一条线', () => {
    expect(keys([
      { type: 'focus' },
      { type: 'sleep', ms: 10 },
    ])).toEqual([
      'steps:0->steps:1:next',
    ]);
  });

  it('if 有两个出口，两条分支最后都汇到下一步', () => {
    const edges = keys([
      {
        type: 'if',
        cond: 'true',
        steps: [{ type: 'focus' }],
        else: [{ type: 'sleep', ms: 1 }],
      },
      { type: 'sleep', ms: 2 },
    ]);

    expect(edges).toContain('steps:0->steps:0/steps:0:true:成立');
    expect(edges).toContain('steps:0->steps:0/else:0:false:否则');
    expect(edges).toContain('steps:0/steps:0->steps:1:next');
    expect(edges).toContain('steps:0/else:0->steps:1:next');
    expect(edges).not.toContain('steps:0->steps:1:true:成立');
  });

  it('if 的空分支直接连到下一步，并保留 true/false 标签', () => {
    const edges = keys([
      {
        type: 'if',
        cond: 'true',
        steps: [{ type: 'focus' }],
      },
      { type: 'sleep', ms: 2 },
    ]);

    expect(edges).toContain('steps:0->steps:1:false:否则');
    expect(edges).toContain('steps:0/steps:0->steps:1:next');
    expect(edges).not.toContain('steps:0->steps:1:true:成立');
  });

  it('while 的循环体最后一步同时回开头和去下一步', () => {
    const edges = keys([
      {
        type: 'while',
        cond: 'n < 2',
        steps: [{ type: 'focus' }, { type: 'sleep', ms: 1 }],
      },
      { type: 'sleep', ms: 2 },
    ]);

    expect(edges).toContain('steps:0->steps:0/steps:0:loop:循环');
    expect(edges).toContain('steps:0/steps:0->steps:0/steps:1:next');
    expect(edges).toContain('steps:0/steps:1->steps:0:back:返回');
    expect(edges).toContain('steps:0/steps:1->steps:1:exit:退出');
    expect(edges).not.toContain('steps:0->steps:1:exit:结束');
  });

  it('break 只跳出循环，continue 只回开头，不再补普通返回/退出', () => {
    const edges = keys([
      {
        type: 'while',
        cond: 'n < 2',
        steps: [
          { type: 'focus' },
          { type: 'sleep', ms: 1 },
          {
            type: 'if',
            cond: 'done',
            steps: [{ type: 'break' }],
            else: [{ type: 'continue' }],
          },
        ],
      },
      { type: 'sleep', ms: 2 },
    ]);

    expect(edges).toContain('steps:0/steps:2/steps:0->steps:1:exit:跳出');
    expect(edges).toContain('steps:0/steps:2/else:0->steps:0:back:继续');
    expect(edges).not.toContain('steps:0/steps:2/steps:0->steps:0:back:返回');
    expect(edges).not.toContain('steps:0/steps:2/else:0->steps:1:exit:退出');
    expect(edges).not.toContain('steps:0/steps:2/else:0->steps:0:back:返回');
    expect(edges).not.toContain('steps:0/steps:2/steps:0->steps:1:exit:退出');
    expect(edges).not.toContain('steps:0/steps:0->steps:0:back:返回');
    expect(edges).not.toContain('steps:0/steps:1->steps:0:back:返回');
  });

  it('二层 if 的路径仍按树结构生成，不串到顶层', () => {
    const edges = keys([
      {
        type: 'if',
        cond: 'outer',
        steps: [
          {
            type: 'if',
            cond: 'inner',
            steps: [{ type: 'focus' }],
            else: [{ type: 'sleep', ms: 1 }],
          },
        ],
      },
      { type: 'sleep', ms: 2 },
    ]);

    expect(edges).toContain('steps:0->steps:0/steps:0:true:成立');
    expect(edges).toContain('steps:0/steps:0->steps:0/steps:0/steps:0:true:成立');
    expect(edges).toContain('steps:0/steps:0->steps:0/steps:0/else:0:false:否则');
    expect(edges).toContain('steps:0/steps:0/steps:0->steps:1:next');
    expect(edges).toContain('steps:0/steps:0/else:0->steps:1:next');
  });

  it('循环体为空时只画 while 到下一步的结束边', () => {
    expect(keys([
      { type: 'while', cond: 'n < 2' },
      { type: 'sleep', ms: 2 },
    ])).toEqual([
      'steps:0->steps:1:exit:结束',
    ]);
  });

  it('return 是终端，后续步骤不再接普通下一步', () => {
    const edges = keys([
      { type: 'focus' },
      { type: 'return' },
      { type: 'sleep', ms: 2 },
    ]);

    expect(edges).toEqual(['steps:0->steps:1:next']);
    expect(edges).not.toContain('steps:1->steps:2:next');
  });

  it('if 两条分支都 return 时，if 后面不再有可达路径', () => {
    const edges = keys([
      {
        type: 'if',
        cond: 'done',
        steps: [{ type: 'return' }],
        else: [{ type: 'return' }],
      },
      { type: 'sleep', ms: 2 },
    ]);

    expect(edges).toContain('steps:0->steps:0/steps:0:true:成立');
    expect(edges).toContain('steps:0->steps:0/else:0:false:否则');
    expect(edges.some((edge) => edge.endsWith('->steps:1:next'))).toBe(false);
  });
});
