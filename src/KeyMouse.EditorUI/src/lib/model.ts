/**
 * 流程文件的形状 —— 与 `src/KeyMouse.Core/flow.schema.json` 对齐。
 *
 * 这里只放"界面要读的那部分"：步骤种类、标题、一行摘要。真正的校验在引擎里，
 * 前端不重复实现规则——契约只有一份。
 */

export interface FlowTarget {
  process?: string;
  class?: string;
  title?: string;
}

export interface FlowPoint {
  space: 'screen' | 'client';
  x: number;
  y: number;
}

export interface FlowRegion {
  space: 'client';
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface FlowStep {
  type: StepType;
  note?: string;
  target?: FlowTarget;
  at?: FlowPoint;
  from?: FlowPoint;
  to?: FlowPoint;
  region?: FlowRegion;
  text?: string;
  match?: 'contains' | 'exact' | 'fuzzy';
  maxErrors?: number;
  timeoutMs?: number;
  intervalMs?: number;
  durationMs?: number;
  confirm?: number;
  button?: 'left' | 'right' | 'middle';
  into?: string;
  'in'?: string;
  flow?: string;
  vars?: Record<string, unknown>;
  export?: Record<string, string>;
  when?: string;
  ms?: number;
  delta?: number;
  combo?: string;
  count?: number;
  times?: number;
  steps?: FlowStep[];
  /**
   * 录制留下的证据图，路径**相对流程文件**（`flow.shots/0001-click.png`）。
   *
   * 只有录制会写它，回放不会重拍——所以界面上这张图永远是"录这一遍时它长什么样"。
   */
  shot?: string;
}

export interface FlowDocument {
  format: 'keymouse-flow';
  version: 1;
  recordedAt?: string;
  variables?: Record<string, string | string[]>;
  steps: FlowStep[];
}

export type StepType =
  | 'focus'
  | 'click'
  | 'drag'
  | 'move'
  | 'wheel'
  | 'type'
  | 'key'
  | 'keydown'
  | 'keyup'
  | 'keyhold'
  | 'mousedown'
  | 'mouseup'
  | 'mousehold'
  | 'sleep'
  | 'wait-window'
  | 'wait-text'
  | 'click-text'
  | 'read-text'
  | 'repeat'
  | 'foreach'
  | 'call';

const TITLES: Record<StepType, string> = {
  focus: '切换窗口',
  click: '点击',
  drag: '拖动',
  move: '移动鼠标',
  wheel: '滚轮',
  type: '输入文字',
  key: '按键/组合键',
  // 这四个是"按下 / 松开"分开写的那一对，录制与命令行都能产生；
  // 少了它们列表里就只剩英文类型名（实测：用户录一段长按，行上写着 keyhold）。
  keydown: '按住键',
  keyup: '松开键',
  keyhold: '键盘长按',
  mousedown: '按住鼠标键',
  mouseup: '松开鼠标键',
  mousehold: '鼠标长按',
  sleep: '等待时间',
  'wait-window': '等窗口出现',
  'wait-text': '等文字出现',
  'click-text': '找字并点它',
  'read-text': '读屏取字',
  repeat: '重复 N 次',
  foreach: '逐个遍历',
  call: '调用另一个流程',
};

const MATCH_LABELS: Record<string, string> = {
  contains: '包含即可',
  exact: '必须完全一致',
  fuzzy: '允许容错',
};

export function stepTitle(type: StepType): string {
  return TITLES[type] ?? type;
}

export function targetLabel(target: FlowTarget | undefined): string {
  if (!target) return '';
  if (target.process) return target.process;
  if (target.title) return `标题「${target.title}」`;
  if (target.class) return `类名 ${target.class}`;
  return '';
}

function pointLabel(p: FlowPoint): string {
  return `${p.x},${p.y}`;
}

/**
 * 点击的摘要：`点 120,64`，右键/中键再跟一个「右键」「中键」。
 * 左键是默认值，写出来只是噪音——用户明确要求"左键默认不显示"。
 */
function clickLabel(step: FlowStep): string {
  const where = step.at ? `点 ${pointLabel(step.at)}` : '点击';
  if (step.button === 'right') return `${where} · 右键`;
  if (step.button === 'middle') return `${where} · 中键`;
  return where;
}

/** 「左键 / 右键 / 中键」：没写 button 就是左键（契约里的默认值）。 */
export function buttonLabel(step: FlowStep): string {
  if (step.button === 'right') return '右键';
  if (step.button === 'middle') return '中键';
  return '左键';
}

/**
 * 一行摘要。规则和 WinUI 版的 `StepVm.Summary` 一致：**说"这一步做什么"，
 * 不说"字段是什么"**——参数只写人会关心的那几个。
 */
export function stepSummary(step: FlowStep): string {
  const where = targetLabel(step.target);
  switch (step.type) {
    case 'focus':
      return where ? `切到 ${where}` : '切到目标窗口';
    case 'type': {
      const text = step.text ? `输入「${step.text}」` : '输入文字';
      return step.intervalMs ? `${text}，间隔 ${step.intervalMs} ms` : text;
    }
    case 'key':
      return step.combo ? `按 ${step.combo}` : step.text ? `敲「${step.text}」` : '按键';
    case 'keydown':
    case 'keyup': {
      const which = step.combo ?? step.text ?? '?';
      return `${step.type === 'keydown' ? '按住' : '松开'} ${which}`;
    }
    case 'keyhold': {
      const which = step.combo ?? step.text ?? '?';
      return step.ms ? `按住 ${which} ${step.ms} ms` : `按住 ${which}`;
    }
    case 'mousedown':
    case 'mouseup':
      return `${step.type === 'mousedown' ? '按住' : '松开'} ${buttonLabel(step)}`;
    case 'mousehold':
      return step.ms ? `按住 ${buttonLabel(step)} ${step.ms} ms` : `按住 ${buttonLabel(step)}`;
    case 'sleep':
      return `等 ${step.ms ?? 0} ms`;
    case 'click':
      return step.at ? clickLabel(step) : '点击';
    case 'move':
      return step.at ? `移到 ${pointLabel(step.at)}` : '移动鼠标';
    case 'wheel':
      return `滚轮 ${step.delta ?? 0}`;
    case 'wait-text':
    case 'click-text':
    case 'read-text': {
      const match = MATCH_LABELS[step.match ?? 'contains'];
      const errors = step.maxErrors && step.maxErrors > 0 ? `，容错 ${step.maxErrors}` : '';
      const verb = step.type === 'click-text' ? '并点它' : step.type === 'wait-text' ? '出现' : '取字';
      return `找「${step.text ?? ''}」${verb}（${match}${errors}）`;
    }
    case 'repeat':
      return `重复 ${step.times ?? 0} 次`;
    case 'foreach':
      return '逐个遍历';
    case 'call':
      return '调用另一个流程';
    default:
      return '';
  }
}

/** 子步骤：只有分组类步骤才有。 */
export function childSteps(step: FlowStep): FlowStep[] {
  return step.steps ?? [];
}
