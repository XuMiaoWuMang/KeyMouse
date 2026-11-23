import { writable } from 'svelte/store';
import type { FlowStep } from './model';
import { stepAt, type NodePath } from './tree';

/** 一个流程文件：文档对象原样保留，界面只碰 steps。 */
export type FlowDocument = {
  format?: string;
  version?: number;
  recordedAt?: string;
  variables?: Record<string, string>;
  steps: FlowStep[];
  [key: string]: unknown;
};

export function createDocumentStore() {
  const {subscribe, set, update} = writable<FlowDocument | null>(null);
  return {
    subscribe, open: set,
    replaceSteps: (steps: FlowStep[]) => update(doc => doc ? {...doc, steps} : null),
    notifyEdit: () => update(doc => doc),
  };
}

/** Each editor owns its selection: opening or undoing a document cannot leak paths to a sibling. */
export function createSelectionStore() {
  const empty = () => ({primary: null as NodePath | null, paths: [] as NodePath[]});
  const {subscribe, set, update} = writable(empty());
  return {subscribe, clear: () => set(empty()),
    select: (paths: NodePath[], primary: NodePath | null) => set({paths, primary}),
    retain: (steps: FlowStep[]) => update(s => {
      const paths = s.paths.filter(path => stepAt(steps, path) !== undefined);
      const primary = s.primary && !stepAt(steps, s.primary) ? paths.at(-1) ?? null : s.primary;
      return {paths, primary};
    }),
  };
}

/** Store snapshots together with their external section boundaries. Keyed typing coalesces for 800ms. */
export function createHistory<T>(limit = 50) {
  const past:T[] = [], future:T[] = [];
  let previousKey:string | undefined, previousTime=0;
  const {subscribe,set} = writable({canUndo:false,canRedo:false});
  function publish() { set({canUndo:past.length>0,canRedo:future.length>0}); }
  function transfer(from:T[], to:T[], current:()=>T) {
    if (!from.length) return undefined;
    const value=from.pop()!; to.push(current()); previousKey=undefined; publish(); return value;
  }
  return {subscribe,
    record(current:()=>T, key?:string, now=Date.now()) {
      const coalesce=key!==undefined && key===previousKey && now-previousTime<800;
      previousKey=key; previousTime=now;
      if (coalesce) return;
      past.push(current()); if (past.length>limit) past.shift(); future.length=0; publish();
    },
    undo:(current:()=>T)=>transfer(past,future,current), redo:(current:()=>T)=>transfer(future,past,current),
    reset(){past.length=future.length=0; previousKey=undefined; publish();},
  };
}
