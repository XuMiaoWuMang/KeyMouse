import { describe, expect, it } from 'vitest';

import { layoutFlow, NODE_H, NODE_W, PAD } from './canvas';
import type { FlowStep } from './model';

const simple: FlowStep[] = [
  { type: 'focus' },
  { type: 'sleep', ms: 10 },
];

describe('layoutFlow', () => {
  it('顶层从上到下排，同一条竖线', () => {
    const layout = layoutFlow(simple);
    expect(layout.nodes).toHaveLength(2);
    expect(layout.nodes[0].x).toBe(layout.nodes[1].x);
    expect(layout.nodes[1].y - layout.nodes[0].y).toBeGreaterThanOrEqual(NODE_H);
    expect(layout.nodes.map((node) => node.category)).toEqual(['action', 'action']);
    expect(layout.nodes.map((node) => node.origin)).toEqual(['1', '2']);
  });

  it('if 的两条分支并排在右边，空否则给一个可点的空槽', () => {
    const layout = layoutFlow([
      {
        type: 'if',
        cond: 'true',
        steps: [{ type: 'sleep', ms: 1 }],
      },
      { type: 'sleep', ms: 2 },
    ]);
    expect(layout.labels.map((label) => label.text)).toEqual(['成立时', '否则']);
    expect(layout.empties).toHaveLength(1);
    expect(layout.empties[0].branch).toBe('else');
    const body = layout.nodes.find((node) => node.origin === '1.1')!;
    expect(body.x).toBeGreaterThan(layout.nodes[0].x);
    expect(layout.edges.some((edge) => edge.kind === 'branch')).toBe(true);
  });

  it('循环的子步骤在右边往下长，后续顶层步骤回到主线下方', () => {
    const layout = layoutFlow([
      {
        type: 'while',
        cond: 'n < 2',
        steps: [{ type: 'sleep', ms: 1 }],
      },
      { type: 'sleep', ms: 2 },
    ]);
    const loop = layout.nodes[0];
    const body = layout.nodes.find((node) => node.origin === '1.1')!;
    const after = layout.nodes.find((node) => node.origin === '2')!;
    expect(body.x).toBeGreaterThan(loop.x);
    expect(body.y).toBeGreaterThan(loop.y + NODE_H);
    expect(after.x).toBe(loop.x);
    expect(after.y).toBeGreaterThan(body.y);
  });

  it('所有分支的缝隙都是横线，节点宽度固定', () => {
    const layout = layoutFlow([
      {
        type: 'if',
        cond: 'true',
        steps: [{ type: 'sleep', ms: 1 }, { type: 'sleep', ms: 2 }],
        else: [{ type: 'sleep', ms: 3 }],
      },
    ]);
    const thenGaps = layout.gaps.filter((gap) => gap.branch === 'steps' && gap.parentPath.length === 1);
    expect(thenGaps).toHaveLength(3);
    expect(layout.nodes.every((node) => node.w === NODE_W)).toBe(true);
    expect(layout.nodes[0].x).toBe(PAD);
  });

  it('空流程也能铺出画布，不返回 NaN', () => {
    const layout = layoutFlow([]);
    expect(layout.nodes).toEqual([]);
    expect(layout.width).toBeGreaterThan(0);
    expect(layout.height).toBeGreaterThan(0);
    expect(Number.isFinite(layout.width)).toBe(true);
  });
});
