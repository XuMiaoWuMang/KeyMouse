import './hud.css';
import { invoke } from '@tauri-apps/api/core';
import { hudRows, recordingState, statusText, type HudRow, type RecordingState } from './lib/hud';

/*
  运行日志 HUD：左上角一块透明、不可点击的置顶窗。

  它不是浮条：没有计时器、没有按钮、也不接受鼠标。主窗口藏起来以后，用户仍然能
  看见最近几步在做什么；要停就按 Ctrl+Alt+Q（播放与普通录制由外壳注册全局键，
  提权录制由命令行自己听）。鼠标事件由窗口样式整体穿透，不跟目标程序抢点击。
*/

const root = document.getElementById('hud')!;
const body = document.createElement('div');
body.className = 'hud';
body.innerHTML = `
  <div class="head">
    <span class="pulse" aria-hidden="true"></span>
    <span class="title">运行日志</span>
    <span class="tally" id="tally"></span>
  </div>
  <div class="rows" id="rows" role="log" aria-live="polite"></div>
`;
root.append(body);

const rowsEl = document.getElementById('rows')!;
const tallyEl = document.getElementById('tally')!;
const titleEl = body.querySelector('.title') as HTMLElement;
let lines: string[] = [];

/** 录制没有步骤行，给一块状态卡，别让人对着“等待第一步…”猜。 */
function paintRecording(state: RecordingState): void {
  const card = document.createElement('div');
  card.className = `recording ${state.active ? 'active' : 'stopped'}`;

  const lead = document.createElement('div');
  lead.className = 'recording-lead';
  lead.textContent = state.active ? '正在录制' : `已记录 ${state.steps ?? 0} 步`;

  const hint = document.createElement('div');
  hint.className = 'recording-hint';
  hint.textContent = state.active
    ? 'Ctrl+Alt+Q 停止 · 连按两次 Esc 也可停'
    : '流程文件已保存';

  const detail = document.createElement('div');
  detail.className = 'recording-detail';
  detail.textContent = state.active
    ? '点击、拖动、输入会记录成流程步骤'
    : '停止键已生效，录制窗口即将关闭';

  card.append(lead, hint, detail);
  rowsEl.append(card);
  titleEl.textContent = state.active ? '录制日志' : '录制结束';
  tallyEl.textContent = state.active ? '●' : state.steps === null ? '' : `${state.steps} 步`;
}

/** 不让日志里的窗口标题/输入内容有机会被当成 HTML。 */
function rowElement(row: HudRow, current: boolean): HTMLElement {
  const element = document.createElement('div');
  element.className = `row ${row.status}${current ? ' current' : ''}`;

  const counter = document.createElement('span');
  counter.className = 'counter';
  counter.textContent = row.total === null ? `[${row.index}]` : `[${row.index}/${row.total}]`;

  const name = document.createElement('span');
  name.className = 'name';
  name.textContent = row.name;

  const colon = document.createElement('span');
  colon.className = 'colon';
  colon.textContent = ':';

  const detail = document.createElement('span');
  detail.className = 'detail';
  detail.textContent = row.detail || '—';

  const status = document.createElement('span');
  status.className = 'status';
  status.textContent = statusText(row.status);

  element.append(counter, name, colon, detail, status);
  return element;
}

function paint(): void {
  const rows = hudRows(lines);
  rowsEl.replaceChildren();
  const recording = recordingState(lines);
  if (rows.length === 0 && recording) {
    paintRecording(recording);
    return;
  }
  titleEl.textContent = '运行日志';
  if (rows.length === 0) {
    const empty = document.createElement('p');
    empty.className = 'empty';
    empty.textContent = '等待第一步…';
    rowsEl.append(empty);
    tallyEl.textContent = '';
    return;
  }

  const append = (row: HudRow, target: HTMLElement): void => {
    const group = document.createElement('div');
    group.className = 'group';
    group.append(rowElement(row, row.status === 'current'));
    if (row.children?.length) {
      const children = document.createElement('div');
      children.className = 'children';
      row.children.forEach(child => append(child, children));
      group.append(children);
    }
    target.append(group);
  };
  rows.forEach(row => append(row, rowsEl));
  const last = rows[rows.length - 1];
  tallyEl.textContent = last.total === null ? `${last.index}` : `${last.index}/${last.total}`;
}

/**
 * 轮询后端的环形缓冲，而不是订阅事件。
 *
 * HUD 是第二个 WebView，挂载时间比流程启动晚；事件只发给已经订阅的页面，第一条
 * “执行中”会丢，而一个 60 秒的等待步骤后面没有第二条可补。轮询每次拿完整快照，
 * 晚挂载也能立刻看到当前进度，窗口被关闭后定时器自然停止。
 */
async function refresh(): Promise<void> {
  try {
    const snapshot = await invoke<string[]>('hud_lines');
    if (snapshot.length === lines.length && snapshot.every((line, index) => line === lines[index])) {
      return;
    }
    lines = snapshot;
    paint();
  } catch (error) {
    tallyEl.textContent = `日志读取失败：${String(error)}`;
  }
}

paint();
void refresh();
setInterval(() => void refresh(), 250);
