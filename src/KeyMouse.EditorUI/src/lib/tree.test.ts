import { describe, expect, it } from 'vitest';

import type { FlowStep } from './model';
import {
  allNodes,
  appendToBranch,
  copyNodes,
  duplicateAt,
  insertAt,
  insertManyAt,
  isPathPrefix,
  moveAt,
  moveNodes,
  originOf,
  removeAt,
  replaceAt,
  samePath,
  sequenceOf,
  stepAt,
  type NodePath,
} from './tree';

const flow = (): FlowStep[] => [
  {
    type: 'if',
    cond: 'true',
    steps: [
      { type: 'sleep', ms: 1 },
      { type: 'sleep', ms: 2 },
    ],
    else: [{ type: 'sleep', ms: 3 }],
  },
  { type: 'sleep', ms: 4 },
];

describe('tree paths', () => {
  it('读得到任意深度的步骤，也分得清成立时与否则', () => {
    const steps = flow();
    expect(stepAt(steps, [{ branch: 'steps', index: 0 }])?.type).toBe('if');
    expect(stepAt(steps, [{ branch: 'steps', index: 0 }, { branch: 'steps', index: 1 }])?.ms).toBe(2);
    expect(stepAt(steps, [{ branch: 'steps', index: 0 }, { branch: 'else', index: 0 }])?.ms).toBe(3);
    expect(stepAt(steps, [{ branch: 'steps', index: 9 }])).toBeUndefined();
    expect(samePath([{ branch: 'steps', index: 1 }], [{ branch: 'steps', index: 1 }])).toBe(true);
    expect(samePath([{ branch: 'steps', index: 1 }], [])).toBe(false);
  });

  it('origin 和引擎一样写 .e', () => {
    const steps = flow();
    expect(originOf(steps, [{ branch: 'steps', index: 0 }])).toBe('1');
    expect(originOf(steps, [{ branch: 'steps', index: 0 }, { branch: 'steps', index: 1 }])).toBe('1.2');
    expect(originOf(steps, [{ branch: 'steps', index: 0 }, { branch: 'else', index: 0 }])).toBe('1.e1');
  });
});

describe('tree edits', () => {
  it('替换嵌套步骤不碰兄弟和顶层', () => {
    const steps = flow();
    const path: NodePath = [{ branch: 'steps', index: 0 }, { branch: 'steps', index: 0 }];
    const next = replaceAt(steps, path, { type: 'sleep', ms: 99 });
    expect(stepAt(next, path)?.ms).toBe(99);
    expect(stepAt(next, [{ branch: 'steps', index: 0 }, { branch: 'steps', index: 1 }])?.ms).toBe(2);
    expect(stepAt(steps, path)?.ms).toBe(1);
  });

  it('删掉否则最后一步时连 else 键一起收掉', () => {
    const next = removeAt(flow(), [{ branch: 'steps', index: 0 }, { branch: 'else', index: 0 }]);
    expect(next[0].else).toBeUndefined();
    expect(next[0].steps).toHaveLength(2);
  });

  it('往空分支追加、在中间插入都只改那一条分支', () => {
    const source: FlowStep = { type: 'if', cond: 'true', steps: [{ type: 'sleep', ms: 1 }] };
    const appended = appendToBranch([source], [{ branch: 'steps', index: 0 }], 'else', { type: 'sleep', ms: 2 });
    expect(appended[0].else).toEqual([{ type: 'sleep', ms: 2 }]);
    const inserted = insertAt(flow(), [{ branch: 'steps', index: 0 }], 'steps', 1, { type: 'sleep', ms: 7 });
    expect(inserted[0].steps?.map((step) => step.ms)).toEqual([1, 7, 2]);
    expect(sequenceOf(inserted, [{ branch: 'steps', index: 0 }], 'steps')).toHaveLength(3);
  });

  it('复制是深拷贝，嵌套块也跟着复制', () => {
    const next = duplicateAt(flow(), [{ branch: 'steps', index: 0 }]);
    expect(next).toHaveLength(3);
    expect(next[0]).not.toBe(next[1]);
    expect(next[1].else?.[0].ms).toBe(3);
  });

  it('同分支内换位，跨分支不偷偷丢步骤', () => {
    const steps: FlowStep[] = [{ type: 'sleep', ms: 1 }, { type: 'sleep', ms: 2 }, { type: 'sleep', ms: 3 }];
    const moved = moveAt(steps, [{ branch: 'steps', index: 0 }], [], 'steps', 2);
    expect(moved.map((step) => step.ms)).toEqual([2, 1, 3]);
    const across = moveAt(flow(), [{ branch: 'steps', index: 0 }], [{ branch: 'steps', index: 0 }], 'steps', 0);
    expect(across).toEqual(flow());
  });

  it('allNodes 先父后子、先成立时后否则', () => {
    const nodes = allNodes(flow());
    expect(nodes.map((node) => node.step.type)).toEqual(['if', 'sleep', 'sleep', 'sleep', 'sleep']);
    expect(nodes.map((node) => node.depth)).toEqual([0, 1, 1, 1, 0]);
  });
});

