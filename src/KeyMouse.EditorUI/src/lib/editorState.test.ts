import { describe, expect, it } from 'vitest';
import { get } from 'svelte/store';
import { createDocumentStore, createHistory, createSelectionStore } from './editorState';

describe('editor session state', () => {
  it('keeps the original document metadata when replacing steps', () => {
    const a = createDocumentStore(), b = createDocumentStore();
    a.open({steps:[], extra:'keep'}); a.replaceSteps([{type:'sleep',ms:10}]);
    expect(get(a)).toEqual({steps:[{type:'sleep',ms:10}], extra:'keep'}); expect(get(b)).toBeNull();
  });
  it('repairs primary and multiple selection together after undo', () => {
    const state = createSelectionStore(); const path=(index:number)=>[{branch:'steps' as const,index}];
    state.select([path(0), path(1)],path(1)); state.retain([{type:'sleep',ms:1}]);
    expect(get(state)).toEqual({paths:[path(0)],primary:path(0)}); state.clear(); expect(get(state).paths).toEqual([]);
  });
  it('coalesces typing, drops redo after edits, never undoes across file switches', () => {
    const h=createHistory<string>(2);
    h.record(()=> 'before','a',0); h.record(()=> 'mid','a',50); expect(h.undo(()=> 'after')).toBe('before');
    expect(h.redo(()=> 'before')).toBe('after'); expect(h.undo(()=> 'after')).toBe('before');
    h.record(()=> 'before','a',60); expect(get(h).canRedo).toBe(false);
    h.reset(); expect(get(h)).toEqual({canUndo:false,canRedo:false});
    h.record(()=> 'fresh','a',70); expect(h.undo(()=> 'new')).toBe('fresh');
  });
});
