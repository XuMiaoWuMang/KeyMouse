<script lang="ts">
  /* 设置页：按 guizang-page-skill 的杂志风排版（报头 + 章节花饰编号 + 三线表 + 方框标签），
   * 但每一行都能改。
   *
   * 为什么单独一个组件而不是塞进 App.svelte：那个文件已经三千多行，设置页是一块
   * 完全独立的"纸张"，它只需要 runtime（显示版本/环境）和两个回调。
   */
  import { onMount, createEventDispatcher } from 'svelte';
  import { invoke } from '@tauri-apps/api/core';
  import Icon from './lib/Icon.svelte';

  type RuntimeInfo = {
    cliPath: string | null;
    cliVersion: string | null;
    environment: 'development' | 'installed' | 'unknown';
    repoRoot: string | null;
    editorExe: string;
    elevated: boolean;
    initialPath: string | null;
  };

  export let runtime: RuntimeInfo | null = null;

  const dispatch = createEventDispatcher<{ close: void; theme: string }>();

  type ThemeId = 'paper' | 'kraft' | 'night';

  const THEMES: { id: ThemeId; name: string; note: string }[] = [
    { id: 'paper', name: '纸白', note: '米白纸、暖黑墨，默认' },
    { id: 'kraft', name: '牛皮纸', note: '更深一档，适合长时间读流程' },
    { id: 'night', name: '午夜墨', note: '夜间；强调色换暖金以便看清' },
  ];

  let theme: ThemeId = 'paper';
  let themes: string[] = [];
  let recent: string[] = [];
  let doctor = '';
  let doctorBusy = false;
  let diagnosticsPath = '';
  let hint = '';
  let hintError = false;

  function say(text: string, error = false): void {
    hint = text;
    hintError = error;
  }

  onMount(async () => {
    try {
      themes = await invoke<string[]>('theme_options');
    } catch {
      themes = THEMES.map((t) => t.id);
    }
    // 账本里的主题可能不在这份清单里（老版本），认不出就按纸白。
    try {
      const saved = await invoke<string>('theme');
      theme = (THEMES.find((t) => t.id === saved)?.id ?? 'paper') satisfies ThemeId;
    } catch {
      theme = 'paper';
    }
    applyTheme(theme);

    try {
      recent = await invoke<string[]>('recent_files');
    } catch {
      recent = [];
    }
  });

  function applyTheme(id: ThemeId): void {
    document.documentElement.dataset.theme = id;
  }

  async function pickTheme(id: ThemeId): Promise<void> {
    theme = id;
    applyTheme(id);
    dispatch('theme', id);
    try {
      const applied = await invoke<string>('save_theme', { id });
      if (applied !== id) say('这个主题没认出来，已退回默认', true);
      else say('');
    } catch (e) {
      say(`主题没存下来：${e}`, true);
    }
  }

  async function openRecent(path: string): Promise<void> {
    dispatch('close');
    // 交给主界面打开：这里只管"请求"，不重复一遍打开流程。
    window.dispatchEvent(new CustomEvent('keymouse:open-path', { detail: path }));
  }

  async function forget(path: string): Promise<void> {
    try {
      recent = await invoke<string[]>('forget_recent', { path });
      say('已从最近列表里去掉');
    } catch (e) {
      say(`没去掉：${e}`, true);
    }
  }

  async function clearRecent(): Promise<void> {
    try {
      recent = await invoke<string[]>('clear_recent');
      say('最近列表清空了');
    } catch (e) {
      say(`没清掉：${e}`, true);
    }
  }

  async function copyCli(): Promise<void> {
    if (!runtime?.cliPath) return;
    try {
      await navigator.clipboard.writeText(runtime.cliPath);
      say('命令行路径已复制');
    } catch {
      say('剪贴板不可用，可以手动选中这一行', true);
    }
  }

  async function openFolder(where: 'settings' | 'cli'): Promise<void> {
    const { openPath, revealItemInDir } = await import('@tauri-apps/plugin-opener');
    try {
      if (where === 'cli' && runtime?.cliPath) await revealItemInDir(runtime.cliPath);
      else await openPath(settingsDir());
    } catch (e) {
      say(`打不开：${e}`, true);
    }
  }

  /** 设置账本就在 LOCALAPPDATA 下；这里只拼给 opener 用，读文件仍然走 Rust。 */
  function settingsDir(): string {
    const local = navigator.userAgent.includes('Windows') ? '%LOCALAPPDATA%' : '';
    return `${local}\\KeyMouse`;
  }

  async function runDoctor(): Promise<void> {
    doctorBusy = true;
    doctor = '';
    try {
      doctor = await invoke<string>('run_doctor');
    } catch (e) {
      doctor = `体检没跑起来：${e}`;
    } finally {
      doctorBusy = false;
    }
  }

  async function bundle(): Promise<void> {
    try {
      const path = await invoke<string>('collect_diagnostics');
      diagnosticsPath = path;
      say('诊断打包好了');
    } catch (e) {
      say(`打包失败：${e}`, true);
    }
  }

  const shortVersion = (): string => runtime?.cliVersion ?? '没找到命令行';
  const envLabel = (): string =>
    runtime?.environment === 'development' ? '开发树' : runtime?.environment === 'installed' ? '安装版' : '未知';
