import { describe, expect, it } from 'vitest';
import { clientPlaybackOnly } from './clientPlayback';
import type { FlowStep } from './model';

describe('client playback visibility', () => {
  const click: FlowStep = { type: 'click', at: { x: 2, y: 3 } };
  it('keeps the editor visible for implicit and explicit client coordinates, including nested steps', () => {
    expect(clientPlaybackOnly([click, { type: 'repeat', steps: [{ type: 'drag', from: click.at, to: { space: 'client', x: 0, y: 0 } }] }])).toBe(true);
  });
  it('retains foreground lifecycle for mixed flows, screen reads, incomplete and unknown actions', () => {
    for (const step of [{ type: 'key' }, { type: 'click', at: { ...click.at, space: 'screen' } },
      { ...click, when: { region: { space: 'screen' } } }, { type: 'if', else: [{ type: 'focus' }] },
      { type: 'click' }, { type: 'future-action' }]) {
      expect(clientPlaybackOnly([click, step as FlowStep])).toBe(false);
    }
  });
});
