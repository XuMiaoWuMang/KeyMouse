/**
 * 把流程树铺到无限画布上。
 *
 * 版式：**从上到下是顺序，往右一格是"进入某个块"**。
 * 顶层动作沿一条竖线往下排；块的子步骤在它右边展开，`if` 的成立时/否则并排。
 * 这是流程图的常规读法，也符合"先看到下一步，再看到分支"的顺序。
 *
 * 这里只管几何，不碰 DOM：节点、分支标签、空分支按钮、连线、可插入的缝隙都是数据。
 * 画布组件负责缩放/平移/拖动，App 负责把拖动结果写回流程树。
 */

import { branchesOf, type FlowStep, type StepCategory } from './model';
import { categoryOf } from './steps';
import { originOf, type NodePath } from './tree';

export const NODE_W = 208;
export const NODE_H = 82;
export const NODE_GAP = 20;
export const BRANCH_INDENT = 72;
export const BRANCH_GAP = 32;
export const BRANCH_LABEL_H = 24;
export const EMPTY_H = 40;
export const PAD = 48;

export interface CanvasNode {
  path: NodePath;
  parentPath: NodePath;
  branch: 'steps' | 'else';
  index: number;
  step: FlowStep;
  depth: number;
  x: number;
  y: number;
  w: number;
  h: number;
  origin: string;
  category: StepCategory;
}

export interface CanvasBranchLabel {
  parentPath: NodePath;
  branch: 'steps' | 'else';
  text: string;
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface CanvasEmpty {
  parentPath: NodePath;
  branch: 'steps' | 'else';
  text: string;
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface CanvasEdge {
  d: string;
  kind: 'next' | 'branch';
}

/**
 * 同一父节点、同一分支里两个相邻步骤之间的一条缝。
 * 所有序列都从上往下，所以插入线一律横着画。
 */
export interface CanvasGap {
  parentPath: NodePath;
  branch: 'steps' | 'else';
  index: number;
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface CanvasLayout {
  nodes: CanvasNode[];
  labels: CanvasBranchLabel[];
  empties: CanvasEmpty[];
  edges: CanvasEdge[];
  gaps: CanvasGap[];
  width: number;
  height: number;
}

function verticalLine(x1: number, y1: number, x2: number, y2: number): string {
  return `M ${x1} ${y1} V ${y2}`;
}

/** 从块的底边拐到一条分支的入口；拐点放在两者之间，读起来像一条电路。 */
function branchLine(x1: number, y1: number, x2: number, y2: number): string {
  const mid = y1 + Math.max(10, (y2 - y1) / 2);
  return `M ${x1} ${y1} V ${mid} H ${x2} V ${y2}`;
}

export function layoutFlow(steps: readonly FlowStep[]): CanvasLayout {
  const nodes: CanvasNode[] = [];
  const labels: CanvasBranchLabel[] = [];
  const empties: CanvasEmpty[] = [];
  const edges: CanvasEdge[] = [];
  const gaps: CanvasGap[] = [];
  let maxX = PAD;
  let maxY = PAD;

  const layoutSequence = (
    items: readonly FlowStep[],
    parentPath: NodePath,
    branch: 'steps' | 'else',
    x: number,
    y: number,
    depth: number,
  ): number => {
    let cursor = y;
    let previous: CanvasNode | null = null;

    items.forEach((step, index) => {
      const path: NodePath = [...parentPath, { branch, index }];
      const node: CanvasNode = {
        path,
        parentPath,
        branch,
        index,
        step,
        depth,
        x,
        y: cursor,
        w: NODE_W,
        h: NODE_H,
        origin: originOf(steps, path),
        category: categoryOf(step.type),
      };
      nodes.push(node);
      maxX = Math.max(maxX, x + NODE_W);
      maxY = Math.max(maxY, cursor + NODE_H);

      if (previous) {
        edges.push({
          d: verticalLine(previous.x + previous.w / 2, previous.y + previous.h, node.x + node.w / 2, node.y),
          kind: 'next',
        });
      }
      previous = node;

      gaps.push({
        parentPath,
        branch,
        index,
        x,
        y: cursor - NODE_GAP / 2,
        w: NODE_W,
        h: NODE_GAP,
      });

      const branches = branchesOf(step);
      let next = cursor + NODE_H + NODE_GAP;

      if (branches.length > 0) {
        const labelY = cursor + NODE_H + NODE_GAP;
        let bottom = labelY + BRANCH_LABEL_H;
        maxY = Math.max(maxY, bottom);

        branches.forEach((item, branchIndex) => {
          const bx = x + BRANCH_INDENT + branchIndex * (NODE_W + BRANCH_GAP);
          const labelX = bx + NODE_W / 2;
          labels.push({
            parentPath: path,
            branch: item.key,
            text: item.label,
            x: bx,
            y: labelY,
            w: NODE_W,
            h: BRANCH_LABEL_H,
          });
          maxX = Math.max(maxX, bx + NODE_W);
          edges.push({
            d: branchLine(x + NODE_W / 2, cursor + NODE_H, labelX, labelY),
            kind: 'branch',
          });

          const childY = labelY + BRANCH_LABEL_H;
          if (item.steps.length > 0) {
            bottom = Math.max(bottom, layoutSequence(item.steps, path, item.key, bx, childY, depth + 1));
          } else {
            empties.push({
              parentPath: path,
              branch: item.key,
              text: item.label,
              x: bx,
              y: childY,
              w: NODE_W,
              h: EMPTY_H,
            });
            maxY = Math.max(maxY, childY + EMPTY_H);
            bottom = Math.max(bottom, childY + EMPTY_H);
            edges.push({
              d: verticalLine(labelX, labelY + BRANCH_LABEL_H, labelX, childY),
              kind: 'branch',
            });
          }
        });

        next = bottom + NODE_GAP;
      }

      cursor = next;
      maxY = Math.max(maxY, cursor);
    });

    gaps.push({
      parentPath,
      branch,
      index: items.length,
      x,
      y: Math.max(0, cursor - NODE_GAP / 2),
      w: NODE_W,
      h: NODE_GAP,
    });

    return cursor;
  };

  layoutSequence(steps, [], 'steps', PAD, PAD, 0);

  return {
    nodes,
    labels,
    empties,
    edges,
    gaps,
    width: Math.max(PAD * 2, maxX + PAD),
    height: Math.max(PAD * 2, maxY + PAD),
  };
}

