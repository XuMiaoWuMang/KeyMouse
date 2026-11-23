import { afterEach, describe, expect, it, vi } from 'vitest';
import { ChildFlowSession, adoptChildSession, discardChildSessions, editChildField, retainChildSession, retainedChildSession, childErrorMessage } from './childflow';
import type { FlowDocument, FlowStep } from './model';

const path = [{ branch: 'steps' as const, index: 0 }];
const opened = (steps: FlowStep[] = [{ type: 'sleep', ms: 1 }]) => ({ path: 'C:/flows/child.json', text: '{\"steps\":[]}', json: { steps } });
afterEach(() => discardChildSessions());

describe('independent child document session', () => {
  it('adopts disk text separately and advances the successful save baseline', async () => {
    const session = new ChildFlowSession(opened());
    expect(session.dirty).toBe(false);
    session.document = editChildField(session.document, path, 'ms', 2);
    const write = vi.fn().mockResolvedValue(undefined);
    await session.save(write);
    const text = JSON.stringify(session.document, null, 2);
    expect(write).toHaveBeenCalledWith({ path: session.path, text, expectedText: opened().text });
    expect(session.expectedText).toBe(text);
    expect(session.dirty).toBe(false);
    session.document = editChildField(session.document, path, 'ms', 3);
    await session.save(write);
    expect(write.mock.calls[1][0].expectedText).toBe(text);
  });

  it('retains edits and the compare token on conflicts without retrying', async () => {
    const session = new ChildFlowSession(opened());
    session.document = editChildField(session.document, path, 'ms', 2);
    const write = vi.fn().mockRejectedValue({ message: 'file changed' });
    await expect(session.save(write)).rejects.toEqual({ message: 'file changed' });
    expect(session.expectedText).toBe(opened().text);
    expect(session.dirty).toBe(true);
    expect(session.saving).toBe(false);
    expect(write).toHaveBeenCalledTimes(1);
  });

  it('retains edits made after the in-flight snapshot and prevents duplicate saves', async () => {
    const session = new ChildFlowSession(opened());
    session.document = editChildField(session.document, path, 'ms', 2);
    let finish!: () => void;
    const write = vi.fn(() => new Promise<void>((resolve) => { finish = resolve; }));
    const pending = session.save(write);
    expect(session.saving).toBe(true);
    retainChildSession(session);
    expect(adoptChildSession(opened())).toBe(session);
    await session.save(write);
    session.document = editChildField(session.document, path, 'ms', 3);
    finish();
    await pending;
    expect(write).toHaveBeenCalledTimes(1);
    expect(session.dirty).toBe(true);
    expect(JSON.parse(session.expectedText).steps[0].ms).toBe(2);
  });

  it('edits nested fields without mutating or inlining the parent call', () => {
    const parent: FlowDocument = { format: 'keymouse-flow', version: 1, steps: [{ type: 'call', flow: 'child.json' }] };
    const session = new ChildFlowSession(opened([{ type: 'if', cond: 'true', steps: [{ type: 'click', at: { space: 'screen', x: 1, y: 2 } }] }]));
    const selected = [...path, { branch: 'steps' as const, index: 0 }];
    const original = session.document;
    session.document = editChildField(original, selected, 'at.x', 42);
    expect(session.document.steps[0].steps?.[0].at?.x).toBe(42);
    expect(original.steps[0].steps?.[0].at?.x).toBe(1);
    expect(parent).toEqual({ format: 'keymouse-flow', version: 1, steps: [{ type: 'call', flow: 'child.json' }] });
  });

  it('keeps same-file editor dirty reports independent and protects dirty grandchildren', () => {
    const call: FlowStep = { type: 'call', flow: 'grandchild.json' };
    const container: FlowStep = { type: 'repeat', count: 2, steps: [call] };
    const session = new ChildFlowSession(opened([container]));
    session.reportChild('editor-a', call, true);
    session.reportChild('editor-b', call, false);
    expect(session.pendingChanges).toBe(true);
    expect(session.hasDirtyWithin(container)).toBe(true);
    expect(session.hasDirtyWithin(call)).toBe(true);
    expect(session.hasDirtyWithin({ type: 'sleep' })).toBe(false);
    retainChildSession(session);
    expect(adoptChildSession(opened())).toBe(session);
    session.reportChild('editor-a', call, false);
    expect(session.pendingChanges).toBe(false);
  });

  it('blocks deleting a block or changing a call with dirty grandchildren', () => {
    const call: FlowStep = { type: 'call', flow: 'grandchild.json' };
    const container: FlowStep = { type: 'repeat', count: 2, steps: [call] };
    const session = new ChildFlowSession(opened([container]));
    const callPath = [...path, { branch: 'steps' as const, index: 0 }];
    session.reportChild('grandchild-editor', call, true);
    const original = session.document;
    expect(() => session.remove(path)).toThrow('孙流程');
    expect(() => session.editField(callPath, 'flow', 'replacement.json')).toThrow('孙流程');
    expect(session.document).toBe(original);
    session.editField(callPath, 'note', 'allowed');
    expect(() => session.remove(path)).toThrow('孙流程');
    session.reportChild('grandchild-editor', call, false);
    session.editField(callPath, 'flow', 'replacement.json');
    expect(session.document.steps[0].steps?.[0].flow).toBe('replacement.json');
    session.remove(path);
    expect(session.document.steps).toEqual([]);
  });

  it('notifies a remounted editor when an in-flight save completes', async () => {
    const session = adoptChildSession(opened(), 'remounted');
    session.editField(path, 'ms', 2);
    let finish!: () => void;
    const pending = session.save(() => new Promise<void>((resolve) => { finish = resolve; }));
    retainChildSession(session);
    const restored = retainedChildSession('remounted')!;
    const notify = vi.fn();
    const unsubscribe = restored.subscribe(notify);
    finish();
    await pending;
    expect(notify).toHaveBeenCalledTimes(1);
    expect(restored.saving).toBe(false);
    expect(restored.dirty).toBe(false);
    unsubscribe();
    restored.reportChild('child', { type: 'call' }, true);
    expect(notify).toHaveBeenCalledTimes(1);
  });

  it('does not apply field edits or removal during a save', async () => {
    const session = new ChildFlowSession(opened());
    let finish!: () => void;
    const pending = session.save(() => new Promise<void>((resolve) => { finish = resolve; }));
    const snapshot = session.document;
    session.editField(path, 'ms', 9);
    session.remove(path);
    expect(session.document).toBe(snapshot);
    finish();
    await pending;
  });

  it('retains draft on unmount but explicit discard prevents resurrection', () => {
    const session = adoptChildSession(opened());
    session.document = editChildField(session.document, path, 'ms', 2);
    retainChildSession(session);
    expect(adoptChildSession(opened())).toBe(session);
    discardChildSessions();
    retainChildSession(session); // delayed destroy after the parent confirmed discard
    const fresh = adoptChildSession(opened());
    expect(fresh).not.toBe(session);
    expect(fresh.dirty).toBe(false);
  });

  it('uses stable distinct descendant identities even for two calls to one path', () => {
    const a: FlowStep = { type: 'call', flow: 'same.json' };
    const b: FlowStep = { type: 'call', flow: 'same.json' };
    const session = new ChildFlowSession(opened([a, b]));
    expect(session.childIdentity(a)).toBe(session.childIdentity(a));
    expect(session.childIdentity(a)).not.toBe(session.childIdentity(b));
  });

  it('isolates cached drafts for separate editors of the same file', () => {
    const a = adoptChildSession(opened(), 'instance-a');
    const b = adoptChildSession(opened(), 'instance-b');
    a.document = editChildField(a.document, path, 'ms', 2);
    b.document = editChildField(b.document, path, 'ms', 3);
    retainChildSession(a);
    retainChildSession(b);
    expect(retainedChildSession('instance-a')).toBe(a);
    expect(retainedChildSession('instance-b')).toBe(b);
    expect(adoptChildSession(opened(), 'instance-a')).toBe(a);
    expect(adoptChildSession(opened(), 'instance-b')).toBe(b);
    expect(a.document.steps[0].ms).toBe(2);
    expect(b.document.steps[0].ms).toBe(3);
  });

  it('preserves a descendant draft identity when editing its call metadata', () => {
    const call: FlowStep = { type: 'call', flow: 'grandchild.json' };
    const session = new ChildFlowSession(opened([call]));
    const key = session.childIdentity(call);
    session.reportChild(key, call, true);
    const updated = { ...call, note: 'changed' };
    session.inheritChildIdentity(call, updated);
    expect(session.childIdentity(updated)).toBe(key);
    expect(session.hasDirtyWithin(updated)).toBe(true);
    expect(session.reportChild(key, updated, true)).toBe(false);
  });

  it('formats Tauri errors using message instead of object coercion', () => {
    expect(childErrorMessage({ message: 'conflict', code: 'changed' })).toBe('conflict');
    expect(childErrorMessage(new Error('failed'))).toBe('failed');
    expect(childErrorMessage('cancelled')).toBe('cancelled');
    expect(childErrorMessage({ code: 1 })).toBe('{\"code\":1}');
  });
});
