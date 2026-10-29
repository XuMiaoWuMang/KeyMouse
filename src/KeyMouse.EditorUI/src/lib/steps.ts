/**
 * 每种步骤的"新建模板"与"还缺什么"。
 *
 * 新建的步骤故意是**不完整**的：`focus` 不带目标窗口、`type` 不带文字。
 * 一插进来就是半成品，检查器右上角写着还缺哪几项——比插一个看起来能用、跑起来
 * 才发现没目标的步骤诚实。规则表照抄 `docs/reference/interface.md` 的参数契约，
 * 那边是引擎的真规则，这里只用来提示，不参与校验（校验问命令行）。
 */

import type { FlowStep, StepType } from './model';

export interface StepTemplate {
  type: StepType;
  /** 工具栏「+ 更多」里的分组，纯排版用 */
  group: '常用' | '鼠标' | '键盘' | '等待' | '读取' | '流程' | '其它';
  /** 缺了这些就发不出正确输入；用于检查器右上角的提示 */
  required: readonly string[];
  /** 字段名 → 给人看的中文，用于"还缺 X" */
  labels?: Readonly<Record<string, string>>;
}

export const STEP_TEMPLATES: readonly StepTemplate[] = [
  { type: 'focus', group: '常用', required: ['target'], labels: { target: '目标窗口' } },
  { type: 'type', group: '常用', required: ['text'], labels: { text: '文字' } },
  { type: 'wait-text', group: '常用', required: ['target', 'region', 'text'], labels: { target: '目标窗口', region: '区域', text: '文字' } },
  { type: 'click-text', group: '常用', required: ['target', 'region', 'text'], labels: { target: '目标窗口', region: '区域', text: '文字' } },
  { type: 'click', group: '鼠标', required: ['at'], labels: { at: '坐标' } },
  { type: 'drag', group: '鼠标', required: ['from', 'to', 'target'], labels: { from: '起点', to: '终点', target: '目标窗口' } },
  { type: 'move', group: '鼠标', required: ['at'], labels: { at: '坐标' } },
  { type: 'wheel', group: '鼠标', required: [] },
  { type: 'key', group: '键盘', required: ['combo'], labels: { combo: '按键组合' } },
  { type: 'sleep', group: '等待', required: [] },
  { type: 'wait-window', group: '等待', required: ['target'], labels: { target: '目标窗口' } },
  { type: 'read-text', group: '读取', required: ['target', 'region', 'into'], labels: { target: '目标窗口', region: '区域', into: '变量名' } },
  { type: 'repeat', group: '流程', required: ['times', 'steps'], labels: { times: '次数', steps: '子步骤' } },
  { type: 'foreach', group: '流程', required: ['in', 'steps'], labels: { in: '来源', steps: '子步骤' } },
  { type: 'call', group: '流程', required: ['flow'], labels: { flow: '流程文件' } },
];

const BY_TYPE = new Map(STEP_TEMPLATES.map((t) => [t.type, t]));

/** 工具栏上直接摆出来的四种，其余进「+ 更多」。 */
export const QUICK_TYPES: readonly StepType[] = ['click', 'type', 'wait-text', 'click-text'];

/** 新建一步。只放"这一步是什么"，参数留给检查器——空着比猜错好。 */
export function makeStep(type: StepType): FlowStep {
  switch (type) {
    case 'focus':
      return { type };
    case 'type':
      return { type, intervalMs: 30 };
    case 'key':
      return { type, combo: 'Ctrl+S' };
    case 'sleep':
      return { type, ms: 300 };
    case 'wheel':
      return { type, delta: -120 };
    case 'wait-text':
    case 'click-text':
      return { type, text: '', match: 'contains', timeoutMs: 8000 };
    case 'wait-window':
      return { type, timeoutMs: 8000 };
    case 'read-text':
      return { type, into: 'value', match: 'contains' };
    case 'repeat':
      return { type, times: 3, steps: [] };
    case 'foreach':
      return { type, in: 'items', steps: [] };
    default:
      return { type };
  }
}

/** 空、空白、空数组、空对象都算"没填"——检查器里敲了几个空格不该被当成填好了。 */
function isBlank(value: unknown): boolean {
  if (value === undefined || value === null) return true;
  if (typeof value === 'string') return value.trim() === '';
  if (Array.isArray(value)) return value.length === 0;
  if (typeof value === 'object') {
    const fields = Object.entries(value as Record<string, unknown>);
    if (fields.length === 0) return true;
    return fields.every(([, field]) => typeof field === 'string' && field.trim() === '');
  }
  return false;
}

/** 这一步还缺哪些必填项（键名）。齐了就是空数组。 */
export function missingOf(step: FlowStep): string[] {
  const template = BY_TYPE.get(step.type);
  if (!template) return [];
  const raw = step as unknown as Record<string, unknown>;
  return template.required.filter((key) => isBlank(raw[key]));
}

/** 把缺的项说成人话，用于检查器右上角。 */
export function missingHint(step: FlowStep): string {
  const missing = missingOf(step);
  if (missing.length === 0) return '齐全';
  const template = BY_TYPE.get(step.type);
  const words = missing.map((key) => template?.labels?.[key] ?? key);
  return `还缺 ${words.join('、')}`;
}
