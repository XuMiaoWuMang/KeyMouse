/**
 * 给桌面日志 HUD 用的“一行输出 → 一条可读日志”。
 *
 * 命令行输出是给人看的，不是协议；这里只做保守的展示折衷：
 * - 带 `[x/y]` 的步骤行才进 HUD，初始化/汇总行不进；
 * - 流程输出里 `ok/skip/exit N` 是状态，命令才是内容；
 * - 脚本输出里第一个词本身就是步骤名（如“等待”），后面的才是内容；
 * - 看不清的行宁可丢掉，也不猜一个会误导人的步骤名。
 */

export type HudStatus = 'current' | 'done' | 'skipped' | 'failed' | 'cancelled';
export const HUD_EVENT_PREFIX = '@keymouse-hud/v1 ';
export const HUD_CHILD_ROWS = 3;

export type HudEvent = {
  seq: number;
  id: string;
  scopeId: string;
  kind: 'step' | 'call';
  origin: string;
  index: number;
  total: number | null;
  command: string;
  status: HudStatus;
  exitCode?: number | null;
};

export function parseHudEvent(line: string): HudEvent | null {
  if (!line.startsWith(HUD_EVENT_PREFIX)) return null;
  try {
    const e = JSON.parse(line.slice(HUD_EVENT_PREFIX.length));
    if (!e || !Number.isSafeInteger(e.seq) || e.seq < 1 ||
      typeof e.id !== 'string' || !e.id || e.id === 'root' || typeof e.scopeId !== 'string' || !e.scopeId ||
      e.id === e.scopeId || !['step', 'call'].includes(e.kind) ||
      typeof e.origin !== 'string' || typeof e.command !== 'string' ||
      !Number.isSafeInteger(e.index) || e.index < 1 ||
      !(e.total === null || (Number.isSafeInteger(e.total) && e.total >= e.index)) ||
      !['current', 'done', 'skipped', 'failed', 'cancelled'].includes(e.status)) return null;
    return e;
  } catch { return null; }
}

export type HudRow = {
  index: number;
  total: number | null;
  id?: string;
  kind?: 'step' | 'call';
  children?: HudRow[];
  name: string;
  detail: string;
  status: HudStatus;
};

export const HUD_ROWS = 7;

export type RecordingState = {
  active: boolean;
  steps: number | null;
};

/**
 * 录制和播放共用同一块 HUD，但录制没有 `[x/y]` 步骤行。
 * 从命令行固定的开始/结束句里认状态，别让录制期间只剩“等待第一步…”。
 */
export function recordingState(lines: readonly string[]): RecordingState | null {
  let state: RecordingState | null = null;
  for (const line of lines) {
    if (/●\s*录制中/.test(line)) {
      state = { active: true, steps: null };
      continue;
    }
    const stopped = /■\s*已停止[：:]\s*(\d+)\s*步/.exec(line);
    if (stopped) state = { active: false, steps: Number(stopped[1]) };
  }
  return state;
}

/** `[  3/8]`；命令行会补空格对齐，所以数字两侧都允许空白。 */
const COUNTER = /^\s*\[\s*(\d+)\s*\/\s*(\d+)\s*\]\s*(.*)$/;

/** 流程输出的状态词；`exit 3` 是失败，`ok`/`skip` 是正常结束。 */
const FLOW_STATUS = /^(ok|skip|exit\s+\d+)\s+(.*)$/;

/** 步骤真正开始前的提示行；只有编辑器起的进程会打。 */
const LIVE_STATUS = /^执行中\s+(.*)$/;

/**
 * 把命令描述归成编辑器里的步骤名。
 *
 * 命令描述来自 `FlowRunner.Describe`，前缀是稳定的；没有命中时退回“操作”，
 * 而不是把一整条命令行硬塞进名字列。
 */
export function stepName(command: string): string {
  const text = command.trim();
  if (/^KeyMouse\s+(mouse\s+)?click\b/i.test(text)) return '点击';
  if (/^KeyMouse\s+type\b/i.test(text)) return '输入文字';
  if (/^KeyMouse\s+key\b/i.test(text)) return '按键';
  if (/^KeyMouse\s+window\b/i.test(text)) return '切换窗口';
  if (/^KeyMouse\s+mouse\s+move\b/i.test(text)) return '移动鼠标';
  if (/^KeyMouse\s+mouse\s+drag\b/i.test(text)) return '拖动';
  if (/^KeyMouse\s+mouse\s+wheel\b/i.test(text)) return '滚轮';
  if (/^KeyMouse\s+waitfor\b/i.test(text)) return '等待';
  if (/^等窗口/.test(text)) return '等窗口出现';
  if (/^等文字/.test(text)) return '等文字出现';
  if (/^等\s/.test(text)) return '等待时间';
  if (/^设\s/.test(text)) return '设变量';
  if (/^确认\s/.test(text)) return '断言';
  if (/^找「/.test(text)) return '找字并点它';
  if (/^读\s/.test(text)) return '读屏取字';
  if (/^调用/.test(text)) return '调用另一个流程';
  return '操作';
}

