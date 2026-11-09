/**
 * 取点/取区域结果的处理规则。
 *
 * 客户区/窗口坐标属于某个窗口，默认把进程名一起写进 target；屏幕坐标不绑定窗口，
 * 不自动填进程名，否则下次窗口移动后这一步会莫名其妙地去找那个窗口。
 */

export function shouldAttachWindow(space: string): boolean {
  return space !== 'screen';
}

export function pickSpaceLabel(space: string): string {
  if (space === 'client') return '客户区';
  if (space === 'window') return '窗口';
  return '屏幕';
}
