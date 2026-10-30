// 按点号路径读写步骤里的字段：`at.x`、`region.width`、`when.text`。
//
// 界面是按契约渲染出来的，字段名来自 JSON，所以只能走路径读写，不能一个个写 setter。
// 三条规矩：写空值等于删掉这个键（流程文件里"没写"和"写空"不是一回事）；一路建对象；
// 返回新对象，不原地改（撤销栈是序列化快照，原地改会让快照跟着变）。

export type Json = string | number | boolean | null | Json[] | { [key: string]: Json };
export type Path = string;

const MISSING = Symbol('missing');

function parts(path: Path): string[] {
  return path.split('.').filter((p) => p.length > 0);
}

export function readPath(target: unknown, path: Path): unknown {
  let node: unknown = target;
  for (const key of parts(path)) {
    if (node === null || typeof node !== 'object') return undefined;
    node = (node as Record<string, unknown>)[key];
  }
  return node;
}

function isEmpty(value: unknown): boolean {
  return value === undefined || value === null || value === '';
}

function cloneWith(node: unknown, key: string, value: unknown): Record<string, unknown> {
  const base = node !== null && typeof node === 'object' && !Array.isArray(node) ? { ...(node as Record<string, unknown>) } : {};
  if (isEmpty(value)) delete base[key];
  else base[key] = value;
  return base;
}

/** 写一个值；value 为空（undefined/null/空串）时删掉这个键。返回新的根对象。
 *
 *  参数类型写成 `object` 而不是 `Record<string, unknown>`：调用方手里是 `FlowStep`
 *  这类具体接口，接口没有索引签名，套上 Record 反而不匹配；进来之后一律当字典用。 */
export function writePath(target: object, path: Path, value: unknown): Record<string, unknown> {
  const keys = parts(path);
  if (keys.length === 0) return target as Record<string, unknown>;
  const [head, ...rest] = keys;
  if (rest.length === 0) return cloneWith(target, head, value);

  const child = (target as Record<string, unknown>)[head];
  const nextChild = writePath((child ?? {}) as Record<string, unknown>, rest.join('.'), value);
  // 删空的嵌套对象：写完发现整棵空了，就连父键一起去掉，免得留下 `"when": {}` 这种半成品。
  const dead = Object.keys(nextChild).length === 0;
  return cloneWith(target, head, dead ? undefined : nextChild);
}

/** 便捷读取：数字字段读不到时给默认值，控件不至于因为一个空值变成 NaN。 */
export function readInt(target: unknown, path: Path, fallback: number | null = null): number | null {
  const value = readPath(target, path);
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string' && value.trim() !== '' && Number.isFinite(Number(value))) return Number(value);
  return fallback;
}

export function readText(target: unknown, path: Path): string {
  const value = readPath(target, path);
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return '';
}

export function readBool(target: unknown, path: Path): boolean {
  return readPath(target, path) === true;
}

export { MISSING };
