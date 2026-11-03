import './pill.css';
import { invoke } from '@tauri-apps/api/core';
import { getCurrentWindow } from '@tauri-apps/api/window';

/*
  录制浮条的界面。

  只放三样东西，全是图：一个呼吸的红点（正在录）、一段走过的时长、一个方块按钮（停）。
  为什么不留文字：这条东西会一直悬在屏幕上，字越多越像一块贴纸；而"录制中"和"停"
  本来就各有公认的图形，多写一行说明只是噪声。

  宿主（src-tauri/src/pill.rs）负责：建透明窗口、置顶、不抢焦点、摆位置。
  这里只做三件事：显示时长、把方块接上停止、把空白处按住变成拖动窗口。
*/

const root = document.getElementById('pill')!;

root.innerHTML = `
  <div class="pill">
    <span class="dot" aria-hidden="true"></span>
    <span class="elapsed" id="elapsed" title="已录时长">0:00</span>
    <button class="stop" id="stop" type="button" aria-label="停止录制" title="停止录制（Esc 也行）">
      <span class="glyph" aria-hidden="true"></span>
    </button>
  </div>
`;

const elapsedEl = document.getElementById('elapsed')!;
const stopEl = document.getElementById('stop') as HTMLButtonElement;
const pillEl = root.querySelector('.pill') as HTMLElement;

/** 秒数只用于显示，真正的时间以宿主为准；这里只做本地递增，不询问命令行。 */
let seconds = 0;
let timer: number | undefined;

/** `0:07` / `12:34`——比"已录 7 秒"短一半，而且宽度稳定，不会让药丸一跳一跳。 */
function clock(total: number): string {
  const minutes = Math.floor(total / 60);
  const rest = total % 60;
  return `${minutes}:${String(rest).padStart(2, '0')}`;
}

function paint(): void {
  elapsedEl.textContent = clock(seconds);
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
  stopEl.title = '正在收尾…';
  void invoke('pill_stop').catch((error: unknown) => {
    stopEl.disabled = false;
    // 没有文字区可写，就用"红点变暗 + 悬停提示"这两条安静的路子告诉人没停成。
    pillEl.classList.add('failed');
    stopEl.title = '没停成，请按 Ctrl+Alt+Q';
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
