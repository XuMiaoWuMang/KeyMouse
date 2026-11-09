import './pill.css';
import { invoke } from '@tauri-apps/api/core';
import { getCurrentWindow } from '@tauri-apps/api/window';

/*
  录制 / 播放浮条。

  主窗口在开始录制或播放时隐藏，屏幕上只留这一条：一个呼吸点、走过的时长、
  播放时多一个暂停按钮，最后一个停止按钮。为什么不留文字：这条东西会一直悬在
  屏幕上，字越多越像一块贴纸；"暂停""停止"各有公认的图形，多写一行说明只是噪声。

  录制与播放的停止走不同命令：录制要等它自己写盘（`pill_stop`），播放可以直接
  `run_cancel`。所以模式从 URL 的 `?mode=` 读，Rust 建窗时写死。
*/

const mode = new URLSearchParams(location.search).get('mode') === 'play' ? 'play' : 'record';
const isPlay = mode === 'play';
const root = document.getElementById('pill')!;

root.innerHTML = `
  <div class="pill ${mode}">
    <span class="dot" aria-hidden="true"></span>
    <span class="elapsed" id="elapsed" title="${isPlay ? '已播放时长' : '已录时长'}">0:00</span>
    ${isPlay ? `
      <button class="pause" id="pause" type="button" aria-label="暂停" title="暂停">
        <span class="glyph" aria-hidden="true"></span>
      </button>` : ''}
    <button class="stop" id="stop" type="button" aria-label="停止" title="停止（Ctrl+Alt+Q 也行）">
      <span class="glyph" aria-hidden="true"></span>
    </button>
  </div>
`;

const elapsedEl = document.getElementById('elapsed')!;
const stopEl = document.getElementById('stop') as HTMLButtonElement;
const pauseEl = document.getElementById('pause') as HTMLButtonElement | null;
const pillEl = root.querySelector('.pill') as HTMLElement;

/** 秒数只用于显示，真正的时间以宿主为准；这里只做本地递增，不询问命令行。 */
let seconds = 0;
let timer: number | undefined;
let paused = false;

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
    if (paused) return;
    seconds += 1;
    paint();
  }, 1000);
}

function fail(message: string): void {
  pillEl.classList.add('failed');
  stopEl.title = message;
}

stopEl.addEventListener('click', (event) => {
  event.stopPropagation();
  stopEl.disabled = true;
  stopEl.title = '正在收尾…';
  const action = isPlay
    ? invoke('run_cancel').then(() => invoke('hide_pill'))
    : invoke('pill_stop');
  void action.catch((error: unknown) => {
    stopEl.disabled = false;
    fail(isPlay ? '没停成，请按 Ctrl+Alt+Q' : '没停成，请按 Ctrl+Alt+Q');
    console.error('浮条停止失败', error);
  });
});

pauseEl?.addEventListener('click', (event) => {
  event.stopPropagation();
  const next = !paused;
  pauseEl.disabled = true;
  void invoke<boolean>('pause_flow', { paused: next })
    .then((accepted) => {
      // 没有在跑的流程时命令返回 false：别让按钮假装切了状态。
      if (!accepted) {
        fail('没有在跑的流程');
        return;
      }
      paused = next;
      pillEl.classList.toggle('paused', paused);
      pauseEl.classList.toggle('on', paused);
      pauseEl.title = paused ? '继续' : '暂停';
      pauseEl.setAttribute('aria-label', paused ? '继续' : '暂停');
    })
    .catch((error: unknown) => {
      fail('暂停失败');
      console.error('浮条暂停失败', error);
    })
    .finally(() => {
      pauseEl.disabled = false;
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
