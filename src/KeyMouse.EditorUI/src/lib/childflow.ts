/** ChildFlow's independent document session. The parent call remains a reference.
 * open_child_flow -> {path,text,json}; save_child_flow uses expectedText as a
 * compare-and-save token. Never retry a failed save with unconditional save_flow.
 */
import { branchesOf, type FlowDocument, type FlowStep } from './model';
import { editStepField } from './pickField';
import { removeAt, replaceAt, stepAt, type NodePath } from './tree';

export interface OpenChildFlow {
  path: string;
  text: string;
  json: unknown;
}

/** Same adoption boundary as the main editor; validation belongs to the engine. */
export function adoptChildFlow(json: unknown): FlowDocument {
  if (!json || typeof json !== 'object') throw new Error('文件里不是 JSON 对象');
  const candidate = json as FlowDocument;
  if (!Array.isArray(candidate.steps)) throw new Error('这份文件没有 steps 数组，不像是流程');
  return candidate;
}

export function serializeChildFlow(document: FlowDocument): string {
  return JSON.stringify(document, null, 2);
}

export function editChildField(document: FlowDocument, selected: NodePath, field: string, value: unknown): FlowDocument {
  const step = stepAt(document.steps, selected);
  if (!step) return document;
  const next = editStepField(step, field, value);
  return { ...document, steps: replaceAt(document.steps, selected, next) };
}

/** Retain unsaved sessions across a call node's temporary removal/remount.
 * The parent still guards destructive actions before unmounting dirty children.
 */
const drafts = new Map<string, ChildFlowSession>();
let cacheGeneration = 0;
let nextIdentity = 0;

/** Stable per editor, never a file path: two editors may open the same file. */
export function childEditorIdentity(): string {
  return `child-editor-${++nextIdentity}`;
}

/** Call after confirming abandonment of the parent document, before its teardown.
 * The generation prevents delayed onDestroy callbacks from restoring old drafts. */
export function discardChildSessions(): void {
  cacheGeneration += 1;
  drafts.clear();
}

export function childErrorMessage(cause: unknown): string {
  if (cause && typeof cause === 'object' && 'message' in cause && typeof cause.message === 'string') return cause.message;
  if (typeof cause === 'string') return cause;
  try { return JSON.stringify(cause) ?? String(cause); } catch { return '未知错误'; }
}

export function retainChildSession(session: ChildFlowSession): void {
  if (session.cacheGeneration !== cacheGeneration) return;
  if (session.pendingChanges) drafts.set(session.cacheKey, session);
  else if (drafts.get(session.cacheKey) === session) drafts.delete(session.cacheKey);
}

/** Restore before touching disk: a dirty draft survives deletion or an open error. */
export function retainedChildSession(identity: string): ChildFlowSession | undefined {
  return [...drafts.values()].find((session) => session.identity === identity && session.pendingChanges);
}

export function adoptChildSession(opened: OpenChildFlow, identity = opened.path): ChildFlowSession {
  const key = JSON.stringify([identity, opened.path]);
  const draft = drafts.get(key);
  if (draft?.pendingChanges) return draft;
  drafts.delete(key);
  return new ChildFlowSession(opened, identity);
}

export type SaveChildFlow = (args: { path: string; text: string; expectedText: string }) => Promise<unknown>;

/** Keeps the exact disk text separately from the adopted document's dirty baseline.
 * A successful in-flight save advances only its snapshot, retaining newer edits.
 */
export class ChildFlowSession {
  readonly path: string;
  readonly cacheGeneration = cacheGeneration;
  readonly cacheKey: string;
  document: FlowDocument;
  expectedText: string;
  private savedDocument: string;
  private children = new Map<string, { step: FlowStep; dirty: boolean }>();
  private childKeys = new WeakMap<FlowStep, string>();
  private listeners = new Set<() => void>();

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  private notify(): void {
    for (const listener of this.listeners) listener();
  }
  saving = false;

  constructor(opened: OpenChildFlow, readonly identity = opened.path) {
    this.path = opened.path;
    this.cacheKey = JSON.stringify([identity, opened.path]);
    this.document = adoptChildFlow(opened.json);
    this.expectedText = opened.text;
    this.savedDocument = serializeChildFlow(this.document);
  }

  get dirty(): boolean {
    return serializeChildFlow(this.document) !== this.savedDocument;
  }

  get pendingChanges(): boolean {
    return this.dirty || this.saving || [...this.children.values()].some((child) => child.dirty);
  }

  childIdentity(step: FlowStep): string {
    let key = this.childKeys.get(step);
    if (!key) { key = childEditorIdentity(); this.childKeys.set(step, key); }
    return key;
  }

  inheritChildIdentity(original: FlowStep, updated: FlowStep): void {
    const key = this.childKeys.get(original);
    if (key) this.childKeys.set(updated, key);
    for (const child of this.children.values()) {
      if (child.step === original) child.step = updated;
    }
  }

  reportChild(key: string, step: FlowStep, dirty: boolean): boolean {
    const previous = this.children.get(key);
    if (dirty) {
      if (previous?.step === step) return false;
      this.children.set(key, { step, dirty });
    } else {
      if (!previous) return false;
      this.children.delete(key);
    }
    this.notify();
    return true;
  }

  /** Protect the selected call or any call nested inside a removed block. */
  hasDirtyWithin(step: FlowStep): boolean {
    if ([...this.children.values()].some((child) => child.dirty && child.step === step)) return true;
    return branchesOf(step).some((branch) => branch.steps.some((child) => this.hasDirtyWithin(child)));
  }

  editField(selected: NodePath, field: string, value: unknown): void {
    if (this.saving) return;
    const original = stepAt(this.document.steps, selected);
    if (!original) return;
    if (field === 'flow' && this.hasDirtyWithin(original)) {
      throw new Error('请先保存此调用中的子流程及孙流程，再修改调用路径。');
    }
    this.document = editChildField(this.document, selected, field, value);
    const updated = stepAt(this.document.steps, selected);
    if (updated) this.inheritChildIdentity(original, updated);
  }

  remove(selected: NodePath): void {
    if (this.saving) return;
    const target = stepAt(this.document.steps, selected);
    if (target && this.hasDirtyWithin(target)) {
      throw new Error('所选节点包含未保存的子流程或孙流程，请先保存再删除。');
    }
    this.document = { ...this.document, steps: removeAt(this.document.steps, selected) };
  }

  async save(write: SaveChildFlow): Promise<void> {
    if (this.saving) return;
    const text = serializeChildFlow(this.document);
    this.saving = true;
    this.notify();
    try {
      await write({ path: this.path, text, expectedText: this.expectedText });
      this.expectedText = text;
      this.savedDocument = text;
    } finally {
      this.saving = false;
      this.notify();
    }
  }
}
