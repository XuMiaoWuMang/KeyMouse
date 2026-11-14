import { describe, expect, it } from 'vitest';

import {
  angleFromVertical,
  autoPorts,
  nearestPort,
  routeEdge,
  type Point,
  type RoutingRect,
} from './routing';

const box = (x: number, y: number, w = 100, h = 50): RoutingRect => ({ x, y, w, h });

function horizontalOrVertical(points: readonly Point[]): boolean {
  return points.every((point, index) => {
    if (index === 0) return true;
    const prev = points[index - 1];
    return Math.abs(point.x - prev.x) < 0.01 || Math.abs(point.y - prev.y) < 0.01;
  });
}

describe('angleFromVertical / autoPorts', () => {
  it('目标在正下方：0°，使用下边进上边', () => {
    const from = box(0, 0);
    const to = box(0, 200);
    expect(angleFromVertical(from, to)).toBeCloseTo(0);
    expect(autoPorts(from, to)).toEqual({
      from: { side: 'bottom', t: 0.5 },
      to: { side: 'top', t: 0.5 },
    });
  });

  it('目标在右侧：90°，使用右边进左边', () => {
    const from = box(0, 0);
    const to = box(200, 0);
    expect(angleFromVertical(from, to)).toBeCloseTo(90);
    expect(autoPorts(from, to)).toEqual({
      from: { side: 'right', t: 0.5 },
      to: { side: 'left', t: 0.5 },
    });
  });

  it('偏竖直的斜线（<45°）仍走上下边', () => {
    const from = box(0, 0);
    const to = box(50, 200);
    expect(angleFromVertical(from, to)).toBeLessThan(45);
    expect(autoPorts(from, to).from.side).toBe('bottom');
    expect(autoPorts(from, to).to.side).toBe('top');
  });

  it('45° 归左右边，超过 45° 也走左右边', () => {
    const diagonal = box(200, 200);
    expect(angleFromVertical(box(0, 0), diagonal)).toBeCloseTo(45);
    expect(autoPorts(box(0, 0), diagonal).from.side).toBe('right');
    expect(autoPorts(box(0, 0), box(100, 50)).from.side).toBe('right');
  });

  it('目标在上方时上下边方向相反', () => {
    const from = box(0, 200);
    const to = box(0, 0);
    expect(autoPorts(from, to)).toEqual({
      from: { side: 'top', t: 0.5 },
      to: { side: 'bottom', t: 0.5 },
    });
  });
});

