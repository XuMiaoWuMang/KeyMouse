import './hud.css';
import { invoke } from '@tauri-apps/api/core';
import { hudRows, statusText, type HudRow } from './lib/hud';

/*
  运行日志 HUD：左上角一块透明、不可点击的置顶窗。

  它不是浮条：没有计时器、没有按钮、也不接受鼠标。主窗口藏起来以后，用户仍然能
  看见最近几步在做什么；要停就按 Ctrl+Alt+Q（播放时由外壳注册全局键，录制时由
  命令行自己听）。鼠标事件由窗口样式整体穿透，不跟目标程序抢点击。
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
let lines: string[] = [];

/** 不让日志里的窗口标题/输入内容有机会被当成 HTML。 */
function rowElement(row: HudRow, current: boolean): HTMLElement {
  const element = document.createElement('div');
  element.className = `row ${row.status}${current ? ' current' : ''}`;

  const counter = document.createElement('span');
  counter.className = 'counter';
  counter.textContent = `[${row.index}/${row.total}]`;

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
  if (rows.length === 0) {
    const empty = document.createElement('p');
    empty.className = 'empty';
    empty.textContent = '等待第一步…';
    rowsEl.append(empty);
    tallyEl.textContent = '';
    return;
  }

  rows.forEach((row, index) => rowsEl.append(rowElement(row, index === rows.length - 1)));
  const last = rows[rows.length - 1];
  tallyEl.textContent = `${last.index}/${last.total}`;
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
