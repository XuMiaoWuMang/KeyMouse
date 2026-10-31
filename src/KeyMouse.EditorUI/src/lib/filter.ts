import type { FlowStep } from './model';
import { childSteps, stepSummary, stepTitle } from './model';

/**
 * 列表过滤：按类型名、标题、摘要里的字找一步。
 *
 * 三件事分开写，是因为它们的判断标准不一样：
 * - `searchText` 只关心"这一步看起来是什么样"，与内部字段无关——用户找的是他看得见的那行字；
 * - `matchesFilter` 允许"父命中"：搜"重复"时那一组里的三步都该露出来，否则展开了看不见内容；
 * - `childVisible` 决定子步骤的可见性：父命中看全部，父没命中就只看自己命中的。
 */

/** 一行里所有能被肉眼看到的字，小写。 */
export function searchText(step: FlowStep): string {
  return `${stepTitle(step.type)} ${stepSummary(step)} ${step.type}`.toLowerCase();
}

/** 这一步（含它的子步骤）是否该在列表里出现。 */
export function matchesFilter(step: FlowStep, query: string): boolean {
  const needle = query.trim().toLowerCase();
  if (needle.length === 0) return true;
  if (searchText(step).includes(needle)) return true;
  return childSteps(step).some((child) => searchText(child).includes(needle));
}

/** 父已经命中时子步骤全显示；否则只显示自己命中的那些。 */
export function childVisible(step: FlowStep, child: FlowStep, query: string): boolean {
  const needle = query.trim().toLowerCase();
  if (needle.length === 0) return true;
  if (searchText(step).includes(needle)) return true;
  return searchText(child).includes(needle);
}

/** 顶层列表用：过滤后仍然是原来的顺序，编号不会因此改变。 */
export function filterSteps(steps: FlowStep[], query: string): FlowStep[] {
  return steps.filter((step) => matchesFilter(step, query));
}
