import './pill.css';
import { invoke } from '@tauri-apps/api/core';
import { getCurrentWindow } from '@tauri-apps/api/window';

/*
  录制浮条的界面。

  宿主（src-tauri/src/pill.rs）负责：建透明窗口、置顶、不抢焦点、摆位置、把已录时长推过来。
  这里只做三件事：显示时长、把「停止」接上、把空白处按住变成拖动窗口。
*/

const root = document.getElementById('pill')!;

root.innerHTML = `
  <div class="pill">
    <span class="dot" aria-hidden="true"></span>
    <span class="title">正在录制</span>
    <span class="elapsed" id="elapsed">0 秒</span>
    <span class="hint" id="hint">操作你要录的窗口，Esc 或按钮结束</span>
    <button class="stop" id="stop" type="button">停止</button>
  </div>
`;

const elapsedEl = document.getElementById('elapsed')!;
const hintEl = document.getElementById('hint')!;
const stopEl = document.getElementById('stop') as HTMLButtonElement;

/** 秒数只用于显示，真正的时间以宿主为准；这里只做本地递增，不询问命令行。 */
let seconds = 0;
let timer: number | undefined;

function paint(): void {
  elapsedEl.textContent = `已录 ${seconds} 秒`;
}

function startTicking(): void {
  window.clearInterval(timer);
  timer = window.setInterval(() => {
    seconds += 1;
    paint();
  }, 1000);
}

stopEl.addEventListener('click', (event) => {
  event.stopPropagation();
  stopEl.disabled = true;
  hintEl.textContent = '正在收尾…';
  void invoke('pill_stop').catch((error: unknown) => {
    stopEl.disabled = false;
    hintEl.textContent = '停止没成功，请用 Ctrl+Alt+Q';
    console.error('pill_stop 失败', error);
  });
});

// 按住药丸空白处 = 拖窗口。按钮在冒泡前已经 stopPropagation，不会被拖。
root.addEventListener('pointerdown', (event) => {
  const target = event.target as HTMLElement;
  if (target.closest('button')) {
    return;
  }
  void getCurrentWindow().startDragging();
});

paint();
startTicking();
