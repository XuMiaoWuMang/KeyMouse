import { describe, expect, it } from 'vitest';
import {
  displayNumbers,
  foldHeads,
  foldMoves,
  foldRuns,
  isFoldHead,
  isFoldMember,
  moveRunSummary,
  runKind,
  runSummary,
  wheelRunSummary,
} from './fold';

const move = (x: number, y: number) => ({ type: 'move', at: { x, y } });
const wheel = (delta: number, x = 350, y = 470) => ({ type: 'wheel', at: { x, y }, delta });
const sleep = (ms: number) => ({ type: 'sleep', ms });

describe('foldMoves', () => {
  it('两次移动不折：折起来比摊开更难读', () => {
    const runs = foldMoves([move(1, 1), move(2, 2)]);
    expect(runs.every((run) => run.head === -1)).toBe(true);
  });

  it('三次连续移动折成一组，组头是第一步', () => {
    const runs = foldMoves([move(1, 1), move(2, 2), move(3, 3)]);
    expect(runs[0]).toEqual({ kind: 'move', head: 0, offset: 0, count: 3 });
    expect(runs[1]).toEqual({ kind: 'move', head: 0, offset: 1, count: 3 });
    expect(runs[2]).toEqual({ kind: 'move', head: 0, offset: 2, count: 3 });
    expect(isFoldHead(runs, 0)).toBe(true);
    expect(isFoldMember(runs, 1)).toBe(true);
    expect(runKind(runs, 0)).toBe('move');
  });

  it('中间夹了别的步骤就断成两组', () => {
    const steps = [move(1, 1), move(2, 2), move(3, 3), sleep(100), move(4, 4), move(5, 5), move(6, 6)];
    const runs = foldMoves(steps);
    expect(foldHeads(runs)).toEqual([0, 4]);
    expect(runs[3].head).toBe(-1);
    expect(runs[6].count).toBe(3);
  });

  it('末尾那一段也照折', () => {
    const runs = foldMoves([sleep(10), move(1, 1), move(2, 2), move(3, 3)]);
    expect(runs[1]).toEqual({ kind: 'move', head: 1, offset: 0, count: 3 });
  });

  it('只有 move 的文档折成一组', () => {
    const runs = foldMoves([move(1, 1), move(2, 2), move(3, 3), move(4, 4)]);
    expect(runs[0].count).toBe(4);
    expect(foldHeads(runs)).toEqual([0]);
  });
});

describe('foldRuns 滚轮', () => {
  it('连着滚三下折成一组，组头是第一次滚动', () => {
    const runs = foldRuns([wheel(240), wheel(120), wheel(600)]);
    expect(runs[0]).toEqual({ kind: 'wheel', head: 0, offset: 0, count: 3 });
    expect(isFoldHead(runs, 0)).toBe(true);
    expect(runKind(runs, 0)).toBe('wheel');
  });

  it('两下不折：只翻了两屏，摊开更好读', () => {
    const runs = foldRuns([wheel(120), wheel(-120)]);
    expect(runs.every((run) => run.head === -1)).toBe(true);
  });

  it('滚轮和移动挨着也不混成一组', () => {
    const steps = [move(1, 1), move(2, 2), move(3, 3), wheel(120), wheel(120), wheel(120)];
    const runs = foldRuns(steps);
    expect(foldHeads(runs)).toEqual([0, 3]);
    expect(runKind(runs, 0)).toBe('move');
    expect(runKind(runs, 3)).toBe('wheel');
  });

  it('滚轮组在编号上同样只占一个号', () => {
    const steps = [sleep(10), wheel(120), wheel(120), wheel(120), sleep(20)];
    expect(displayNumbers(foldRuns(steps))).toEqual([1, 2, 2, 2, 3]);
  });
});

describe('displayNumbers', () => {
  it('折起来的一组占一个号，后面的行往上挪', () => {
    const steps = [move(1, 1), move(2, 2), move(3, 3), sleep(100), { type: 'type', text: 'hi' }];
    const numbers = displayNumbers(foldMoves(steps));
    // 第 1 行是折叠题头；sleep 是第 2 行；type 是第 3 行——不是数组下标 +1 的 4、5。
    expect(numbers).toEqual([1, 1, 1, 2, 3]);
  });

  it('没有折叠时就是 1..n', () => {
    expect(displayNumbers(foldMoves([sleep(1), sleep(2)]))).toEqual([1, 2]);
  });
});

describe('moveRunSummary', () => {
  it('起点与终点都取自真实步骤', () => {
    const steps = [move(120, 64), move(300, 200), move(840, 360)];
    expect(moveRunSummary(steps, 0, 3)).toBe('120,64 → 840,360');
  });

  it('缺坐标的那一步写成问号，别装成有坐标', () => {
    const steps = [move(1, 1), move(3, 3), { type: 'move' }];
    expect(moveRunSummary(steps, 0, 3)).toBe('1,1 → ?');
  });
});

describe('wheelRunSummary', () => {
  it('位置加位移合计：正数往下，写个 +', () => {
    const steps = [wheel(240), wheel(120), wheel(600)];
    expect(wheelRunSummary(steps, 0, 3)).toBe('350,470 · 共 +960');
  });

  it('往上滚是负数，不加 +', () => {
    expect(wheelRunSummary([wheel(-120), wheel(-120), wheel(-120)], 0, 3)).toBe('350,470 · 共 -360');
  });

  it('有一步没写 delta 就写问号，别把半成品藏起来', () => {
    const steps = [wheel(120), { type: 'wheel', at: { x: 350, y: 470 } }, wheel(120)];
    expect(wheelRunSummary(steps, 0, 3)).toBe('350,470 · ?');
  });
});

describe('runSummary', () => {
  it('按组头那一步的类型分派', () => {
    expect(runSummary([move(1, 1), move(2, 2), move(3, 3)], 0, 3)).toBe('1,1 → 3,3');
    expect(runSummary([wheel(120), wheel(120), wheel(120)], 0, 3)).toBe('350,470 · 共 +360');
  });
});
