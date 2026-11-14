/**
 * 自由画布上的连线几何。
 *
 * 节点可以拖到任何地方，所以不能再用"上一张卡片的下边连下一张的上边"这种固定路由。
 * 这里做三件事：
 *   1. 按两个节点中心的连线与纵轴的夹角选接口：更接近竖直就走上下边，否则走左右边；
 *   2. 用横平竖直的折线（drawio 风格）连起来，并在绕开其它节点时保持直角；
 *   3. 允许用户把某一端固定到图形边缘的任意位置（side + t），其余情况自动选接口。
 *
 * 入口点位于矩形边上，路径第一段先沿边的法线离开一小段，最后一段再沿目标边的法线进入。
 * 这两小段既让箭头方向明确，也避免线贴着卡片边缘"擦"过去。
 */

import type { GraphEdgeKind } from './graph';

export type PortSide = 'top' | 'right' | 'bottom' | 'left';

/** 图形边缘上的一个位置：side 决定哪条边，t 是在这条边上的比例（0..1）。 */
export interface EdgePort {
  side: PortSide;
  t: number;
}

/** 一条边可以分别覆盖起点和终点接口；缺省时按角度自动选择。 */
export interface EdgePorts {
  from?: EdgePort;
  to?: EdgePort;
}

export interface Point {
  x: number;
  y: number;
}

