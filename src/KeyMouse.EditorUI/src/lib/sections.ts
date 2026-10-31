/**
 * 纯界面分组（"操作条"的分组头）。
 *
 * 这一层**不进流程文件、不参与执行**：流程 JSON 里只有一条条步骤，分组是编辑器
 * 为了方便看而加的抽屉。所以它存在界面的账本里（`%LOCALAPPDATA%\KeyMouse\editor-settings.json`），
 * 命令行完全不知道它的存在——这一点是刻意保住的性质：文件换台机器、换命令行版本都能跑。
 *
 * 表达方式刻意选"边界"而不是"成员列表"：
 *
 * - 分组是**连续区间**。`Section.at` 表示"在第 at 个步骤之前开一个新分组"，一直管到下一个
 *   边界之前。没有成员 id，就不用担心插入/删除后 id 失效；
 * - 拖分组头 = 把整段连续步骤搬到另一处，天然满足"子步骤跟着走、相对顺序不变"；
 * - 数组长度永远等于分组数，删空时整份清掉即可。
 *
 * 代价：分组只能是连续的。跨组混排要拆成多个分组——这正好也是"分组头"这个隐喻本身
 * 的限制，不打算绕开。
 */

/** 一个分组的开头：`at` 是它管到的第一个步骤下标（0 基）。 */
export interface Section {
  /** 第一个步骤的下标；0 表示从第一行开始。 */
  at: number;
  /** 组名。**空标题 = 一条不带字的分隔线**：拖动分组时为了保住成员，会补一条这样的尾巴。 */
  title: string;
}

/**
 * 把任意来源的分组整理成"能用的"：去掉越界/重复、按下标排序。
 *
 * 标题**不兜底**：空标题是有意义的——它表示"这里只是分隔线，不用起名"。
 * 拖动分组时自动补的那条尾巴就用空标题，免得列表里突然冒出一个"分组 2"。
 */
export function normalizeSections(input: readonly Section[], stepCount: number): Section[] {
  const seen = new Set<number>();
  const cleaned: Section[] = [];
  for (const section of input) {
    const at = Math.trunc(section.at);
    if (!Number.isFinite(at) || at < 0 || at > stepCount) continue;
    if (seen.has(at)) continue;
    seen.add(at);
    cleaned.push({ at, title: section.title.trim() });
  }
  return cleaned.sort((a, b) => a.at - b.at);
}

/** 某个步骤属于哪个分组（返回那个分组的开始下标）；不在任何分组里返回 null。 */
export function sectionOf(sections: readonly Section[], index: number): number | null {
  let current: number | null = null;
  for (const section of sections) {
    if (section.at <= index) current = section.at;
    else break;
  }
  return current;
}

/** `at` 这个分组覆盖的步骤区间 `[start, end)`。 */
export function spanOf(sections: readonly Section[], at: number, stepCount: number): [number, number] {
  const start = Math.max(0, Math.min(at, stepCount));
  const next = sections.find((section) => section.at > start);
  const end = next ? Math.min(next.at, stepCount) : stepCount;
  return [start, end];
}

/**
 * 插入一步之后，边界怎么挪。
 *
 * - 插在边界处（`index === at`）算"这一行进了这个分组"，边界不动；
 * - 插在边界之前，边界右移一格。
 */
export function shiftAfterInsert(sections: readonly Section[], index: number): Section[] {
  return sections.map((section) => (section.at > index ? { ...section, at: section.at + 1 } : section));
}

/** 删掉一步之后：边界在它之后左移一格；刚好等于它（删掉了分组的第一行）就保持，指到新的第一行。 */
export function shiftAfterDelete(sections: readonly Section[], index: number): Section[] {
  return sections
    .map((section) => (section.at > index ? { ...section, at: section.at - 1 } : section))
    .filter((section) => section.at >= 0);
}

/**
 * 拖分组头：把 `[start, end)` 这段搬到 `target`（"放到第 target 个步骤之前"）。
 *
 * 返回新步骤数组与新分组表。分组头自己跟着块走，其它边界按"插入点在某组内/外"重新编号。
 * 目标落在自己这一段里等于没动。
 */
export function moveSection<T>(
  steps: readonly T[],
  sections: readonly Section[],
  at: number,
  target: number,
): { steps: T[]; sections: Section[] } {
  const [start, end] = spanOf(sections, at, steps.length);
  const size = end - start;
  const to = Math.max(0, Math.min(target, steps.length));
  if (to >= start && to <= start + size) {
    return { steps: steps.slice(), sections: sections.slice() };
  }
  const block = steps.slice(start, end);
  const rest = [...steps.slice(0, start), ...steps.slice(end)];
  const insertAt = to > end ? to - size : to;
  const nextSteps = [...rest.slice(0, insertAt), ...block, ...rest.slice(insertAt)];

  // 边界的位置要按"它在剩下的数组里落到哪、再让插入的块挤一下"来重新算：
  // 先去掉这一段自己的边界，把其余边界换算到 rest 里的下标，再按插入点让路。
  const kept = sections.filter((section) => section.at < start || section.at >= end);
  const title = sections.find((section) => section.at === start)?.title ?? '分组';
  const moved = kept.map((section) => {
    const inRest = section.at < start ? section.at : section.at - size;
    const afterInsert = inRest >= insertAt ? inRest + size : inRest;
    return { ...section, at: afterInsert };
  });
  const nextSections = normalizeSections(
    [...moved, { at: insertAt, title }].map((section) => ({ ...section })),
    nextSteps.length,
  );

  // 被搬的这一段原来是"到末尾为止"的（后面没有边界）。搬走之后如果后面还有行，
  // 就在它的新结尾补一条**没有标题的分隔线**，否则那些行会被它顺手吞进组里——
  // 拖一个组不该把别的步骤卷进来。
  const movedEnd = insertAt + size;
  if (end === steps.length && movedEnd < nextSteps.length && !nextSections.some((section) => section.at === movedEnd)) {
    return {
      steps: nextSteps,
      sections: normalizeSections([...nextSections, { at: movedEnd, title: '' }], nextSteps.length),
    };
  }
  return { steps: nextSteps, sections: nextSections };
}

/** 给"从这里分一组"用：在 `index` 处加一个边界；已存在就只改标题。 */
export function addSection(sections: readonly Section[], index: number, title: string): Section[] {
  return normalizeSections([...sections.filter((section) => section.at !== index), { at: index, title }], Number.MAX_SAFE_INTEGER);
}

/** 去掉一个分组头（组里的步骤原样留在列表里）。 */
export function removeSection(sections: readonly Section[], at: number): Section[] {
  return sections.filter((section) => section.at !== at);
}