describe('copyNodes and insertManyAt', () => {
  const base = (): FlowStep[] => [
    { type: 'sleep', ms: 1 },
    { type: 'if', cond: 'true', steps: [{ type: 'sleep', ms: 2 }] },
    { type: 'sleep', ms: 3 },
  ];

  it('复制按文档顺序，父与子同时选中时只复制父', () => {
    const copies = copyNodes(base(), [
      [{ branch: 'steps', index: 2 }],
      [{ branch: 'steps', index: 1 }, { branch: 'steps', index: 0 }],
      [{ branch: 'steps', index: 1 }],
    ]);
    expect(copies.map((step) => step.type)).toEqual(['if', 'sleep']);
    expect(copies[0].steps?.length).toBe(1);
  });

  it('复制出来的是独立对象，连续粘贴不会共享', () => {
    const copies = copyNodes(base(), [[{ branch: 'steps', index: 1 }]]);
    (copies[0] as FlowStep).cond = 'false';
    const next = insertManyAt(base(), [], 'steps', 1, copies);
    (next[1] as FlowStep).cond = 'true';
    expect(copies[0].cond).toBe('false');
  });

  it('插入一组后返回的新流程保持顺序', () => {
    const next = insertManyAt(base(), [], 'steps', 3, copyNodes(base(), [
      [{ branch: 'steps', index: 0 }],
      [{ branch: 'steps', index: 2 }],
    ]));
    expect(next.map((step) => step.type)).toEqual(['sleep', 'if', 'sleep', 'sleep', 'sleep']);
  });
});

