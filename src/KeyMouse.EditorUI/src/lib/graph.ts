/**
 * 流程图连线：从步骤树的**控制流语义**生成边，而不是按画布上谁挨着谁。
 *
 * 这一步只产生路径（from/to）和标签；几何在 FlowCanvas 里算，所以自由拖动节点后
 * 箭头会跟着重画。约定：
 *
 *   - 普通步骤 → 同序列下一步（最后一步 → 外层继续的位置）
 *   - if      → 成立时首步 / 否则首步；两条分支的末步都回到 if 之后的下一步
 *   - while   → 循环体首步；循环体末步同时指向 while 开头和循环后的下一步
 *   - 其他循环同样有「进入 / 返回 / 退出」三条语义边
 *   - break 只画到循环之后的下一步；continue 只画回循环开头
 *   - return 就地结束当前流程，不画后续边
 *
 * 这里不处理「节点放在哪」：坐标由画布负责，所以自由拖动后箭头会跟着重画。
 */

import { branchesOf, type BranchKey, type FlowStep } from './model';
import { pathKey, type NodePath } from './tree';

export type GraphEdgeKind = 'next' | 'true' | 'false' | 'loop' | 'back' | 'exit';

export interface GraphEdge {
  from: NodePath;
  to: NodePath;
  kind: GraphEdgeKind;
  label?: string;
}

/** 同一条语义边在重画前后的稳定键；手工接口按它保存。 */
export function graphEdgeKey(edge: Pick<GraphEdge, 'from' | 'to' | 'kind'>): string {
  return `${pathKey(edge.from)}>${pathKey(edge.to)}:${edge.kind}`;
}

/** 一条序列最后落到哪里；break/continue 的落点由外层循环接管。 */
interface Exit {
  path: NodePath;
  terminal: 'normal' | 'break' | 'continue' | 'return';
}

interface LoopContext {
  /** 循环节点本身：continue 回到这里。 */
  header: NodePath;
  /** 循环之后的下一步：break 跳到这里；没有下一步时为 null。 */
  exit: NodePath | null;
}

const LOOP_TYPES = new Set(['while', 'do', 'for', 'repeat', 'foreach']);
const BREAK_TYPES = new Set(['break']);
const CONTINUE_TYPES = new Set(['continue']);
const RETURN_TYPES = new Set(['return']);

function childPath(parent: NodePath, branch: BranchKey, index: number): NodePath {
  return [...parent, { branch, index }];
}

/**
 * 递归遍历一条序列，边直接写进 `edges`。
 * 返回这条序列的入口（首步）和末端节点（最后真正会落到的节点），供循环回边和分支汇合使用。
 */
function walkSequence(
  items: readonly FlowStep[],
  parent: NodePath,
  branch: BranchKey,
  continuation: NodePath | null,
  loop: LoopContext | null,
  edges: GraphEdge[],
): { entry: NodePath | null; exits: Exit[] } {
  if (items.length === 0) return { entry: null, exits: [] };

  let entry: NodePath | null = null;
  let exits: Exit[] = [];

  for (let index = 0; index < items.length; index += 1) {
    const step = items[index];
    const path = childPath(parent, branch, index);
    if (!entry) entry = path;
    const next = index + 1 < items.length ? childPath(parent, branch, index + 1) : continuation;

    if (step.type === 'if') {
      const branches = branchesOf(step);
      // 分支是 if 的替代出口，不能把 if 之前那条普通的「继续」也算进来。
      // 早先这里直接 push 到外层 exits，会让循环体把前面每一步都当出口，
      // 画出一串从半路"返回"的虚线。
      const branchExits: Exit[] = [];
      for (const item of branches) {
        const child = walkSequence(item.steps, path, item.key, next, loop, edges);
        if (child.entry) {
          edges.push({
            from: path,
            to: child.entry,
            kind: item.key === 'else' ? 'false' : 'true',
            label: item.key === 'else' ? '否则' : '成立',
          });
          branchExits.push(...child.exits);
        } else if (next) {
          // 空分支没有节点可以落箭头，于是直接从 if 连到汇合点；
          // 汇合点也要记进 exits，外层循环才知道这条路会走到哪里。
          edges.push({
            from: path,
            to: next,
            kind: item.key === 'else' ? 'false' : 'true',
            label: item.key === 'else' ? '否则' : '成立',
          });
          branchExits.push({ path: next, terminal: 'normal' });
        }
      }
      exits = branchExits.length > 0 ? branchExits : next ? [{ path: next, terminal: 'normal' }] : [];
      // 两条分支都 return 时，if 之后的节点已经不在可执行路径上。
      if (branchExits.length > 0 && branchExits.every((exit) => exit.terminal === 'return')) break;
      continue;
    }

    if (LOOP_TYPES.has(step.type)) {
      const body = branchesOf(step).find((item) => item.key === 'steps');
      const bodySteps = body?.steps ?? [];
      const bodyWalk = walkSequence(
        bodySteps,
        path,
        'steps',
        null,
        { header: path, exit: next },
        edges,
      );
      if (bodyWalk.entry) {
        edges.push({ from: path, to: bodyWalk.entry, kind: 'loop', label: '循环' });
      }
      // 循环体最后一步有两个出口：回到循环开头，或落到循环后面的下一步。
      // break/continue 已经自己连了目标，不能再补一条"从半路返回"的线。
      for (const exit of bodyWalk.exits) {
        if (exit.terminal !== 'normal') continue;
        edges.push({ from: exit.path, to: path, kind: 'back', label: '返回' });
        if (next) edges.push({ from: exit.path, to: next, kind: 'exit', label: '退出' });
      }
      // 循环体有内容时，"离开循环"画在最后一步上；没有内容时才由循环节点自己连出去。
      // 两条都画会让同一个出口出现两次"结束/退出"，线上标签也会叠在一起。
      if (next && !bodyWalk.entry) edges.push({ from: path, to: next, kind: 'exit', label: '结束' });
      exits = next
        ? [{ path: next, terminal: 'normal' }]
        : bodyWalk.exits.some((item) => item.terminal === 'normal')
          ? bodyWalk.exits.filter((item) => item.terminal === 'normal')
          : [{ path, terminal: 'normal' }];
      continue;
    }

    if (BREAK_TYPES.has(step.type)) {
      if (loop?.exit) edges.push({ from: path, to: loop.exit, kind: 'exit', label: '跳出' });
      exits = [{ path, terminal: 'break' }];
      continue;
    }

    if (CONTINUE_TYPES.has(step.type)) {
      if (loop) edges.push({ from: path, to: loop.header, kind: 'back', label: '继续' });
      exits = [{ path, terminal: 'continue' }];
      continue;
    }

    // return 没有任何后续：同序列后面的步骤在这个流程里已经不可达。
    if (RETURN_TYPES.has(step.type)) {
      exits = [{ path, terminal: 'return' }];
      break;
    }

    // 普通步骤：直接连到同序列下一步；最后一步连到外层继续的位置。
    if (next) edges.push({ from: path, to: next, kind: 'next' });
    exits = [{ path, terminal: 'normal' }];
  }

  return { entry, exits };
}

/** 把整棵步骤树变成有方向的边；画布只负责按节点位置画线。 */
export function buildGraph(steps: readonly FlowStep[]): GraphEdge[] {
  const edges: GraphEdge[] = [];
  walkSequence(steps, [], 'steps', null, null, edges);
  return edges;
}