</script>

<div class="sheet">
  <header class="masthead">
    <div class="kicker">KEYMOUSE · 设置</div>
    <h1>排版与偏好</h1>
    <p class="standfirst">
      这一页只放"改了就立刻生效"的东西。判断环境那套逻辑在命令行里只有一份，所以这里不自己猜——只把它的结论贴出来。
    </p>
    <div class="meta">
      <span>{shortVersion()}</span>
      <span class="dot">·</span>
      <span>{envLabel()}</span>
      <span class="dot">·</span>
      <span class="mono">{runtime?.editorExe ?? ''}</span>
    </div>
    <div class="chrome"></div>
  </header>

  <section class="chapter">
    <div class="ornament"><span>✦✦✦</span><b>01</b><span>✦✦✦</span></div>
    <h2>外观</h2>
    <p class="lede">主题只换纸与墨，不换产品色：朱砂一直是"当前与可动"的那一个颜色。</p>

    <div class="theme-grid">
      {#each THEMES as item (item.id)}
        <button
          class="theme-card"
          class:current={theme === item.id}
          on:click={() => pickTheme(item.id)}
          data-available={themes.includes(item.id)}
        >
          <span class="swatch" data-swatch={item.id}></span>
          <span class="theme-name">{item.name}</span>
          <span class="theme-note">{item.note}</span>
          {#if theme === item.id}
            <span class="picked"><Icon name="check" size={13} />当前</span>
          {/if}
        </button>
      {/each}
    </div>
  </section>

  <section class="chapter">
    <div class="ornament"><span>✦✦✦</span><b>02</b><span>✦✦✦</span></div>
    <h2>文件</h2>

    <div class="def">
      <div class="def-term">最近<em>{recent.length} 个</em></div>
      <div class="def-body">
        {#if recent.length === 0}
          <p class="muted">还没有打开过流程。</p>
        {:else}
          <ul class="recent">
            {#each recent as path (path)}
              <li>
                <button class="link" on:click={() => openRecent(path)} title={path}>
                  {path.split(/[\\/]/).pop()}
                </button>
                <span class="path mono">{path}</span>
                <button class="tiny" on:click={() => forget(path)} title="从列表里去掉">
                  <Icon name="close" size={13} />
                </button>
              </li>
            {/each}
          </ul>
          <button class="ghost" on:click={clearRecent}>清空最近列表</button>
        {/if}
      </div>
    </div>

    <div class="def">
      <div class="def-term">设置账本</div>
      <div class="def-body">
        <p>最近文件、窗口位置、主题都在 <span class="mono">%LOCALAPPDATA%\KeyMouse\editor-settings.json</span>。</p>
        <button class="ghost" on:click={() => openFolder('settings')}><Icon name="folder" size={14} />打开所在目录</button>
      </div>
    </div>
  </section>

  <section class="chapter">
    <div class="ornament"><span>✦✦✦</span><b>03</b><span>✦✦✦</span></div>
    <h2>环境与诊断</h2>

    <div class="def">
      <div class="def-term">命令行<em>{runtime?.cliPath ? '已找到' : '没找到'}</em></div>
      <div class="def-body">
        <p class="mono selectable">{runtime?.cliPath ?? '编辑器会在 artifacts\\cli、bin\\Release、注册表与 PATH 里找 KeyMouse.exe'}</p>
        <div class="row">
          <button class="ghost" disabled={!runtime?.cliPath} on:click={copyCli}><Icon name="copy" size={14} />复制路径</button>
          <button class="ghost" disabled={!runtime?.cliPath} on:click={() => openFolder('cli')}><Icon name="folder" size={14} />在文件夹里看</button>
        </div>
      </div>
    </div>

    <div class="def">
      <div class="def-term">体检<em>doctor</em></div>
      <div class="def-body">
        <p>界面不自己判断环境；点一下就是把命令行那份判断原样搬回来。</p>
        <div class="row">
          <button class="ghost" on:click={runDoctor} disabled={doctorBusy}>
            <Icon name="pulse" size={14} />{doctorBusy ? '正在跑…' : '跑一次体检'}
          </button>
          <button class="ghost" on:click={bundle}><Icon name="wrench" size={14} />打包诊断信息</button>
        </div>
        {#if diagnosticsPath}
          <p class="mono selectable">{diagnosticsPath}</p>
        {/if}
        {#if doctor}
          <pre class="doctor selectable">{doctor}</pre>
        {/if}
      </div>
    </div>
  </section>

  <section class="chapter">
    <div class="ornament"><span>✦✦✦</span><b>04</b><span>✦✦✦</span></div>
    <h2>快捷键</h2>
    <table class="keys">
      <thead>
        <tr><th>键</th><th>做什么</th></tr>
      </thead>
      <tbody>
        <tr><td class="mono">Ctrl+S</td><td>保存</td></tr>
        <tr><td class="mono">Ctrl+Shift+S</td><td>另存为</td></tr>
        <tr><td class="mono">Ctrl+O / Ctrl+N</td><td>打开 / 新建</td></tr>
        <tr><td class="mono">F5 / Ctrl+Enter</td><td>播放 / 试运行（只查不点）</td></tr>
        <tr><td class="mono">↑ ↓</td><td>上一步 / 下一步</td></tr>
        <tr><td class="mono">Alt+↑ / Alt+↓</td><td>把这一步上移 / 下移</td></tr>
        <tr><td class="mono">Ctrl+F</td><td>跳进过滤框</td></tr>
        <tr><td class="mono">Ctrl+,</td><td>打开这一页</td></tr>
        <tr><td class="mono">Esc</td><td>收菜单；跑着的时候是停止</td></tr>
      </tbody>
    </table>
  </section>

  <footer class="colophon">
    <span class="mono">{shortVersion()}</span>
    <span>排版语汇取自 归藏 Page（杂志风）；图标为内联 SVG，不联网。</span>
  </footer>

  {#if hint}
    <div class="toast" class:bad={hintError}>{hint}</div>
  {/if}
</div>

<style>
  .sheet {
    height: 100%;
    overflow: auto;
    /* 底部留出状态栏的高度：页面内容别贴到脚下那条横线 */
    padding: 36px 0 88px;
    background: var(--kp-paper);
  }

  /* —— 报头：居中、上粗下细两条线（skill 的 doc-chrome + hero） —— */
  .masthead {
    max-width: 720px;
    margin: 0 auto 8px;
    padding: 0 40px;
    text-align: center;
  }

  .kicker {
    font: 600 11px/1 var(--kp-font-mono);
    letter-spacing: 0.22em;
    color: var(--kp-ink2);
    text-transform: uppercase;
  }

  h1 {
    margin: 14px 0 10px;
    font: 500 var(--kp-fs-display) / 1.25 var(--kp-font-serif);
    letter-spacing: 0.02em;
  }

  .standfirst {
    margin: 0 auto;
    max-width: var(--kp-measure);
    font: var(--kp-fs-prose) / var(--kp-lh-prose) var(--kp-font-serif);
    color: var(--kp-ink2);
  }

  .meta {
    margin: 18px 0 12px;
    font: 400 11.5px/1 var(--kp-font-mono);
    color: var(--kp-ink3);
    display: flex;
    gap: 8px;
    justify-content: center;
    flex-wrap: wrap;
  }

  .meta .dot {
    opacity: 0.6;
  }

  .chrome {
    height: 0;
    border-top: 2px solid var(--kp-ink);
    border-bottom: 1px solid var(--kp-rule);
    padding-top: 3px;
    max-width: 720px;
    margin: 0 auto;
  }

  /* —— 章节：花饰编号 + 居中衬线标题 —— */
  .chapter {
    max-width: 720px;
    margin: 56px auto 0;
    padding: 0 40px;
  }

  .ornament {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 10px;
    font: 400 11px/1 var(--kp-font-mono);
    color: var(--kp-ink3);
    letter-spacing: 0.18em;
  }

  .ornament span {
    opacity: 0.55;
  }

  .ornament b {
    font-weight: 500;
    letter-spacing: 0.08em;
    color: var(--kp-ink2);
  }

  h2 {
    margin: 10px 0 6px;
    text-align: center;
    font: 500 22px/1.4 var(--kp-font-serif);
    letter-spacing: 0.03em;
  }

  .lede {
    margin: 0 auto 22px;
    max-width: var(--kp-measure);
    text-align: center;
    font: var(--kp-fs-prose) / var(--kp-lh-prose) var(--kp-font-serif);
    color: var(--kp-ink2);
  }

  /* —— 主题卡 —— */
  .theme-grid {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 12px;
  }

  .theme-card {
    position: relative;
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 6px;
    padding: 14px 14px 16px;
    text-align: left;
    background: var(--kp-card);
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-sheet);
  }

  .theme-card:hover {
    border-color: var(--kp-rule-strong);
  }

  .theme-card.current {
    border-color: var(--kp-signal);
  }

  .swatch {
    width: 100%;
    height: 34px;
    border: 1px solid var(--kp-rule);
    border-radius: 2px;
  }

  .swatch[data-swatch='paper'] {
    background: linear-gradient(100deg, #f1efea 55%, #0a0a0b 55%);
  }

  .swatch[data-swatch='kraft'] {
    background: linear-gradient(100deg, #eedfc7 55%, #2a1e13 55%);
  }

  .swatch[data-swatch='night'] {
    background: linear-gradient(100deg, #0e0d0c 55%, #ece2cf 55%);
  }

  .theme-name {
    font: 500 14px/1.4 var(--kp-font);
  }

  .theme-note {
    font: 400 12px/1.6 var(--kp-font);
    color: var(--kp-ink3);
  }

  .picked {
    position: absolute;
    right: 10px;
    top: 10px;
    display: flex;
    align-items: center;
    gap: 3px;
    padding: 2px 6px;
    font: 500 10.5px/1.4 var(--kp-font-mono);
    color: var(--kp-signal);
    background: var(--kp-paper);
    border: 1px solid var(--kp-signal);
    border-radius: 2px;
    letter-spacing: 0.06em;
  }

  /* —— 术语定义：左标题 / 右正文（skill 的 def-card，杂志风为堆叠式，这里改并列以便操作） —— */
  .def {
    display: grid;
    grid-template-columns: 132px minmax(0, 1fr);
    gap: 16px;
    padding: 18px 0;
    border-top: 1px solid var(--kp-rule);
  }

  .def:last-of-type {
    border-bottom: 1px solid var(--kp-rule);
  }

  .def-term {
    font: 500 13.5px/1.5 var(--kp-font);
    color: var(--kp-ink);
  }

  .def-term em {
    display: block;
    font: 400 11px/1.6 var(--kp-font-mono);
    font-style: normal;
    color: var(--kp-ink3);
    letter-spacing: 0.04em;
  }

  .def-body {
    font: var(--kp-fs-prose) / 1.7 var(--kp-font-serif);
    color: var(--kp-ink2);
  }

  .def-body p {
    margin: 0 0 10px;
  }

  .def-body p:last-child {
    margin-bottom: 0;
  }

  .row {
    display: flex;
    align-items: center;
    gap: 10px;
    flex-wrap: wrap;
  }

  .recent {
    margin: 0 0 10px;
    padding: 0;
    list-style: none;
  }

  .recent li {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 7px 0;
    border-bottom: 1px solid var(--kp-rule);
  }

  .recent li:last-child {
    border-bottom: 0;
  }

  .link {
    font: 500 13.5px/1.4 var(--kp-font);
    color: var(--kp-ink);
    border-bottom: 1px solid transparent;
  }

  .link:hover {
    border-bottom-color: var(--kp-signal);
    color: var(--kp-signal);
  }

  .path {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: 11.5px;
    color: var(--kp-ink3);
  }

  .ghost {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 28px;
    padding: 0 10px;
    font: 400 12.5px/1 var(--kp-font);
    color: var(--kp-ink2);
    background: transparent;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-control);
  }

  .ghost:hover:not(:disabled) {
    color: var(--kp-ink);
    border-color: var(--kp-rule-strong);
  }

  .ghost:disabled {
    opacity: 0.45;
  }

  .tiny {
    display: grid;
    place-items: center;
    width: 22px;
    height: 22px;
    color: var(--kp-ink3);
    border-radius: var(--kp-radius-control);
  }

  .tiny:hover {
    color: var(--kp-critical);
    background: var(--kp-card);
  }

  .muted {
    color: var(--kp-ink3);
    font-size: 13px;
  }

  .mono {
    font-family: var(--kp-font-mono);
    font-size: 0.86em;
  }

  .selectable {
    user-select: text;
    cursor: text;
  }

  /* 书籍三线表：上下两条墨线、行间发丝线、无竖线 */
  .keys {
    width: 100%;
    border-collapse: collapse;
    margin-top: 14px;
  }

  .keys th {
    padding: 6px 8px;
    text-align: left;
    font: 400 11px/1.6 var(--kp-font-mono);
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--kp-ink3);
    border-bottom: 1.5px solid var(--kp-ink);
  }

  .keys td {
    padding: 7px 8px;
    font: 400 13px/1.6 var(--kp-font);
    border-bottom: 1px solid var(--kp-rule);
  }

  .keys tr:last-child td {
    border-bottom: 1.5px solid var(--kp-ink);
  }

  .keys td:first-child {
    width: 180px;
    color: var(--kp-ink);
  }

  .doctor {
    margin: 12px 0 0;
    padding: 12px 14px;
    max-height: 260px;
    overflow: auto;
    background: var(--kp-card-2);
    border-left: 1px solid var(--kp-signal);
    font: 400 11.5px/1.75 var(--kp-font-mono);
    color: var(--kp-ink2);
    white-space: pre-wrap;
  }

  .colophon {
    max-width: 720px;
    margin: 56px auto 0;
    padding: 18px 40px 0;
    border-top: 1px solid var(--kp-rule);
    display: flex;
    justify-content: space-between;
    gap: 16px;
    flex-wrap: wrap;
    font: 400 11.5px/1.7 var(--kp-font-mono);
    color: var(--kp-ink3);
  }

  .toast {
    position: sticky;
    bottom: 16px;
    margin: 24px auto 0;
    max-width: 420px;
    padding: 8px 14px;
    text-align: center;
    font: 400 12.5px/1.6 var(--kp-font);
    color: var(--kp-ink);
    background: var(--kp-card);
    border: 1px solid var(--kp-rule);
    border-left: 2px solid var(--kp-signal);
    border-radius: var(--kp-radius-control);
  }

  .toast.bad {
    border-left-color: var(--kp-critical);
    color: var(--kp-critical);
  }
</style>