export interface RoutingRect {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface RouteEdgeOptions {
  /** 目前只用来保留边的语义，后续如果需要按类型改标签/线型可以继续用。 */
  kind?: GraphEdgeKind;
  fromPort?: EdgePort | null;
  toPort?: EdgePort | null;
  /** 需要绕开的其它节点；起止节点会自动排除。 */
  obstacles?: readonly RoutingRect[];
}

export interface RoutedEdge {
  /** SVG path 的 `d`，只由水平和竖直线段组成。 */
  d: string;
  /** 标签落点；标签由 SVG 文本画，避免 HTML 层盖住节点。 */
  labelX: number;
  labelY: number;
  /** 从图形边缘的起点到终点的完整折线，用来画端点和做命中判断。 */
  points: Point[];
  start: Point;
  end: Point;
  fromPort: EdgePort;
  toPort: EdgePort;
}

const STUB = 18;
const CLEARANCE = 10;
const TURN_COST = 24;
const PORT_PAD = 0.08;

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function centerX(rect: RoutingRect): number {
  return rect.x + rect.w / 2;
}

function centerY(rect: RoutingRect): number {
  return rect.y + rect.h / 2;
}

function isVerticalSide(side: PortSide): boolean {
  return side === 'top' || side === 'bottom';
}

function normalOf(side: PortSide): Point {
  switch (side) {
    case 'top':
      return { x: 0, y: -1 };
    case 'right':
      return { x: 1, y: 0 };
    case 'bottom':
      return { x: 0, y: 1 };
    default:
      return { x: -1, y: 0 };
  }
}

export function isPortSide(value: unknown): value is PortSide {
  return value === 'top' || value === 'right' || value === 'bottom' || value === 'left';
}

/** 把外部读到的 t 收在安全范围内，避免箭头压在圆角/尖角上。 */
function normalizePort(port: EdgePort): EdgePort {
  return {
    side: port.side,
    t: clamp(Number.isFinite(port.t) ? port.t : 0.5, PORT_PAD, 1 - PORT_PAD),
  };
}

/** 取矩形某条边上的点。t=0 在左上角开始顺时针量。 */
export function portPoint(rect: RoutingRect, port: EdgePort): Point {
  const t = clamp(Number.isFinite(port.t) ? port.t : 0.5, 0, 1);
  switch (port.side) {
    case 'top':
      return { x: rect.x + rect.w * t, y: rect.y };
    case 'right':
      return { x: rect.x + rect.w, y: rect.y + rect.h * t };
    case 'bottom':
      return { x: rect.x + rect.w * t, y: rect.y + rect.h };
    default:
      return { x: rect.x, y: rect.y + rect.h * t };
  }
}

/**
 * 两个节点中心连线与纵轴的夹角，单位度。
 * 0° = 目标在正上/正下方；90° = 目标在正左/正右方。
 */
export function angleFromVertical(from: RoutingRect, to: RoutingRect): number {
  const dx = centerX(to) - centerX(from);
  const dy = centerY(to) - centerY(from);
  return (Math.atan2(Math.abs(dx), Math.abs(dy)) * 180) / Math.PI;
}

/**
 * 用户的角度规则：与纵轴夹角小于 45° 走上下边，否则走左右边。
 * 45° 本身归到左右边，和 create 时的判断一致。
 */
export function autoPorts(from: RoutingRect, to: RoutingRect): { from: EdgePort; to: EdgePort } {
  const vertical = angleFromVertical(from, to) < 45;
  const dx = centerX(to) - centerX(from);
  const dy = centerY(to) - centerY(from);

  if (vertical) {
    const below = dy >= 0;
    return {
      from: { side: below ? 'bottom' : 'top', t: 0.5 },
      to: { side: below ? 'top' : 'bottom', t: 0.5 },
    };
  }

  const right = dx >= 0;
  return {
    from: { side: right ? 'right' : 'left', t: 0.5 },
    to: { side: right ? 'left' : 'right', t: 0.5 },
  };
}

/**
 * 把鼠标位置投影到矩形最近的边，得到可拖拽的接口。
 * 这里留一点边距，避免用户把箭头塞到六边形/胶囊的尖角里。
 */
export function nearestPort(rect: RoutingRect, x: number, y: number): EdgePort {
  const edges: { side: PortSide; distance: number }[] = [
    { side: 'left', distance: Math.abs(x - rect.x) },
    { side: 'right', distance: Math.abs(x - (rect.x + rect.w)) },
    { side: 'top', distance: Math.abs(y - rect.y) },
    { side: 'bottom', distance: Math.abs(y - (rect.y + rect.h)) },
  ];
  const nearest = edges.reduce((best, item) => (item.distance < best.distance ? item : best));
  const t = isVerticalSide(nearest.side)
    ? (x - rect.x) / Math.max(rect.w, 1)
    : (y - rect.y) / Math.max(rect.h, 1);
  return normalizePort({ side: nearest.side, t });
}

function shift(point: Point, side: PortSide, distance: number): Point {
  const normal = normalOf(side);
  return { x: point.x + normal.x * distance, y: point.y + normal.y * distance };
}

function samePoint(a: Point, b: Point): boolean {
  return Math.abs(a.x - b.x) < 0.01 && Math.abs(a.y - b.y) < 0.01;
}

function unique(values: readonly number[]): number[] {
  const result: number[] = [];
  for (const value of [...values].sort((a, b) => a - b)) {
    if (result.length === 0 || Math.abs(result[result.length - 1] - value) > 0.01) result.push(value);
  }
  return result;
}

/** 去掉连续重复点和共线的中间点，让路径和标签都更干净。 */
function simplify(points: readonly Point[]): Point[] {
  const compact: Point[] = [];
  for (const point of points) {
    if (compact.length === 0 || !samePoint(compact[compact.length - 1], point)) compact.push(point);
  }
  if (compact.length < 3) return compact;

  const result: Point[] = [compact[0]];
  for (let index = 1; index < compact.length - 1; index += 1) {
    const prev = result[result.length - 1];
    const current = compact[index];
    const next = compact[index + 1];
    const collinear = (Math.abs(prev.x - current.x) < 0.01 && Math.abs(current.x - next.x) < 0.01) ||
      (Math.abs(prev.y - current.y) < 0.01 && Math.abs(current.y - next.y) < 0.01);
    if (!collinear) result.push(current);
  }
  result.push(compact[compact.length - 1]);
  return result;
}

/**
 * 线段是否穿过矩形（矩形先向外扩 CLEARANCE）。
 * 只处理水平/竖直线段；贴边不算穿过，这样路径可以走通道边缘。
 */
function segmentBlocked(a: Point, b: Point, rect: RoutingRect): boolean {
  const left = rect.x - CLEARANCE;
  const right = rect.x + rect.w + CLEARANCE;
  const top = rect.y - CLEARANCE;
  const bottom = rect.y + rect.h + CLEARANCE;

  if (Math.abs(a.y - b.y) < 0.01) {
    if (a.y <= top || a.y >= bottom) return false;
    const min = Math.min(a.x, b.x);
    const max = Math.max(a.x, b.x);
    return max > left && min < right;
  }
  if (Math.abs(a.x - b.x) < 0.01) {
    if (a.x <= left || a.x >= right) return false;
    const min = Math.min(a.y, b.y);
    const max = Math.max(a.y, b.y);
    return max > top && min < bottom;
  }
  return true;
}

function routeBlocked(points: readonly Point[], obstacles: readonly RoutingRect[]): boolean {
  for (let index = 1; index < points.length; index += 1) {
    const a = points[index - 1];
    const b = points[index];
    if (obstacles.some((rect) => segmentBlocked(a, b, rect))) return true;
  }
  return false;
}

/** 几个简单折线候选：先选最平衡的中间弯，再考虑贴边弯。 */
function candidateRoutes(start: Point, end: Point, fromSide: PortSide, toSide: PortSide): Point[][] {
  const routes: Point[][] = [];
  const push = (route: Point[]) => {
    const clean = simplify(route);
    if (clean.length >= 2) routes.push(clean);
  };

  if (isVerticalSide(fromSide) && isVerticalSide(toSide)) {
    const midY = (start.y + end.y) / 2;
    push([start, { x: start.x, y: midY }, { x: end.x, y: midY }, end]);
    if (Math.abs(start.x - end.x) < 0.01) push([start, end]);
    push([start, { x: start.x, y: end.y }, end]);
  } else if (!isVerticalSide(fromSide) && !isVerticalSide(toSide)) {
    const midX = (start.x + end.x) / 2;
    push([start, { x: midX, y: start.y }, { x: midX, y: end.y }, end]);
    if (Math.abs(start.y - end.y) < 0.01) push([start, end]);
    push([start, { x: end.x, y: start.y }, end]);
  } else if (isVerticalSide(fromSide)) {
    // 从上下边出发，先竖着走；到目标高度后再横着靠近目标。
    push([start, { x: start.x, y: end.y }, end]);
    push([start, { x: end.x, y: start.y }, end]);
  } else {
    // 从左右边出发，先横着走；到目标 x 后再竖着靠近目标。
    push([start, { x: end.x, y: start.y }, end]);
    push([start, { x: start.x, y: end.y }, end]);
  }
  return routes;
}

interface SearchState {
  xi: number;
  yi: number;
  dir: -1 | 0 | 1;
}

interface SearchRecord extends SearchState {
  g: number;
  parent: string | null;
  point: Point;
}

function stateKey(xi: number, yi: number, dir: number): string {
  return `${xi},${yi},${dir}`;
}

/**
 * 网格上的直角最短路：x/y 坐标取自端点与障碍物边界，避开被节点挡住的线段。
 * 只在简单候选全被挡住时启用；普通流程仍然走候选折线，拖动时不会每次都跑搜索。
 */
function shortestOrthogonal(start: Point, end: Point, obstacles: readonly RoutingRect[]): Point[] | null {
  if (samePoint(start, end)) return [start];

  const xs = unique([
    start.x,
    end.x,
    ...obstacles.flatMap((rect) => [rect.x - CLEARANCE, rect.x + rect.w + CLEARANCE]),
    Math.min(start.x, end.x, ...obstacles.map((rect) => rect.x)) - 48,
    Math.max(start.x, end.x, ...obstacles.map((rect) => rect.x + rect.w)) + 48,
  ]);
  const ys = unique([
    start.y,
    end.y,
    ...obstacles.flatMap((rect) => [rect.y - CLEARANCE, rect.y + rect.h + CLEARANCE]),
    Math.min(start.y, end.y, ...obstacles.map((rect) => rect.y)) - 48,
    Math.max(start.y, end.y, ...obstacles.map((rect) => rect.y + rect.h)) + 48,
  ]);

  const startXi = xs.indexOf(start.x);
  const startYi = ys.indexOf(start.y);
  const endXi = xs.indexOf(end.x);
  const endYi = ys.indexOf(end.y);
  if (startXi < 0 || startYi < 0 || endXi < 0 || endYi < 0) return null;

  const startKey = stateKey(startXi, startYi, -1);
  const records = new Map<string, SearchRecord>();
  const open: SearchState[] = [{ xi: startXi, yi: startYi, dir: -1 }];
  records.set(startKey, { xi: startXi, yi: startYi, dir: -1, g: 0, parent: null, point: start });

  const heuristic = (xi: number, yi: number): number =>
    Math.abs(xs[xi] - end.x) + Math.abs(ys[yi] - end.y);

  while (open.length > 0) {
    let bestIndex = 0;
    let bestScore = Infinity;
    for (let index = 0; index < open.length; index += 1) {
      const state = open[index];
      const record = records.get(stateKey(state.xi, state.yi, state.dir));
      if (!record) continue;
      const score = record.g + heuristic(state.xi, state.yi);
      if (score < bestScore) {
        bestScore = score;
        bestIndex = index;
      }
    }
    const current = open.splice(bestIndex, 1)[0];
    const currentKey = stateKey(current.xi, current.yi, current.dir);
    const currentRecord = records.get(currentKey);
    if (!currentRecord) continue;
    if (current.xi === endXi && current.yi === endYi) {
      const points: Point[] = [];
      let cursor: SearchRecord | undefined = currentRecord;
      while (cursor) {
        points.push(cursor.point);
        cursor = cursor.parent ? records.get(cursor.parent) : undefined;
      }
      return simplify(points.reverse());
    }

    const moves: { xi: number; yi: number; dir: 0 | 1 }[] = [];
    if (current.xi > 0) moves.push({ xi: current.xi - 1, yi: current.yi, dir: 0 });
    if (current.xi + 1 < xs.length) moves.push({ xi: current.xi + 1, yi: current.yi, dir: 0 });
    if (current.yi > 0) moves.push({ xi: current.xi, yi: current.yi - 1, dir: 1 });
    if (current.yi + 1 < ys.length) moves.push({ xi: current.xi, yi: current.yi + 1, dir: 1 });

    for (const move of moves) {
      const nextPoint = { x: xs[move.xi], y: ys[move.yi] };
      if (obstacles.some((rect) => segmentBlocked(currentRecord.point, nextPoint, rect))) continue;
      const turn = current.dir !== -1 && current.dir !== move.dir ? TURN_COST : 0;
      const nextG = currentRecord.g + Math.abs(nextPoint.x - currentRecord.point.x) +
        Math.abs(nextPoint.y - currentRecord.point.y) + turn;
      const nextKey = stateKey(move.xi, move.yi, move.dir);
      const known = records.get(nextKey);
      if (known && known.g <= nextG) continue;
      records.set(nextKey, {
        xi: move.xi,
        yi: move.yi,
        dir: move.dir,
        g: nextG,
        parent: currentKey,
        point: nextPoint,
      });
      open.push({ xi: move.xi, yi: move.yi, dir: move.dir });
    }
  }
  return null;
}

function pathData(points: readonly Point[]): string {
  return points.map((point, index) => {
    const x = Number(point.x.toFixed(2));
    const y = Number(point.y.toFixed(2));
    return `${index === 0 ? 'M' : 'L'} ${x} ${y}`;
  }).join(' ');
}

/** 取折线总长度一半处的点，让标签落在真正经过的线上。 */
function labelPoint(points: readonly Point[]): Point {
  let total = 0;
  for (let index = 1; index < points.length; index += 1) {
    total += Math.abs(points[index].x - points[index - 1].x) +
      Math.abs(points[index].y - points[index - 1].y);
  }
  if (total < 0.01) return points[0];

  let left = total / 2;
  for (let index = 1; index < points.length; index += 1) {
    const from = points[index - 1];
    const to = points[index];
    const length = Math.abs(to.x - from.x) + Math.abs(to.y - from.y);
    if (left <= length && length > 0.01) {
      const ratio = left / length;
      return {
        x: from.x + (to.x - from.x) * ratio,
        y: from.y + (to.y - from.y) * ratio,
      };
    }
    left -= length;
  }
  return points[points.length - 1];
}

/**
 * 路由一条边。
 *
 * 自动接口按中心连线的角度选；传了 `fromPort` / `toPort` 就尊重用户拖到的位置。
 * 路径先尝试几个规则的直角折线，全部被其它节点挡住时才走网格搜索。
 */
export function routeEdge(
  from: RoutingRect,
  to: RoutingRect,
  options: RouteEdgeOptions = {},
): RoutedEdge {
  const auto = autoPorts(from, to);
  const fromPort = normalizePort(options.fromPort ?? auto.from);
  const toPort = normalizePort(options.toPort ?? auto.to);
  const start = portPoint(from, fromPort);
  const end = portPoint(to, toPort);
  const startStub = shift(start, fromPort.side, STUB);
  const endStub = shift(end, toPort.side, STUB);
  const obstacles = options.obstacles ?? [];

  const candidates = candidateRoutes(startStub, endStub, fromPort.side, toPort.side);
  let core = candidates.find((route) => !routeBlocked(route, obstacles));
  if (!core) {
    core = shortestOrthogonal(startStub, endStub, obstacles) ?? candidates[0] ??
      simplify([startStub, endStub]);
  }

  // 不能对整条线做 simplify：它会把 startStub/endStub 当成共线中间点删掉，
  // 路径就会从图形边缘直接折回图形内部。两端的小引线必须保留。
  const points: Point[] = [start];
  for (const point of [...core, end]) {
    if (!samePoint(points[points.length - 1], point)) points.push(point);
  }
  const label = labelPoint(points);
  return {
    d: pathData(points),
    labelX: label.x,
    labelY: label.y,
    points,
    start,
    end,
    fromPort,
    toPort,
  };
}
