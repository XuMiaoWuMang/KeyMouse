import { describe, expect, it } from 'vitest';
import { displayNumbers, foldHeads, foldMoves, isFoldHead, isFoldMember, moveRunSummary } from './fold';

const move = (x: number, y: number) => ({ type: 'move', at: { x, y } });
const sleep = (ms: number) => ({ type: 'sleep', ms });

describe('foldMoves', () => {
  it('两次移动不折：折起来比摊开更难读', () => {
    const runs = foldMoves([move(1, 1), move(2, 2)]);
    expect(runs.every((run) => run.head === -1)).toBe(true);
  });

  it('三次连续移动折成一组，组头是第一步', () => {
    const runs = foldMoves([move(1, 1), move(2, 2), move(3, 3)]);
    expect(runs[0]).toEqual({ head: 0, offset: 0, count: 3 });
    expect(runs[1]).toEqual({ head: 0, offset: 1, count: 3 });
    expect(runs[2]).toEqual({ head: 0, offset: 2, count: 3 });
    expect(isFoldHead(runs, 0)).toBe(true);
    expect(isFoldMember(runs, 1)).toBe(true);
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
    expect(runs[1]).toEqual({ head: 1, offset: 0, count: 3 });
  });

  it('只有 move 的文档折成一组', () => {
    const runs = foldMoves([move(1, 1), move(2, 2), move(3, 3), move(4, 4)]);
    expect(runs[0].count).toBe(4);
    expect(foldHeads(runs)).toEqual([0]);
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
