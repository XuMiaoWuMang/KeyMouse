import type { FlowStep } from './model';

/** Conservative: if any step may use the real keyboard/pointer, retain the normal hide/play lifecycle. */
export function clientPlaybackOnly(steps: FlowStep[]): boolean {
  const client = (p?: { space?: string }) => !!p && (p.space ?? 'client') === 'client';
  return steps.length > 0 && steps.every(s => {
    if ((s.when && !client(s.when.region)) || (s.probe && !client(s.probe.region))) return false;
    switch (s.type) {
      case 'click': case 'move': case 'mousehold': case 'mousedown': case 'mouseup': case 'wheel': return client(s.at);
      case 'drag': return client(s.from) && client(s.to);
      case 'read-text': case 'wait-text': case 'click-text': return client(s.region);
      case 'repeat': case 'foreach': case 'if': case 'while': case 'do': case 'for':
        return [s.steps, s.else].every(branch => !branch?.length || clientPlaybackOnly(branch));
      case 'sleep': case 'set': case 'assert': case 'break': case 'continue': case 'return': return true;
      default: return false;
    }
  });
}
