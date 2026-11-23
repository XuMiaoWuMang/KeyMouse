import { describe, expect, it } from 'vitest';
import { layoutFlow } from './canvas';
import {
  MIN_RUN,
  buildView,
  realPathOf,
  remapLayout,
  rollupRunResult,
  runAtHead,
  runOriginLabel,
  runSelection,
  runSummary,
} from './runs';
import type { FlowStep } from './model';
import { pathKey, type NodePath } from './tree';

const move = (x: number, y: number): FlowStep => ({ type: 'move', at: { space: 'screen', x, y } });
const wheel = (delta: number): FlowStep => ({ type: 'wheel', at: { space: 'screen', x: 350, y: 470 }, delta });
const sleep = (ms: number): FlowStep => ({ type: 'sleep', ms });
const click = (): FlowStep => ({ type: 'click', at: { space: 'screen', x: 12, y: 24 } });

const top = (index: number): NodePath => [{ branch: 'steps', index }];
const child = (parent: number, index: number): NodePath => [
  { branch: 'steps', index: parent },
  { branch: 'steps', index },
];

describe('buildView', () => {
  it('三次以上相邻移动并成一个节点，前后的普通步骤不动', () => {
    const steps = [click(), move(1, 1), move(2, 2), move(3, 3), click()];
    const view = buildView(steps);
    expect(view.steps).toHaveLength(3);
    const run = view.runs.get(pathKey(top(1)));
    expect(run?.paths).toHaveLength(3);
    expect(view.items.get(pathKey(top(1)))?.path).toEqual(top(1));
    // 视图里第三张卡是最后那次点击，真实下标应为 4。
    expect(view.items.get(pathKey(top(2)))?.path).toEqual(top(4));
  });

  it('两步移动不合并：折起来比摊开更难读', () => {
    const steps = [move(1, 1), move(2, 2)];
    const view = buildView(steps);
    expect(view.runs.size).toBe(0);
    expect(view.steps).toHaveLength(2);
  });

  it('移动与滚轮混着也能并成一段，摘要分类型统计', () => {
    const steps = [move(1, 1), wheel(-120), move(2, 2), wheel(-240)];
    const view = buildView(steps);
    const run = view.runs.get(pathKey(top(0)));
    expect(view.steps).toHaveLength(1);
    expect(run).toBeDefined();
    if (!run) return;
    expect(run.moves).toBe(2);
    expect(run.wheels).toBe(2);
    expect(runSummary(run)).toBe('移动 2 · 滚轮 2 · 共 4 步');
    expect(runOriginLabel(run)).toBe('1–4');
  });

  it('中间夹了别的步骤就断成两段，只有够长的那段合并', () => {
    const steps = [move(1, 1), move(2, 2), sleep(100), move(3, 3), move(4, 4), move(5, 5)];
    const view = buildView(steps);
    expect(view.steps).toHaveLength(4);
    expect(view.runs.size).toBe(1);
    expect([...view.runs.keys()]).toEqual([pathKey(top(3))]);
  });

  it('块里的分支也按同样规则合并，块本身是浅拷贝', () => {
    const body = [move(1, 1), move(2, 2), move(3, 3)];
    const branch: FlowStep = { type: 'if', cond: 'x > 0', steps: body };
    const steps = [branch, click()];
    const view = buildView(steps);
    expect(view.steps).toHaveLength(2);
    const cloned = view.steps[0];
    expect(cloned).not.toBe(branch);
    expect(cloned.steps).toHaveLength(1);
    // 原流程一个字节都没改：合并只是视图。
    expect(branch.steps).toHaveLength(3);
    expect(view.runs.get(pathKey(child(0, 0)))?.paths).toHaveLength(3);
  });

  it('展开的组不合并，但组头仍然带着这一组，用来画「收起」', () => {
    const steps = [move(1, 1), move(2, 2), move(3, 3), click()];
    const head = top(0);
    const view = buildView(steps, [pathKey(head)]);
    expect(view.steps).toHaveLength(4);
    expect(view.runs.has(pathKey(head))).toBe(true);
    expect(view.items.get(pathKey(head))?.run?.paths).toHaveLength(3);
  });

  it('少于 MIN_RUN 的段永远不合并', () => {
    const steps = Array.from({ length: MIN_RUN - 1 }, (_, i) => move(i, i));
    expect(buildView(steps).runs.size).toBe(0);
  });
});

describe('remapLayout', () => {
  it('节点、空槽与落点缝都换回真实路径和下标', () => {
    const steps = [click(), move(1, 1), move(2, 2), move(3, 3), click()];
    const view = buildView(steps);
    const layout = remapLayout(layoutFlow(view.steps), view);
    const paths = layout.nodes.map((node) => node.path);
    expect(paths).toEqual([top(0), top(1), top(4)]);
    expect(layout.nodes.map((node) => node.origin)).toEqual(['1', '2', '5']);
    // 第二张卡后面的缝要落在整段之后，插进去才不会把组劈开。
    const afterRun = layout.gaps.find(
      (gap) => gap.parentPath.length === 0 && gap.branch === 'steps' && gap.index === 4,
    );
    expect(afterRun).toBeDefined();
  });

  it('视图路径换回真实路径，顶层原样返回', () => {
    const steps = [move(1, 1), move(2, 2), move(3, 3)];
    const view = buildView(steps);
    expect(realPathOf(view, top(0))).toEqual(top(0));
    expect(realPathOf(view, [])).toEqual([]);
  });
});

describe('runAtHead / runSelection', () => {
  const steps = [click(), move(1, 1), move(2, 2), move(3, 3), click()];

  it('组头返回整组；组员、太短的段、非鼠标步骤都返回 null', () => {
    const run = runAtHead(steps, top(1));
    expect(run?.paths).toEqual([top(1), top(2), top(3)]);
    expect(runAtHead(steps, top(2))).toBeNull();
    expect(runAtHead(steps, top(0))).toBeNull();
    expect(runAtHead(steps, top(4))).toBeNull();
  });

  it('选中集合恰好是一整组时认出来，混进别的节点就不算', () => {
    const run = runAtHead(steps, top(1));
    expect(run).not.toBeNull();
    if (!run) return;
    expect(runSelection(steps, run.paths)?.head).toEqual(top(1));
    expect(runSelection(steps, [...run.paths, top(4)])).toBeNull();
    expect(runSelection(steps, run.paths.slice(0, 2))).toBeNull();
  });
});

describe('rollupRunResult', () => {
  const steps = [move(1, 1), move(2, 2), move(3, 3)];
  const run = buildView(steps).runs.get(pathKey(top(0)));
  if (!run) throw new Error('fixture 应该合并成一组');

  it('全部成功时合成一句，而不是让三行字叠在卡片里', () => {
    const results = {
      '1': { ok: true, text: '移到 1,1' },
      '2': { ok: true, text: '移到 2,2' },
      '3': { ok: true, text: '移到 3,3' },
    };
    expect(rollupRunResult(run, steps, results)).toEqual({
      ok: true,
      text: '3 步都成功',
      dry: false,
    });
  });

  it('有失败就报第一个失败，不把失败藏进合并卡片', () => {
    const results = {
      '1': { ok: true, text: '移到 1,1' },
      '2': { ok: false, text: '窗口没了' },
      '3': { ok: true, text: '移到 3,3' },
    };
    expect(rollupRunResult(run, steps, results)).toEqual({ ok: false, text: '窗口没了' });
  });

  it('一步结果都没有时返回 null，卡片上不显示结果标签', () => {
    expect(rollupRunResult(run, steps, {})).toBeNull();
  });
});