describe('routeEdge', () => {
  it('目标在下方：下边中点到上边中点，路径是直角折线', () => {
    const route = routeEdge(box(0, 0), box(0, 200));
    expect(route.start).toEqual({ x: 50, y: 50 });
    expect(route.end).toEqual({ x: 50, y: 200 });
    expect(route.points[0]).toEqual(route.start);
    expect(route.points[route.points.length - 1]).toEqual(route.end);
    expect(route.d).toContain('L');
    expect(route.d).not.toContain('C');
    expect(horizontalOrVertical(route.points)).toBe(true);
    expect(Number.isFinite(route.labelX)).toBe(true);
    expect(Number.isFinite(route.labelY)).toBe(true);
  });

  it('目标在右侧：从右边出，进左边', () => {
    const route = routeEdge(box(0, 0), box(200, 0));
    expect(route.start).toEqual({ x: 100, y: 25 });
    expect(route.end).toEqual({ x: 200, y: 25 });
    expect(route.d).toContain('L');
    expect(route.d).not.toContain('C');
  });

  it('斜向目标按角度走上下边，并在中间转弯', () => {
    const route = routeEdge(box(0, 0), box(80, 200));
    expect(route.start).toEqual({ x: 50, y: 50 });
    expect(route.end).toEqual({ x: 130, y: 200 });
    expect(route.points.length).toBeGreaterThan(2);
    expect(horizontalOrVertical(route.points)).toBe(true);
  });

  it('用户固定起点和终点后，路径从指定边出发并落到指定边', () => {
    const route = routeEdge(box(0, 0), box(200, 0), {
      fromPort: { side: 'left', t: 0.25 },
      toPort: { side: 'right', t: 0.75 },
    });
    expect(route.start).toEqual({ x: 0, y: 12.5 });
    expect(route.end).toEqual({ x: 300, y: 37.5 });
    expect(route.fromPort).toEqual({ side: 'left', t: 0.25 });
    expect(route.toPort).toEqual({ side: 'right', t: 0.75 });
  });

  it('保留两端引线：固定左边出、右边进时不会先折回图形内部', () => {
    const from = box(0, 0);
    const to = box(200, 0);
    const route = routeEdge(from, to, {
      fromPort: { side: 'left', t: 0.5 },
      toPort: { side: 'right', t: 0.5 },
      obstacles: [from, to],
    });
    expect(route.points[1].x).toBeLessThan(0);
    expect(route.points[route.points.length - 2].x).toBeGreaterThan(300);
    expect(horizontalOrVertical(route.points)).toBe(true);
  });

  it('把起止节点也当作障碍时，路径不会从自己身上穿过去', () => {
    const from = box(0, 0);
    const to = box(0, 200);
    const route = routeEdge(from, to, { obstacles: [from, to] });
    expect(horizontalOrVertical(route.points)).toBe(true);
    for (let index = 2; index < route.points.length - 1; index += 1) {
      const a = route.points[index - 1];
      const b = route.points[index];
      const crossesFrom = Math.min(a.x, b.x) < 100 && Math.max(a.x, b.x) > 0 &&
        Math.min(a.y, b.y) < 50 && Math.max(a.y, b.y) > 0;
      const crossesTo = Math.min(a.x, b.x) < 100 && Math.max(a.x, b.x) > 0 &&
        Math.min(a.y, b.y) < 250 && Math.max(a.y, b.y) > 200;
      expect(crossesFrom || crossesTo).toBe(false);
    }
  });

  it('只覆盖一端时，另一端继续按角度自动选择', () => {
    const route = routeEdge(box(0, 0), box(0, 200), {
      fromPort: { side: 'right', t: 0.5 },
    });
    expect(route.start).toEqual({ x: 100, y: 25 });
    expect(route.end).toEqual({ x: 50, y: 200 });
  });

  it('中间有节点挡住时，自动绕开而不是从卡片里穿过去', () => {
    const from = box(0, 0);
    const to = box(0, 300);
    const obstacle = box(0, 150);
    const route = routeEdge(from, to, { obstacles: [obstacle] });
    expect(horizontalOrVertical(route.points)).toBe(true);
    expect(route.points.some((point) => point.x < 0 || point.x > 100)).toBe(true);
    for (let index = 1; index < route.points.length; index += 1) {
      const a = route.points[index - 1];
      const b = route.points[index];
      const inside = (Math.min(a.x, b.x) < 100 && Math.max(a.x, b.x) > 0) &&
        (Math.min(a.y, b.y) < 200 && Math.max(a.y, b.y) > 150);
      expect(inside).toBe(false);
    }
  });

  it('标签落在折线总长一半处', () => {
    const route = routeEdge(box(0, 0), box(0, 200));
    expect(route.labelX).toBeCloseTo(50);
    expect(route.labelY).toBeGreaterThanOrEqual(50);
    expect(route.labelY).toBeLessThanOrEqual(200);
  });
});

describe('nearestPort', () => {
  it('把点投影到最近的边并保留边缘比例', () => {
    expect(nearestPort(box(0, 0), 10, -5)).toEqual({ side: 'top', t: 0.1 });
    expect(nearestPort(box(0, 0), 120, 30)).toEqual({ side: 'right', t: 0.6 });
  });

  it('靠近尖角时收在安全边距内', () => {
    expect(nearestPort(box(0, 0), -5, 0).t).toBe(0.08);
    expect(nearestPort(box(0, 0), 105, 50).t).toBe(0.92);
  });
});