describe('moveNodes across parents', () => {
  const source = (): FlowStep[] => [
    { type: 'sleep', ms: 1 },
    { type: 'if', cond: 'true', steps: [{ type: 'sleep', ms: 10 }] },
    { type: 'sleep', ms: 2 },
    { type: 'sleep', ms: 3 },
  ];

  it('一级操作可以放到二级操作后面', () => {
    const result = moveNodes(source(), [[{ branch: 'steps', index: 3 }]], [{ branch: 'steps', index: 1 }], 'steps', 1);
    expect(result.steps.map((step) => step.type)).toEqual(['sleep', 'if', 'sleep']);
    expect(result.steps[1].steps?.map((step) => step.ms)).toEqual([10, 3]);
    expect(result.moved).toEqual([[{ branch: 'steps', index: 1 }, { branch: 'steps', index: 1 }]]);
  });

  it('二级操作可以搬到一级操作后面', () => {
    const steps = source().slice(0, 3);
    const result = moveNodes(steps, [[{ branch: 'steps', index: 1 }, { branch: 'steps', index: 0 }]], [], 'steps', 2);
    expect(result.steps.map((step) => step.type)).toEqual(['sleep', 'if', 'sleep', 'sleep']);
    expect(result.steps[2].ms).toBe(10);
    expect(result.steps[1].steps).toBeUndefined();
    expect(result.moved).toEqual([[{ branch: 'steps', index: 2 }]]);
  });

  it('多个节点按文档顺序一起搬，目标父节点的路径也会重算', () => {
    const result = moveNodes(
      source(),
      [[{ branch: 'steps', index: 0 }], [{ branch: 'steps', index: 2 }]],
      [{ branch: 'steps', index: 1 }],
      'steps',
      1,
    );
    expect(result.steps.map((step) => step.type)).toEqual(['if', 'sleep']);
    expect(result.steps[0].steps?.map((step) => step.ms)).toEqual([10, 1, 2]);
    expect(result.moved).toEqual([
      [{ branch: 'steps', index: 0 }, { branch: 'steps', index: 1 }],
      [{ branch: 'steps', index: 0 }, { branch: 'steps', index: 2 }],
    ]);
  });

  it('不能把节点放进自己的子树', () => {
    const steps = source();
    const moved = moveNodes(steps, [[{ branch: 'steps', index: 1 }]], [{ branch: 'steps', index: 1 }], 'steps', 1);
    expect(moved.moved).toEqual([]);
    expect(moved.steps).toEqual(steps);
  });

  it('同时选父与子时只搬父，避免重复', () => {
    const result = moveNodes(
      source(),
      [[{ branch: 'steps', index: 1 }], [{ branch: 'steps', index: 1 }, { branch: 'steps', index: 0 }]],
      [],
      'steps',
      3,
    );
    expect(result.steps.map((step) => step.type)).toEqual(['sleep', 'sleep', 'if', 'sleep']);
    expect(result.moved).toHaveLength(1);
  });

  it('isPathPrefix 只认完整路径前缀', () => {
    expect(isPathPrefix([{ branch: 'steps', index: 1 }], [{ branch: 'steps', index: 1 }, { branch: 'steps', index: 0 }])).toBe(true);
    expect(isPathPrefix([{ branch: 'steps', index: 1 }], [{ branch: 'steps', index: 10 }])).toBe(false);
  });
});

describe('arbitrary-depth moves', () => {
  const three = (): FlowStep[] => [
    {
      type: 'while',
      cond: 'true',
      steps: [
        {
          type: 'if',
          cond: 'true',
          steps: [{ type: 'sleep', ms: 1 }],
        },
        { type: 'sleep', ms: 3 },
      ],
    },
    { type: 'sleep', ms: 4 },
  ];

  it('三级可以搬到一级后面', () => {
    const result = moveNodes(three(), [[{ branch: 'steps', index: 0 }, { branch: 'steps', index: 0 }, { branch: 'steps', index: 0 }]], [], 'steps', 2);
    expect(result.steps.map((step) => step.type)).toEqual(['while', 'sleep', 'sleep']);
    expect(result.steps[2].ms).toBe(1);
    expect(result.moved).toEqual([[{ branch: 'steps', index: 2 }]]);
  });

  it('二级可以搬进三级分支并排到三级后面', () => {
    const result = moveNodes(
      three(),
      [[{ branch: 'steps', index: 0 }, { branch: 'steps', index: 1 }]],
      [{ branch: 'steps', index: 0 }, { branch: 'steps', index: 0 }],
      'steps',
      1,
    );
    expect(result.steps[0].steps?.map((step) => step.type)).toEqual(['if']);
    expect(result.steps[0].steps?.[0].steps?.map((step) => step.ms)).toEqual([1, 3]);
  });

  it('三级可以搬到二级后面，变成二级同级', () => {
    const result = moveNodes(
      three(),
      [[{ branch: 'steps', index: 0 }, { branch: 'steps', index: 0 }, { branch: 'steps', index: 0 }]],
      [{ branch: 'steps', index: 0 }],
      'steps',
      1,
    );
    expect(result.steps[0].steps?.map((step) => step.type)).toEqual(['if', 'sleep', 'sleep']);
    expect(result.steps[0].steps?.[1].ms).toBe(1);
  });

  it('不能把一级块搬到它自己的子树里', () => {
    const steps = three();
    const result = moveNodes(steps, [[{ branch: 'steps', index: 0 }]], [{ branch: 'steps', index: 0 }], 'steps', 1);
    expect(result.moved).toEqual([]);
    expect(result.steps).toEqual(steps);
  });
});
