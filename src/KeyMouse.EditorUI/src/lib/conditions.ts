/**
 * 流程控制条件的"选项 → 表达式"翻译。
 *
 * 流程文件里只有 `cond` 一个字符串；界面上把它拆成有限的几种选择，避免让用户
 * 手写 `&&`/括号/引号。生成出来的仍然是同一种表达式，所以引擎、老外壳、命令行
 * 看到的完全一样。选项覆盖不了的情况（老文件里的复杂表达式）退回"自定义"。
 *
 * 必填项的判定放在这里：缺任何一项就返回空串，节点上的「待补」与播放前检查
 * （`missingOf`）会照常拦住，不会把半截条件写进文件。
 */

export type ConditionMode =
  | ''
  | 'true'
  | 'false'
  | 'compare'
  | 'text'
  | 'window'
  | 'front'
  | 'found'
  | 'foundAny'
  | 'elapsed'
  | 'custom';

export interface ConditionFields {
  left: string;
  operator: '==' | '!=' | '<' | '<=' | '>' | '>=';
  right: string;
  text: string;
  process: string;
  ms: string;
  custom: string;
}

export const EMPTY_FIELDS: ConditionFields = {
  left: '',
  operator: '<',
  right: '',
  text: '',
  process: '',
  ms: '1000',
  custom: '',
};

export const MODE_LABELS: { value: ConditionMode; label: string; hint: string }[] = [
  { value: '', label: '选择条件…', hint: '先选一种，再填右边带「必填」的项。' },
  { value: 'true', label: '总是成立', hint: '等价于 cond = true；适合先搭结构、之后再改。' },
  { value: 'false', label: '从不成立', hint: '等价于 cond = false；调试分支时有用。' },
  { value: 'compare', label: '比较两个值', hint: '左值 / 运算符 / 右值，例如 n < 3、name == "完成"。' },
  { value: 'text', label: '屏幕上读到文字', hint: '生成 text("…")；还要在下面填「条件读哪里」的窗口与区域。' },
  { value: 'window', label: '窗口存在', hint: '生成 window("进程名")，例如 window("notepad")。' },
  { value: 'front', label: '窗口在前台', hint: '生成 front("进程名")。' },
  { value: 'found', label: '上次读到的文字包含', hint: '生成 found("…")；读取结果来自最近一次读屏/等待文字。' },
  { value: 'foundAny', label: '上次读到过文字', hint: '生成 found()；不关心具体内容，只要求上一次读屏有结果。' },
  { value: 'elapsed', label: '运行超过 N 毫秒', hint: '生成 elapsed() > N；N 必须是正整数。' },
  { value: 'custom', label: '自定义表达式', hint: '直接写引擎表达式；用于选项覆盖不了的情况。' },
];

function quote(text: string): string {
  return JSON.stringify(text);
}

function unquote(raw: string): string | null {
  const trimmed = raw.trim();
  if (trimmed.length < 2) return null;
  if (trimmed.startsWith('"')) {
    try {
      const parsed = JSON.parse(trimmed);
      return typeof parsed === 'string' ? parsed : null;
    } catch {
      return null;
    }
  }
  if (trimmed.startsWith("'") && trimmed.endsWith("'")) {
    return trimmed
      .slice(1, -1)
      .replace(/\\'/g, "'")
      .replace(/\\\\/g, '\\');
  }
  return null;
}

/** 解析一段表达式，给组件回填选项；认不出就退回 custom。 */
export function parseCondition(raw: string): { mode: ConditionMode; fields: ConditionFields } {
  const fields: ConditionFields = { ...EMPTY_FIELDS };
  const text = raw.trim();
  if (text === '') return { mode: '', fields };
  if (text === 'true') return { mode: 'true', fields };
  if (text === 'false') return { mode: 'false', fields };

  const stringArg = '("(?:[^"\\\\]|\\\\.)*"|\'(?:[^\'\\\\]|\\\\.)*\')';
  const textMatch = new RegExp(`^text\\s*\\(\\s*${stringArg}\\s*\\)$`).exec(text);
  if (textMatch) {
    const parsed = unquote(textMatch[1]);
    if (parsed !== null) {
      fields.text = parsed;
      return { mode: 'text', fields };
    }
  }
  const windowMatch = new RegExp(`^(window|front)\\s*\\(\\s*${stringArg}\\s*\\)$`).exec(text);
  if (windowMatch) {
    const parsed = unquote(windowMatch[2]);
    if (parsed !== null) {
      fields.process = parsed;
      return { mode: windowMatch[1] === 'window' ? 'window' : 'front', fields };
    }
  }
  const foundMatch = new RegExp(`^found\\s*\\(\\s*(?:${stringArg}\\s*)?\\)$`).exec(text);
  if (foundMatch) {
    if (foundMatch[1]) {
      const parsed = unquote(foundMatch[1]);
      if (parsed !== null) {
        fields.text = parsed;
        return { mode: 'found', fields };
      }
    } else {
      return { mode: 'foundAny', fields };
    }
  }
  const elapsedMatch = /^elapsed\s*\(\s*\)\s*>\s*(\d+)$/.exec(text);
  if (elapsedMatch) {
    fields.ms = elapsedMatch[1];
    return { mode: 'elapsed', fields };
  }
  const compareMatch = new RegExp(
    '^([A-Za-z_][A-Za-z0-9_]*)\\s*(==|!=|<=|>=|<|>)\\s*' +
      '("(?:[^"\\\\]|\\\\.)*"|\'(?:[^\'\\\\]|\\\\.)*\'|[-+]?\\d+(?:\\.\\d+)?|[A-Za-z_][A-Za-z0-9_]*)$',
  ).exec(text);
  if (compareMatch) {
    fields.left = compareMatch[1];
    fields.operator = compareMatch[2] as ConditionFields['operator'];
    fields.right = compareMatch[3].trim();
    return { mode: 'compare', fields };
  }

  fields.custom = raw;
  return { mode: 'custom', fields };
}

/** 生成 cond；必填项缺失时返回空串，交给 missingOf 拦。 */
export function buildCondition(mode: ConditionMode, fields: ConditionFields): string {
  switch (mode) {
    case 'true':
      return 'true';
    case 'false':
      return 'false';
    case 'compare': {
      const left = fields.left.trim();
      const right = fields.right.trim();
      return left && right ? `${left} ${fields.operator} ${right}` : '';
    }
    case 'text': {
      const text = fields.text.trim();
      return text ? `text(${quote(text)})` : '';
    }
    case 'window': {
      const process = fields.process.trim();
      return process ? `window(${quote(process)})` : '';
    }
    case 'front': {
      const process = fields.process.trim();
      return process ? `front(${quote(process)})` : '';
    }
    case 'found': {
      const text = fields.text.trim();
      return text ? `found(${quote(text)})` : '';
    }
    case 'foundAny':
      return 'found()';
    case 'elapsed': {
      const ms = Number.parseInt(fields.ms, 10);
      return Number.isFinite(ms) && ms > 0 ? `elapsed() > ${ms}` : '';
    }
    case 'custom': {
      const custom = fields.custom.trim();
      return custom || '';
    }
    default:
      return '';
  }
}
