import { writePath } from './fieldpath';
import type { FlowStep } from './model';
import { shouldAttachWindow } from './pick';

export interface PickRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** Tauri 的 pick_region 结果：相对坐标与绝对屏幕坐标必须同时保留。 */
export interface PickResult extends PickRect {
  space: string;
  screen: PickRect;
  process?: string | null;
  title?: string | null;
  class?: string | null;
  handle?: string | null;
}

export interface PickedField {
  step: FlowStep;
  coordinates: PickRect & { space: string };
  attachedProcess: string | null;
}

/** The two ends of a drag belong to one coordinate space. Update them in one undoable edit. */
export function editStepField(step: FlowStep, path: string, value: unknown): FlowStep {
  let next = writePath(step, path, value);
  if (step.type === 'drag' && (path === 'from.space' || path === 'to.space')) {
    next = writePath(next, path === 'from.space' ? 'to.space' : 'from.space', value);
  }
  return next as unknown as FlowStep;
}

/** 取点写回的唯一入口；保持纯函数，撤销快照和未编辑字段不受影响。 */
export function applyPickedField(step: FlowStep, kind: 'point' | 'region', path: string, picked: PickResult): PickedField {
  const dragPoint = step.type === 'drag' && kind === 'point' && (path === 'from' || path === 'to');
  let coordinates: PickRect & { space: string } = kind === 'point' && picked.space === 'window'
    ? { ...picked.screen, space: 'screen' } : picked;
  if (!dragPoint && path === 'at' && step.at?.space === 'screen') coordinates = { ...picked.screen, space: 'screen' };
  if (dragPoint) {
    // 已有字段的显式空间优先；只补缺失端时沿用另一端。
    // 空白的新拖动跟着"取点落在哪儿"走：窗口里取就是客户区（默认），桌面上取就是屏幕。
    const current = path === 'from' ? step.from : step.to;
    const other = path === 'from' ? step.to : step.from;
    const space = current ? current.space ?? 'client' : other ? other.space ?? 'client' : picked.space;
    if (space === 'client' && picked.space !== 'client') {
      throw new Error('这一步使用客户区坐标，请在目标窗口的客户区内取点');
    }
    if (space === 'client' && step.target && (['process', 'class', 'title'] as const).some(key =>
      step.target?.[key] && step.target[key]!.toLowerCase() !== picked[key]?.toLowerCase())) {
      throw new Error('后台拖动的两个端点必须取自同一个目标窗口；跨窗口请使用屏幕坐标');
    }
    coordinates = space === 'client' ? picked : { ...picked.screen, space: 'screen' };
  }
  let next = writePath(step, path, {
    space: coordinates.space,
    x: coordinates.x,
    y: coordinates.y,
    ...(kind === 'region' ? { width: coordinates.width, height: coordinates.height } : {}),
  });
  // 旧客户区拖动可能带用户写的精确选择器，重新取点只改坐标，不覆盖它。
  const hasDragTarget = dragPoint && Object.values(step.target ?? {}).some((value) => value?.trim());
  const attachWindow = shouldAttachWindow(coordinates.space) && !hasDragTarget;
  if (attachWindow && picked.process) {
    const owner = path.includes('.') ? path.slice(0, path.lastIndexOf('.') + 1) : '';
    next = writePath(next, `${owner}target`, { process: picked.process,
      ...(picked.class ? { class: picked.class } : {}), ...(picked.title ? { title: picked.title } : {}) });
  }
  return { step: next as unknown as FlowStep, coordinates, attachedProcess: attachWindow ? picked.process ?? null : null };
}
