import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { hudRows, HUD_EVENT_PREFIX, parseHudEvent, statusText } from '../../src/KeyMouse.EditorUI/src/lib/hud';

let sequence = 0;
const event = (id: string, scopeId = 'root', kind = 'step', status = 'done', index = 1) =>
  HUD_EVENT_PREFIX + JSON.stringify({ seq: ++sequence, id, scopeId, kind, status, index, total: null, origin: '1', command: kind === 'call' ? '调用 child.json' : '等 0 ms' });

describe('call HUD tree', () => {
  it('shows the running call before any child completes and merges its terminal state', () => {
    const lines = [event('call', 'root', 'call', 'current'), event('child', 'call', 'step', 'current')];
    expect(hudRows(lines)[0]).toMatchObject({ id: 'call', status: 'current', total: null, children: [{ id: 'child', status: 'current' }] });
    lines.push(event('child', 'call'), event('call', 'root', 'call', 'done'));
    expect(hudRows(lines)).toHaveLength(1);
    expect(hudRows(lines)[0]).toMatchObject({ status: 'done', children: [{ id: 'child', status: 'done' }] });
  });

  it('retains latest three children both during a long call and after completion', () => {
    const lines = [event('call', 'root', 'call', 'current')];
    for (let i = 1; i <= 600; i++) lines.push(event(`child${i}`, 'call', 'step', i === 600 ? 'current' : 'done', i));
    expect(hudRows(lines)[0]).toMatchObject({ status: 'current', children: [{ index: 598 }, { index: 599 }, { index: 600, status: 'current' }] });
    lines.push(event('child600', 'call', 'step', 'done', 600), event('call', 'root', 'call', 'done'));
    expect(hudRows(lines)[0]).toMatchObject({ status: 'done', children: [{ index: 598 }, { index: 599 }, { index: 600, status: 'done' }] });
  });

  it('evicts a completed call and all descendants only when the eighth parent arrives', () => {
    const lines = [event('call', 'root', 'call'), event('nested', 'call', 'call'), event('leaf', 'nested')];
    for (let i = 2; i <= 7; i++) lines.push(event(`root${i}`, 'root', 'step', 'done', i));
    expect(hudRows(lines)).toHaveLength(7);
    expect(hudRows(lines)[0].children?.[0].children?.[0].id).toBe('leaf');
    lines.push(event('root8', 'root', 'step', 'done', 8));
    expect(hudRows(lines).map(r => r.id)).toEqual(['root2', 'root3', 'root4', 'root5', 'root6', 'root7', 'root8']);
  });

  it('keeps repeated invocations with identical origins and local indices separate', () => {
    const lines = [event('c1', 'root', 'call', 'done', 1), event('s1', 'c1'), event('c2', 'root', 'call', 'current', 2), event('s2', 'c2', 'step', 'current')];
    const rows = hudRows(lines);
    expect(rows.map(r => [r.id, r.children?.[0].id, r.children?.[0].index])).toEqual([['c1', 's1', 1], ['c2', 's2', 1]]);
    expect(rows.map(r => r.status)).toEqual(['done', 'current']);
  });

  it('nests calls and ignores legacy duplicate step output in structured mode', () => {
    const rows = hudRows([event('a', 'root', 'call', 'current'), event('b', 'a', 'call', 'current'), event('s', 'b'), ' [1/1] ok 等 0 ms']);
    expect(rows).toHaveLength(1);
    expect(rows[0].children?.[0].children?.[0].id).toBe('s');
  });

  it.each(['failed', 'cancelled', 'skipped'] as const)('preserves %s terminal status', status => {
    const rows = hudRows([event('a', 'root', 'call', 'current'), event('s', 'a', 'step', 'current'), event('s', 'a', 'step', status), event('a', 'root', 'call', status)]);
    expect(rows[0].status).toBe(status);
    expect(rows[0].children?.[0].status).toBe(status);
    expect(statusText(status)).not.toBe('执行中');
  });

  it('does not regress terminal state or reparent an existing invocation', () => {
    const start = event('call', 'root', 'call', 'current');
    const done = event('call', 'root', 'call', 'done');
    const reparent = event('call', 'other', 'call', 'current');
    expect(hudRows([start, done, start, reparent])[0]).toMatchObject({ id: 'call', status: 'done' });
  });

  it('still renders legacy script rows when no structured events exist', () => {
    expect(hudRows([' [1/2] 执行中 等 0 ms', ' [1/2] ok 等 0 ms', ' [2/2] exit 3 确认 false']).map(r => r.status)).toEqual(['done', 'failed']);
  });

  it('rejects malformed protocol and unknown versions', () => {
    expect(parseHudEvent(HUD_EVENT_PREFIX + '{}')).toBeNull();
    expect(parseHudEvent('@keymouse-hud/v2 {}')).toBeNull();
    expect(parseHudEvent(event('root'))).toBeNull();
  });

  it('replays every prefix of real C# output, including running nested calls', () => {
    const path = process.env.CALL_HUD_EVENTS ?? new URL('./nested-call.fixture', import.meta.url);
    const lines = readFileSync(path, 'utf8').split('\n');
    let sawRunningChild = false;
    for (let end = 1; end <= lines.length; end++) {
      const rows = hudRows(lines.slice(0, end));
      expect(rows.length).toBeLessThanOrEqual(7);
      for (const row of rows) {
        expect(row.children?.length ?? 0).toBeLessThanOrEqual(3);
        if (row.status === 'current' && row.children?.some(child => child.status === 'current')) sawRunningChild = true;
      }
    }
    const rows = hudRows(lines);
    expect(sawRunningChild).toBe(true);
    expect(rows).toHaveLength(3);
    expect(rows.slice(0, 2).every(r => r.kind === 'call' && r.status === 'done')).toBe(true);
    expect(rows[0].children).toHaveLength(3);
    expect(rows[0].children?.at(-1)?.children).toHaveLength(1);
  });
});