/** HUD 只有一行宽，长命令留前面有信息的一段。 */
export function compactDetail(command: string): string {
  const flat = command.replace(/\s+/g, ' ').trim().replace(/^KeyMouse\s+/i, '');
  return flat.length > 54 ? `${flat.slice(0, 51)}…` : flat;
}

/**
 * 解析一条输出。没有 `[x/y]` 或数字不合法时返回 null。
 * 只有流程/脚本的“步骤行”才有这一步；其余汇总行由 HUD 忽略。
 */
export function parseHudLine(line: string): HudRow | null {
  const match = COUNTER.exec(line);
  if (!match) return null;
  const index = Number(match[1]);
  const total = Number(match[2]);
  if (!Number.isInteger(index) || !Number.isInteger(total) || total <= 0) return null;
  if (index < 1 || index > total) return null;

  const rest = match[3].trim();
  if (!rest) return null;

  const live = LIVE_STATUS.exec(rest);
  if (live) {
    const command = live[1].trim();
    return {
      index,
      total,
      name: stepName(command),
      detail: compactDetail(command),
      status: 'current',
    };
  }

  const flow = FLOW_STATUS.exec(rest);
  if (flow) {
    const tag = flow[1];
    const command = flow[2].trim();
    const status: HudStatus = tag === 'ok' ? 'done' : tag === 'skip' ? 'skipped' : 'failed';
    return {
      index,
      total,
      name: stepName(command),
      detail: compactDetail(command),
      status,
    };
  }

  // 脚本模式的输出：`[  1/12] 等待  sleep 400`。第一个词就是步骤名。
  const first = /^(\S+)\s*(.*)$/.exec(rest);
  if (!first) return null;
  return {
    index,
    total,
    name: first[1],
    detail: compactDetail(first[2]),
    status: 'done',
  };
}

export function statusText(status: HudStatus): string {
  return status === 'done'
    ? '完成'
    : status === 'skipped'
      ? '跳过'
      : status === 'failed'
        ? '失败'
        : status === 'cancelled' ? '已停止' : '执行中';
}

/**
 * 最近 7 条步骤日志；最后一条是当前操作，用来加重显示。
 *
 * 同一步会先来一条“执行中”，再来一条“完成/跳过/失败”。按步骤号合并，
 * 既保留开始时的“执行中”，也不会让 HUD 被同一步的两行占满。
 */
export function hudRows(lines: readonly string[]): HudRow[] {
  const events = lines.map(parseHudEvent).filter((e): e is HudEvent => e !== null);
  if (events.length) {
    const records = new Map<string, HudEvent>();
    const scopes = new Map<string, string[]>();
    for (const e of events) {
      const old = records.get(e.id);
      if (old && (old.seq >= e.seq || old.scopeId !== e.scopeId || old.kind !== e.kind ||
        old.origin !== e.origin || old.index !== e.index)) continue;
      if (!old) {
        const ids = scopes.get(e.scopeId) ?? [];
        ids.push(e.id);
        scopes.set(e.scopeId, ids);
      }
      records.set(e.id, e);
    }
    const rows = (scope: string, depth: number): HudRow[] => {
      if (depth > 8) return [];
      return (scopes.get(scope) ?? []).slice(scope === 'root' ? -HUD_ROWS : -HUD_CHILD_ROWS).map(id => {
        const e = records.get(id)!;
        return {
          id, kind: e.kind, index: e.index, total: e.total,
          name: stepName(e.command), detail: compactDetail(e.command), status: e.status,
          ...(e.kind === 'call' ? { children: rows(id, depth + 1) } : {}),
        };
      });
    };
    return rows('root', 0);
  }
  const byIndex = new Map<number, HudRow>();
  for (const line of lines) {
    const row = parseHudLine(line);
    if (row) byIndex.set(row.index, row);
  }
  return Array.from(byIndex.values()).slice(-HUD_ROWS);
}
