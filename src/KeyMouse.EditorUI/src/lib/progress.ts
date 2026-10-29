/**
 * 从命令行输出里读"跑到第几步了"。
 *
 * 为什么只读到"第几步"为止，不去猜是哪一行：`run` 的编号是**执行序号**，循环会展开、
 * 子流程会内联，与界面里的行号不是一回事。硬把 3 对到第 3 行，会在有循环的流程里
 * 给出一个自信但错误的答案。给人一个"6/12"的进度，比一个错的定位有用。
 *
 * 输出格式（`KeyMouse run`）：
 *   流程输出：`    [  1/8] ok       切到 notepad`
 *   脚本输出：`[  1/12] 等待  sleep 400`
 *   失败行：  `[  2/12] 失败  window focus --process notepad`
 */

export type RunProgress = { step: number; total: number };

const PATTERN = /\[\s*(\d+)\s*\/\s*(\d+)\s*\]/;

export function parseRunProgress(line: string): RunProgress | null {
  const match = PATTERN.exec(line);
  if (!match) return null;

  const step = Number(match[1]);
  const total = Number(match[2]);
  if (!Number.isFinite(step) || !Number.isFinite(total) || total <= 0) return null;
  if (step < 1 || step > total) return null;

  return { step, total };
}

/** 底部那行进度怎么说：没读到就返回 null，让调用方保持安静。 */
export function progressText(progress: RunProgress | null): string | null {
  if (!progress) return null;
  return `第 ${progress.step}/${progress.total} 步`;
}
