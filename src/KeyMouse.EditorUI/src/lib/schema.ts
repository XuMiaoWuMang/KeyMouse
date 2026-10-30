// 流程格式的契约（来自 `KeyMouse schema`）。
//
// 界面照契约渲染字段，而不是在 Svelte 里再抄一份"点击步骤有哪些参数"。抄一份的结果是
// 命令行加了参数、界面看不见，两边慢慢对不上——老外壳（WinUI）读的是同一份契约。

export type FieldKind =
  | 'const'
  | 'text'
  | 'multiline'
  | 'int'
  | 'bool'
  | 'enum'
  | 'point'
  | 'region'
  | 'target'
  | 'steps'
  | 'vars'
  | 'stringList'
  | 'when';

export interface LocalizedText {
  zh: string;
  en?: string;
}

export interface FieldContract {
  name: string;
  kind: FieldKind;
  required?: boolean;
  default?: unknown;
  min?: number;
  max?: number;
  values?: string[];
  valueLabels?: Record<string, LocalizedText>;
  label: LocalizedText;
  help?: LocalizedText;
  /** 'button' = 该字段有值才显示；'!button' = 没值才显示（长按这类二选一步骤）。 */
  showWhen?: string;
}

export interface ShapeContract {
  /** 这个形状为什么这么设计（契约里的备注，只给人看）。 */
  comment?: string | LocalizedText;
  fields: FieldContract[];
}

export interface StepContract {
  summary: LocalizedText;
  fields: FieldContract[];
}

export interface FlowSchema {
  kinds: Record<string, string>;
  document: FieldContract[];
  common: FieldContract[];
  shapes: Record<string, ShapeContract>;
  steps: Record<string, StepContract>;
  format: string;
  version: number;
}

let cached: FlowSchema | null = null;
let pending: Promise<FlowSchema> | null = null;

export function parsedSchema(json: string): FlowSchema {
  return JSON.parse(json) as FlowSchema;
}

export function schemaFrom(json: string): FlowSchema {
  cached = parsedSchema(json);
  return cached;
}

/** 已经拿到的契约（还没拿到时返回 null，界面先不渲染检查器）。 */
export function currentSchema(): FlowSchema | null {
  return cached;
}

export function setPending(p: Promise<FlowSchema>): Promise<FlowSchema> {
  pending = p;
  return p;
}

export function pendingSchema(): Promise<FlowSchema> | null {
  return pending;
}

/** 某一种步骤的字段；不认识的类型返回空数组（界面上就别装懂）。 */
export function fieldsOf(schema: FlowSchema, type: string): FieldContract[] {
  return schema.steps[type]?.fields ?? [];
}

/** 一个形状（point / region / target / when）展开后的字段。
 *  契约里形状是 { comment, fields }，不是数组——第一版按数组写，运行时炸在
 *  shapeFields(...).every is not a function。 */
export function shapeFields(schema: FlowSchema, kind: string): FieldContract[] {
  return schema.shapes[kind]?.fields ?? [];
}

export function stepSummary(schema: FlowSchema, type: string): LocalizedText | null {
  return schema.steps[type]?.summary ?? null;
}

export function label(field: FieldContract): string {
  return field.label?.zh ?? field.name;
}

export function helpOf(field: FieldContract): string {
  return field.help?.zh ?? '';
}

/** showWhen 规则：`button` = 有值才显示，`!button` = 没值才显示。 */
export function visibleBy(field: FieldContract, step: Record<string, unknown>): boolean {
  const rule = field.showWhen;
  if (!rule || rule.length < 2) return true;
  const negated = rule.startsWith('!');
  const other = rule.slice(negated ? 1 : 0);
  const value = step[other];
  const present = !(value === undefined || value === null || value === '' || value === false);
  return negated ? !present : present;
}

/** 契约里"必填"的字段名，用来在列表行上标"待补"。 */
export function requiredNames(schema: FlowSchema, type: string): string[] {
  return fieldsOf(schema, type)
    .filter((f) => f.required)
    .map((f) => f.name);
}
