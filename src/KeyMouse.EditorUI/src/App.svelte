<script lang="ts">
  import { onMount } from 'svelte';
  import { invoke } from '@tauri-apps/api/core';
  import { listen } from '@tauri-apps/api/event';
  import { open, save } from '@tauri-apps/plugin-dialog';
  import StepIcon from './lib/StepIcon.svelte';
  import Inspector from './lib/Inspector.svelte';
  import { fieldsOf, schemaFrom, type FlowSchema } from './lib/schema';
  import { writePath } from './lib/fieldpath';
  import { duplicateAt, insertAfter, moveDown, moveTo, moveUp, removeAt, selectionAfterRemove } from './lib/edit';
  import { parseRunProgress, progressText, type RunProgress } from './lib/progress';
  import { parseStepResult, runConclusion, runHint, runIsBad, type StepResult } from './lib/run';
  import { QUICK_TYPES, STEP_TEMPLATES, makeStep, missingHint, missingOf } from './lib/steps';
  import {
    childSteps,
    stepSummary,
    stepTitle,
    type FlowStep,
    type FlowTarget,
    type StepType,
  } from './lib/model';

  type RuntimeInfo = {
    cliPath: string | null;
    cliVersion: string | null;
    environment: 'development' | 'installed' | 'unknown';
    repoRoot: string | null;
    editorExe: string;
    elevated: boolean;
    /** 启动时点名要打开的流程；双击 .json 或命令行直接给路径时才有。 */
    initialPath: string | null;
  };

  /** 一个流程文件：文档对象原样保留，界面只碰 steps。 */
  type FlowDocument = {
    format?: string;
    version?: number;
    recordedAt?: string;
    variables?: Record<string, string>;
    steps: FlowStep[];
    [key: string]: unknown;
  };

  let runtime: RuntimeInfo | null = null;
  let path: string | null = null;
  let doc: FlowDocument | null = null;
  let steps: FlowStep[] = [];
  let selected = 0;
  let dirty = false;
  let outputOpen = false;
  let jsonOpen = false;
  let moreOpen = false;
  let busy = false;
  /** 正在跑的那条子进程。停止按钮据此出现，播放按钮据此让位。 */
  let running = false;
  /** 正在录制的那个目标文件。非空就表示"这一轮是录制，不是播放"。 */
  let recordingPath: string | null = null;
  /** self = 当前权限录；levated = 管理员权限录（外壳停不了，只能用户按键）。 */
  let recordMode: 'self' | 'elevated' | null = null;
  /** 「录制」旁边那个 ⌄ 的小菜单开着没有（两个录制方式选一个）。 */
  let recordMenuOpen = false;
  /** 工具栏最右那个 ··· 菜单（打开/保存/另存为/从屏幕取区域）。 */
  let opsOpen = false;
  /** 流程格式的契约；拿到之前检查器只显示"正在读契约"。 */
  let schema: FlowSchema | null = null;

  /** 哪些分组步是展开的。不在表里 = 收起（老外壳的默认也是收起）。 */
  let expanded: Record<number, boolean> = {};

  /** 最近打开过的流程（新在前，只含还存在的文件）。空状态里列出来，省一次"打开"对话框。 */
  let recent: string[] = [];
  // 录制期间主窗口缩成一条细带：那时候用户要操作的是别的窗口，
  // 编辑器那两栏只会挡路；而工具栏一旦被盖住，「停止」就摸不到了。
  let mini = false;
  let miniSince = 0;
  let miniElapsed = 0;
  let ticker: ReturnType<typeof setInterval> | null = null;
  /** 运行输出：命令行的每一行原样收下，错误的行标红。 */
  let lines: { stream: string; line: string }[] = [];
  /** 跑到第几步了；命令行没报编号时保持 null（不猜）。 */
  let progress: RunProgress | null = null;

  /**
   * 每一步跑成什么样，按**行下标**存（只有"没有分组步"的流程才敢这么存）。
   * 命令行的编号是执行序号：分组步会展开、子流程会内联，一次播放的 3 号可能不是第 3 行。
   * 所以这里只在流程里没有 repeat / foreach / call 时才把结果标到行上——那种流程里
   * 执行顺序和显示顺序才是同一件事。分组流程仍旧看底部的「跑到第 n/N 步」。
   */
  let results: Record<number, StepResult> = {};
  /** 这一轮是不是试运行：行上的结果要标出来，否则和真跑长得一模一样。 */
  let runIsDry = false;

  /** 右键菜单：在哪个行、屏幕上的哪个点。`null` 表示没开。 */
  let rowMenu: { index: number; x: number; y: number } | null = null;

  /** 拖拽排序：正在拖第几行、鼠标当前压在第几行上（
ull = 没在拖）。 */
  let dragFrom: number | null = null;
  let dragOver: number | null = null;
  /** 这份流程里有没有分组步（repeat / foreach / call）。有的话行上就不能标编号结果。 */
  $: flatRun = steps.every((step) => !['repeat', 'foreach', 'call'].includes(step.type));
  /** 底部输出里的最后一条人话消息；失败与成功都写这里，不弹对话框。 */
  let notice: { text: string; bad: boolean } | null = null;
  /**
   * 已经取到的证据图，键是步骤里记的那个相对路径。
   *
   * 图是**按需**从外壳读字节再造 Blob URL 的：界面不需要知道流程文件在哪一层目录，
   * 也不需要给 WebView 开文件系统权限。同一张图只读一次，读不到就记住原因，别反复试。
   */
  let shots: Record<string, { url?: string; error?: string }> = {};

  /**
   * 撤销栈存的是"改动之前那份文档"的 JSON。
   *
   * 为什么是 JSON 而不是对象引用：步骤编辑里到处是原地改字段，对象引用会被后续
   * 修改悄悄改掉，撤销就成了"撤到一半"。序列化一份 10 KB 的文本，比追踪引用安全得多。
   */
  const undoStack: string[] = [];
  const redoStack: string[] = [];
  const HISTORY_LIMIT = 50;

  onMount(async () => {
    void loadSchema();
    void loadRecent();
    try {
      runtime = await invoke<RuntimeInfo>('runtime_info');
    } catch (e) {
      runtime = null;
      note(String(e), true);
      return;
    }
    // 命令行/双击带进来的文件直接打开，省掉一次"再点一下打开"。
    if (runtime.initialPath) await loadFrom(runtime.initialPath);
  });

  // 关闭窗口时外壳会问界面一句"能关吗"：脏了就先确认，再让它关。
  onMount(() => {
    const stops: (() => void)[] = [];
    const keep = (p: Promise<() => void>) =>
      void p.then((unlisten) => stops.push(unlisten)).catch(() => undefined);

    keep(
      listen('shell://close-requested', async () => {
        if (await confirmDiscard()) await invoke('close_window').catch(() => undefined);
      }),
    );

    // 第二次启动（双击另一个流程文件）会被外壳拦下，转成这个事件：
    // 有未保存的改动就先问一句，问完再打开——和点「打开」走同一条路。
    keep(
      listen<string>('shell://open-external', async (event) => {
        if (!(await confirmDiscard())) return;
        await loadFrom(event.payload);
      }),
    );

    // 命令行的输出是一行一行来的：每行都收进面板，不在这里做解释。
    keep(
      listen<{ stream: string; line: string }>('shell://run-output', (event) => {
        lines = [...lines, event.payload];
        // 进度只从输出里读，不问命令行走别的通道：一条命令、一份事实。
        progress = parseRunProgress(event.payload.line) ?? progress;
        const result = parseStepResult(event.payload.line);
        if (result && flatRun) results = { ...results, [result.index - 1]: { ...result, dry: runIsDry } };
      }),
    );

    keep(
      listen<{ code: number | null; cancelled: boolean }>('shell://run-exit', (event) => {
        running = false;
        progress = null;
        const { code, cancelled } = event.payload;

        // 录制共用同一条子进程通道：结束时该做的是"把刚录出来的文件打开"，
        // 而不是用播放的结论去解释一次录制。
        if (recordingPath) {
          const target = recordingPath;
          const elevated = recordMode === 'elevated';
          recordingPath = null;
          recordMode = null;
          void exitMini();
          // 提权录制时我们只拿到"启动器"的退出码：UAC 被拒是 1223，录制本身出错是 1/2/3。
          if (cancelled) note('这次录制被掐掉了，没有落盘', true);
          else if (elevated && code === 1223) note('UAC 被拒绝了，这一轮什么都没录', true);
          else if (elevated && code !== 0) note(`提权录制以退出码 ${code} 结束，没有落盘`, true);
          else void adoptRecording(target);
          return;
        }

        note(runConclusion(code, cancelled), runIsBad(code, cancelled));
      }),
    );

    return () => stops.forEach((stop) => stop());
  });

  // 界面里没接住的异常不该只留在 DevTools 里：推给外壳记进 editor-crash.log，
  // 用户报"它崩了"时至少有一条时间线可看。
  onMount(() => {
    const push = (what: string, detail: string) =>
      void invoke('log_error', { message: `${what}: ${detail}` }).catch(() => undefined);

    const onError = (event: ErrorEvent) => push('error', `${event.message} @ ${event.filename}:${event.lineno}`);
    const onRejection = (event: PromiseRejectionEvent) => push('unhandled', String(event.reason));


    /*
      右键菜单：WebView2 自带一张"返回/刷新/打印"的原生菜单，不拦住就会盖在
      我们的菜单上面。应用里没有需要浏览器原生菜单的地方（粘贴走 Ctrl+V）。
    */
    const blockNativeMenu = (event: MouseEvent) => event.preventDefault();
    document.addEventListener('contextmenu', blockNativeMenu);
    window.addEventListener('error', onError);
    window.addEventListener('unhandledrejection', onRejection);
    return () => {
      window.removeEventListener('error', onError);
      window.removeEventListener('unhandledrejection', onRejection);
      document.removeEventListener('contextmenu', blockNativeMenu);
    };
  });

  // 外壳不知道界面里发生了什么，脏标记得推给它；它只负责在关窗前问一句。
  $: syncDirty(dirty);

  // 右侧检查器的内容跟着选中的行走；分组步骤选中的是它自己，不是子步骤。
  $: current = steps[selected] ?? null;
  $: currentShot = current?.shot ?? null;
  // 选中带证据的步骤时才去读那张图；没存过盘就没有可比对的相对路径。
  $: if (currentShot && path) void ensureShot(currentShot);
  $: fileLabel = path ? baseName(path) : '还没有打开流程';
  $: canSave = doc !== null && dirty;

  /** 记一条"刚才发生了什么"。成功的消息只落到底部那行，失败了才把输出面板展开——
   *  轻提醒优先：打开一个文件不该在屏幕上多出一块面板。 */
  function note(text: string, bad = false) {
    notice = { text, bad };
    if (bad) outputOpen = true;
  }

  /**
   * 取这一步的证据图。只读到字节，URL 由这里造：见 `shots` 的注释。
   *
   * 先占位再请求，避免同一张图被连续两次 reactive 触发时读两遍。
   */
  async function ensureShot(shot: string) {
    if (shots[shot] || !path) return;
    shots = { ...shots, [shot]: {} };
    try {
      const bytes = await invoke<ArrayBuffer>('read_shot', { flowPath: path, shot });
      const url = URL.createObjectURL(new Blob([bytes], { type: 'image/png' }));
      shots = { ...shots, [shot]: { url } };
    } catch (e) {
      shots = { ...shots, [shot]: { error: String(e) } };
    }
  }

  /** 用系统看图程序打开 / 在资源管理器里定位。路径由外壳算，这里不拼。 */
  async function openShot(shot: string, reveal: boolean) {
    try {
      const { openPath, revealItemInDir } = await import('@tauri-apps/plugin-opener');
      const full = await invoke<string>('shot_file', { flowPath: path, shot });
      if (reveal) await revealItemInDir(full);
      else await openPath(full);
    } catch (e) {
      note(`打不开证据图：${String(e)}`, true);
    }
  }

  function baseName(full: string): string {
    const cut = Math.max(full.lastIndexOf('\\'), full.lastIndexOf('/'));
    return cut >= 0 ? full.slice(cut + 1) : full;
  }

  function emptyDocument(): FlowDocument {
    return {
      format: 'keymouse-flow',
      version: 1,
      recordedAt: new Date().toISOString(),
      steps: [],
    };
  }

  /** 只认 steps 是数组；别的字段原样留着，保存时一起写回去。 */
  function adopt(json: unknown): FlowDocument {
    if (!json || typeof json !== 'object') throw new Error('文件里不是 JSON 对象');
    const candidate = json as FlowDocument;
    if (!Array.isArray(candidate.steps)) throw new Error('这份文件没有 steps 数组，不像是流程');
    return candidate;
  }

  function useDocument(next: FlowDocument, filePath: string | null) {
    doc = next;
    steps = next.steps;
    path = filePath;
    selected = 0;
    dirty = filePath === null;
  }

  /** 把路径记进"最近打开"，失败就算了——记不住不该挡住干活。 */
  /** 读一次"最近打开"。外壳打不开账本时安静地空着，空状态里就不显示这一块。 */
  async function loadRecent() {
    try {
      recent = await invoke<string[]>('recent_files');
    } catch {
      recent = [];
    }
  }


  async function rememberPath(filePath: string) {
    try {
      recent = await invoke<string[]>('remember_recent', { path: filePath });
    } catch {
      /* 账本写不进去不是用户的问题 */
    }
  }

  async function loadFrom(filePath: string) {
    busy = true;
    try {
      const file = await invoke<{ path: string; text: string; json: unknown }>('open_flow', { path: filePath });
      useDocument(adopt(file.json), file.path);
      note(`打开 ${file.path}`);
      void rememberPath(file.path);
    } catch (e) {
      note(String(e), true);
    } finally {
      busy = false;
    }
  }

  async function newFlow() {
    if (!(await confirmDiscard())) return;
    useDocument(emptyDocument(), null);
    dirty = true;
    note('新建了一份空流程：先加一步，再保存');
  }

  async function openFlow() {
    if (!(await confirmDiscard())) return;
    try {
      const picked = await open({ filters: [{ name: '流程', extensions: ['json'] }], multiple: false });
      if (typeof picked === 'string') await loadFrom(picked);
    } catch (e) {
      note(String(e), true);
    }
  }

  async function openSample() {
    if (!(await confirmDiscard())) return;
    const base = runtime?.repoRoot;
    if (!base) {
      note('安装版没有示例文件；示例只在仓库里（samples\\demo-flow.json）', true);
      return;
    }
    await loadFrom(`${base}\\samples\\demo-flow.json`);
  }

  async function saveFlow(): Promise<void> {
    if (!doc) return;
    if (!path) {
      await saveAs();
      return;
    }
    busy = true;
    try {
      await invoke('save_flow', { path, text: serialize() });
      dirty = false;
      note(`已保存 ${path}`);
    } catch (e) {
      note(String(e), true);
    } finally {
      busy = false;
    }
  }

  async function saveAs(): Promise<void> {
    if (!doc) return;
    try {
      const picked = await save({
        filters: [{ name: '流程', extensions: ['json'] }],
        defaultPath: path ?? 'flow.json',
      });
      if (typeof picked !== 'string') return;
      await invoke('save_flow', { path: picked, text: serialize() });
      path = picked;
      dirty = false;
      note(`已保存 ${picked}`);
    } catch (e) {
      note(String(e), true);
    }
  }

  /** 写盘用的文本：2 空格缩进 + 末尾换行，与命令行读写的格式一致。 */
  function serialize(): string {
    return JSON.stringify(doc, null, 2) + '\n';
  }

  /** 会丢东西的动作才问一句；别的一律写进底部输出。 */
  async function confirmDiscard(): Promise<boolean> {
    if (!dirty) return true;
    const { ask } = await import('@tauri-apps/plugin-dialog');
    return await ask('这份流程还没保存，继续会丢掉改动。', {
      title: '还没保存',
      kind: 'warning',
      okLabel: '丢掉改动',
      cancelLabel: '回去保存',
    });
  }

  /**
   * 试运行与播放。
   *
   * 两条路都要求"已经存成文件"：命令行读的是磁盘上的 JSON，界面里改到一半的东西
   * 它看不见。与其在后台偷偷写一份临时文件、让报错里的行号对不上界面，不如直接说清楚。
   */
  async function startRun(dryRun: boolean): Promise<void> {
    if (!doc) {
      note('先打开或新建一个流程', true);
      return;
    }
    if (!path) {
      note('先保存（Ctrl+S）再跑：命令行要读一个文件', true);
      return;
    }
    if (busy || running) return;

    lines = [];
    results = {};
    progress = null;
    outputOpen = true;
    runIsDry = dryRun;

    if (dryRun) {
      busy = true;
      note(runHint(true));
      try {
        const result = await invoke<{ code: number | null; cancelled: boolean }>('validate_flow', {
          path,
        });
        note(runConclusion(result.code, result.cancelled, true), runIsBad(result.code, result.cancelled));
      } catch (e) {
        note(`试运行没起来：${e}`, true);
      } finally {
        busy = false;
      }
      return;
    }

    try {
      await invoke('run_flow', { path });
      running = true;
      note(runHint(false));
    } catch (e) {
      note(`播放没起来：${e}`, true);
    }
  }

  async function stopRun(): Promise<void> {
    // 录制不是"杀进程"能收尾的：得让它自己走到写文件那一步，所以先发停止键，
    // 外壳里 3 秒还没退才兜底杀（见 record.rs）。
    void exitMini();
    // 提权录制：那个进程的权限比外壳高，UIPI 不许我们往里送键，也杀不掉它。
    // 这里只能如实告诉用户按什么，别假装停下来了。
    if (recordingPath && recordMode === 'elevated') {
      note('提权录制只能你自己停：按 Ctrl+Alt+Q，或连按两次 Esc。', true);
      return;
    }
    try {
      if (recordingPath) await invoke('record_stop');
      else await invoke('run_cancel');
    } catch (e) {
      note(`停不下来：${e}`, true);
    }
  }

  /**
   * 录制。
   *
   * 默认不自我提权：钩子看得见与自己同级或更低权限的窗口，提权窗口上的点击录不到，
   * 命令行会把这句话原样写进输出里。要录那些窗口，用 [`startRecordElevated`]。
   *
   * 停止走产品自己的停止键（Ctrl+Alt+Q），不另开一条后门——它和录制用的是同一套注入，
   * 能录就能停。
   */
  async function startRecord(): Promise<void> {
    if (!runtime?.cliPath) {
      note('这台机器上没找到命令行 KeyMouse.exe', true);
      return;
    }
    if (busy || running) {
      note('正在跑一条流程，先停止再录', true);
      return;
    }

    const { save } = await import('@tauri-apps/plugin-dialog');
    const target = await save({
      title: '把这次录制存到哪',
      defaultPath: path ? path.replace(/\.json$/i, '-录制.json') : '录制.json',
      filters: [{ name: '流程', extensions: ['json'] }],
    });
    if (!target) return;

    lines = [];
    results = {};
    progress = null;
    outputOpen = true;
    try {
      await invoke('record_flow', { outPath: target, durationMs: 0 });
      recordingPath = target;
      recordMode = 'self';
      running = true;
      await enterMini();
      note('开始录了：去操作你要录的窗口，按 Esc 或点「停止」结束');
    } catch (e) {
      note(`录制没起来：${e}`, true);
    }
  }

  /**
   * 以管理员权限录一轮。
   *
   * UAC 里点「是」之后，录制在提权进程里跑——那些窗口上的点击才录得到。代价写在这儿：
   * 提权进程的权限比外壳高，外壳既杀不掉它也送不进停止键，所以
   *
   * - 界面**不做**迷你模式（那细带上的「停止」按了也不会生效，摆着就是骗人）；
   * - 界面上写清楚停法：`Ctrl+Alt+Q`，或连按两次 `Esc`；
   * - 结束与否靠命令行退出 + 文件出现来判断（`shell://run-exit` 照常会来）。
   */
  async function startRecordElevated(): Promise<void> {
    if (!runtime?.cliPath) {
      note('这台机器上没找到命令行 KeyMouse.exe', true);
      return;
    }
    if (busy || running) {
      note('正在跑一条流程，先停止再录', true);
      return;
    }

    const { save } = await import('@tauri-apps/plugin-dialog');
    const target = await save({
      title: '这次提权录制存到哪',
      defaultPath: path ? path.replace(/\.json$/i, '-录制.json') : '录制.json',
      filters: [{ name: '流程', extensions: ['json'] }],
    });
    if (!target) return;

    lines = [];
    results = {};
    progress = null;
    outputOpen = true;
    try {
      await invoke('record_flow_elevated', { outPath: target, durationMs: 0 });
      recordingPath = target;
      recordMode = 'elevated';
      running = true;
      note('UAC 里点「是」之后开始录：结束时按 Ctrl+Alt+Q，或连按两次 Esc');
    } catch (e) {
      recordingPath = null;
      recordMode = null;
      note(`提权录制没起来：${e}`, true);
    }
  }

  /**
   * 缩成一条细带（外壳负责改窗口，界面只负责换一屏）。
   *
   * 收不起来不算录制失败：那条带子是本轮的"顺手"，录制本身还在跑，
   * 所以只写一行提示。
   */
  async function enterMini(): Promise<void> {
    miniSince = Date.now();
    miniElapsed = 0;
    mini = true;
    if (ticker) clearInterval(ticker);
    ticker = setInterval(() => {
      miniElapsed = Math.floor((Date.now() - miniSince) / 1000);
    }, 500);
    try {
      await invoke('enter_mini');
    } catch (e) {
      note(`窗口缩不小：${e}（「停止」还在工具栏上）`, true);
    }
  }

  /** 拖动细带：交给外壳挪窗口，界面自己不改位置。 */
  async function dragMini(event: PointerEvent): Promise<void> {
    event.preventDefault();
    const { getCurrentWindow } = await import('@tauri-apps/api/window');
    await getCurrentWindow().startDragging();
  }

  /** 还原成两栏。录制结束、用户主动停止、或录制进程自己退出时都要走一遍。 */
  async function exitMini(): Promise<void> {
    if (!mini) return;
    mini = false;
    if (ticker) {
      clearInterval(ticker);
      ticker = null;
    }
    try {
      await invoke('leave_mini');
    } catch {
      // 还原失败（极少数：窗口已被关掉）不算问题，下次进入时会重新记原样。
    }
  }

  /** 录制结束后把文件读进来；读不出来就把原因摆出来，别让人对着一份空流程猜。 */
  async function adoptRecording(target: string): Promise<void> {
    try {
      const file = await invoke<{ path: string; text: string; json: unknown }>('open_flow', {
        path: target,
      });
      useDocument(adopt(file.json), file.path);
      // 刚录出来的文件就是它本来的样子，没有未保存的改动。
      dirty = false;
      note(`录了 ${steps.length} 步，已经打开`);
    } catch (e) {
      note(`录制结束了，但没读到文件：${e}`, true);
    }
  }

  function rowNumber(index: number): string {
    return String(index + 1);
  }

  function targetOf(step: FlowStep): FlowTarget {
    return step.target ?? {};
  }

  /** 「+ 更多」里按用途分组列出剩下的步骤种类；常用四种已经在工具栏上。 */
  $: moreGroups = [...new Set(STEP_TEMPLATES.map((t) => t.group))].filter((g) => g !== '常用');

  function syncDirty(value: boolean) {
    void invoke('set_dirty', { dirty: value }).catch(() => undefined);
  }

  // ── 撤销 / 重做 ────────────────────────────────────────

  /** 记一份"改之前的文档"。任何会改 steps 的动作都先调它。 */
  function snapshot() {
    if (!doc) return;
    undoStack.push(JSON.stringify(doc));
    if (undoStack.length > HISTORY_LIMIT) undoStack.shift();
    redoStack.length = 0;
  }

  /**
   * 输入框是逐字触发 on:input 的。每敲一个字都记一份快照，撤销就会退回一个字，
   * 而人期望的是"退回改之前"。所以同一个字段 800ms 内的连续修改只记一次。
   */
  let lastEditKey = '';
  let lastEditAt = 0;
  function snapshotEdit(key: string) {
    const now = Date.now();
    if (key === lastEditKey && now - lastEditAt < 800) {
      lastEditAt = now;
      return;
    }
    snapshot();
    lastEditKey = key;
    lastEditAt = now;
  }

  function restore(json: string) {
    const next = JSON.parse(json) as FlowDocument;
    doc = next;
    steps = next.steps ?? [];
    selected = Math.min(selected, Math.max(steps.length - 1, 0));
    dirty = true;
  }

  function undo() {
    const previous = undoStack.pop();
    if (previous === undefined || !doc) return;
    redoStack.push(JSON.stringify(doc));
    restore(previous);
    note('撤销了一步');
  }

  function redo() {
    const next = redoStack.pop();
    if (next === undefined || !doc) return;
    undoStack.push(JSON.stringify(doc));
    restore(next);
    note('重做了一步');
  }

  $: canUndo = undoStack.length > 0;
  $: canRedo = redoStack.length > 0;

  // ── 步骤的增删与排序 ───────────────────────────────────

  function applySteps(next: FlowStep[]) {
    if (!doc) return;
    snapshot();
    doc = { ...doc, steps: next };
    steps = next;
    touch();
  }

  /** 在选中步之后插一步；没有选中（空文档）就放到最前面。 */
  function addStep(type: StepType) {
    if (!doc) return;
    const at = steps.length === 0 ? -1 : selected;
    applySteps(insertAfter(steps, at, makeStep(type)));
    selected = at + 1;
    moreOpen = false;
    note(`加了「${stepTitle(type)}」${missingOf(makeStep(type)).length > 0 ? '：右边补齐还没填的项' : ''}`);
  }

  /** 复制这一步（右键菜单里的一项）。副本插在原件后面，并选中副本。 */
  function duplicateStep(index: number) {
    const next = duplicateAt(steps, index);
    if (next.length === steps.length) return;
    applySteps(next);
    selected = index + 1;
    note('复制了一步');
  }

  /**
   * 菜单里的四件事。写成无参函数是因为模板里写 `rowMenu!.index` 过不了编译
   * （Svelte 的模板不是 TypeScript），而且这几个动作都要"先收起菜单再动手"。
   */
  function menuDuplicate() {
    if (!rowMenu) return;
    const index = rowMenu.index;
    rowMenu = null;
    duplicateStep(index);
  }

  function menuUp() {
    if (!rowMenu) return;
    const index = rowMenu.index;
    rowMenu = null;
    stepUp(index);
  }

  function menuDown() {
    if (!rowMenu) return;
    const index = rowMenu.index;
    rowMenu = null;
    stepDown(index);
  }

  function menuDelete() {
    if (!rowMenu) return;
    const index = rowMenu.index;
    rowMenu = null;
    deleteStep(index);
  }
  /** 拖起一行。用 HTML5 的拖放：行是按钮，但拖放事件挂在外面那层 row-wrap 上。 */
  function startDrag(index: number, event: DragEvent) {
    dragFrom = index;
    rowMenu = null;
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      // 有些浏览器不给 dataTransfer 设点东西就不发 drop。
      event.dataTransfer.setData('text/plain', String(index));
    }
  }

  /** 模板里不能写类型标注：用函数工厂给每一行造一个 dragstart 处理器。 */
  function dragStarter(index: number) {
    return (event: DragEvent) => startDrag(index, event);
  }

  function dropOn(index: number) {
    const from = dragFrom;
    dragFrom = null;
    dragOver = null;
    if (from === null || from === index) return;
    const next = moveTo(steps, from, index);
    if (next === steps) return;
    applySteps(next);
    selected = index;
    note(`把第 ${from + 1} 步挪到了第 ${index + 1} 位`);
  }
  /** 右键一行：选中它，然后在鼠标位置开一张小菜单。 */
  function openRowMenu(index: number, event: MouseEvent) {
    event.preventDefault();
    selected = index;
    moreOpen = false;
                rowMenu = null;
                rowMenu = null;
    rowMenu = { index, x: event.clientX, y: event.clientY };
  }

  function deleteStep(index = selected) {
    if (!doc || steps.length === 0) return;
    const next = removeAt(steps, index);
    applySteps(next);
    selected = selectionAfterRemove(index, next.length);
    note('删了一步（Ctrl+Z 可以反悔）');
  }

  function stepUp(index = selected) {
    if (!doc || index <= 0) return;
    applySteps(moveUp(steps, index));
    selected = index - 1;
  }

  function stepDown(index = selected) {
    if (!doc || index >= steps.length - 1) return;
    applySteps(moveDown(steps, index));
    selected = index + 1;
  }

  /** 快捷键。输入框里只认 Ctrl+S / Ctrl+O / Ctrl+N / Ctrl+Z，别抢打字。 */
  function onKeydown(event: KeyboardEvent) {
    const element = event.target as HTMLElement | null;
    const typing = element?.tagName === 'INPUT' || element?.tagName === 'TEXTAREA';
    const mod = event.ctrlKey || event.metaKey;
    const key = event.key.toLowerCase();

    if (mod && key === 's') {
      event.preventDefault();
      void saveFlow();
      return;
    }
    if (mod && key === 'o') {
      event.preventDefault();
      void openFlow();
      return;
    }
    if (mod && key === 'n') {
      event.preventDefault();
      void newFlow();
      return;
    }
    if (mod && key === 'z') {
      event.preventDefault();
      if (event.shiftKey) redo();
      else undo();
      return;
    }
    if (mod && key === 'y') {
      event.preventDefault();
      redo();
      return;
    }

    // Esc 分两件事：跑着的时候它是"停止"（这是玩流程时手指最自然的位置），
    // 否则把那张「+ 更多」卡片收起来——它在视觉上压住了列表。
    if (key === 'escape') {
      if (rowMenu) {
        rowMenu = null;
        return;
      }
      if (opsOpen) {
        opsOpen = false;
        return;
      }
      if (running) void stopRun();
      moreOpen = false;
      recordMenuOpen = false;
      return;
    }

    if (event.ctrlKey && event.shiftKey && key === 'a') {
      event.preventDefault();
      moreOpen = true;
      return;
    }

    if (typing || !doc) return;

    if (event.altKey && event.key === 'ArrowUp') {
      event.preventDefault();
      stepUp();
      return;
    }
    if (event.altKey && event.key === 'ArrowDown') {
      event.preventDefault();
      stepDown();
      return;
    }
    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      deleteStep();
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      selected = Math.min(selected + 1, Math.max(steps.length - 1, 0));
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      selected = Math.max(selected - 1, 0);
    }
  }

  /** 改目标窗口的某个字段：空字符串就删掉这个键，别留一个空串在文件里。
   *  收的是下标而不是步骤对象：模板里只写 `(e) => setTargetField(selected, …)`，
   *  再把下标换成对象，模板里就不用做非空断言（模板表达式是纯 JS，没有 `!` 可用）。 */
  function setTargetField(index: number, key: keyof FlowTarget, raw: string) {
    const step = steps[index];
    if (!step) return;
    snapshotEdit(`target:${index}:${key}`);
    const value = raw.trim();
    const next: FlowTarget = { ...(step.target ?? {}) };
    if (value) (next as Record<string, unknown>)[key] = value;
    else delete (next as Record<string, unknown>)[key];
    if (Object.keys(next).length === 0) delete step.target;
    else step.target = next;
    touch();
  }

  function setNote(index: number, raw: string) {
    const step = steps[index];
    if (!step) return;
    snapshotEdit(`note:${index}`);
    const text = raw.trim();
    if (text) step.note = text;
    else delete step.note;
    touch();
  }

  function touch() {
    dirty = true;
    steps = steps;
    doc = doc;
  }

  /** 检查器推来的一次改动：路径是点号写法（`when.timeoutMs`），值已经按契约的类型转好。
   *  走 `writePath` 而不是就地赋值——空值要删键、删空的嵌套对象要连父键一起删，
   *  这些规矩写在一处（lib/fieldpath.ts），模板里就不用各个记得。 */
  function applyField(path: string, value: unknown) {
    const step = steps[selected];
    if (!step) return;
    snapshotEdit(`field:${selected}:${path}`);
    writePath(step, path, value);
    touch();
  }

  /** 「取点」「取一块」：屏幕上的事交给命令行（浮层与坐标换算都在它那儿），
   *  这里只把结果写回字段。选中窗口一并填进 target.process——老外壳也这么做：
   *  只有坐标不知道是哪块屏幕，回放到别的窗口上就偏了。 */
  async function pickField(kind: 'point' | 'region', path: string) {
    const step = steps[selected];
    if (!step) return;
    try {
      const picked = await invoke<{
        space: string;
        x: number;
        y: number;
        width: number;
        height: number;
        process?: string | null;
        title?: string | null;
      } | null>('pick_region', { point: kind === 'point' });
      if (!picked) {
        note('取点取消了');
        return;
      }
      snapshotEdit(`pick:${selected}:${path}`);
      writePath(step, path, {
        space: picked.space,
        x: picked.x,
        y: picked.y,
        ...(kind === 'region' ? { width: picked.width, height: picked.height } : {}),
      });
      if (picked.process) {
        writePath(step, 'target.process', picked.process);
        if (picked.title) writePath(step, 'target.title', picked.title);
      }
      touch();
      const where =
        picked.space === 'client' ? '客户区' : picked.space === 'window' ? '窗口' : '屏幕';
      note(
        '取了 ' + where + '上的 ' + picked.x + ',' + picked.y +
          (picked.process ? '（' + picked.process + '）' : ''),
      );
    } catch (e) {
      note(`取点失败：${String(e)}`, true);
    }
  }

  /**
   * 工具栏「从屏幕取区域…」：不看用户点了哪个字段，直接问契约这一步吃不吃坐标。
   *
   * 老外壳是同一套判断：这一步有 `region` 就写区域，否则写 `at` 这个点。两者都没有
   * （比如「等待时间」）就如实说，别默默什么都不做。
   */
  async function pickForSelected() {
    const step = steps[selected];
    if (!step) {
      note('先在左边选中一步', true);
      return;
    }
    if (!schema) {
      note('还没读到流程格式的契约，先试运行一次', true);
      return;
    }
    const fields = [...fieldsOf(schema, String(step.type)), ...(schema.common ?? [])];
    const region = fields.find((field) => field.name === 'region' && field.kind === 'region');
    if (region) {
      await pickField('region', 'region');
      return;
    }
    const point = fields.find((field) => field.name === 'at' && field.kind === 'point');
    if (point) {
      await pickField('point', 'at');
      return;
    }
    note('这一步没有坐标可填（它不需要屏幕上的位置）', true);
  }

  /** 拿一次契约：命令行是唯一的真相，界面只照它渲染。 */
  async function loadSchema() {
    try {
      const payload = await invoke<{ json: string }>('flow_schema');
      schema = schemaFrom(payload.json);
    } catch (e) {
      note(`读不到流程格式的契约：${String(e)}（检查器先只显示目标窗口）`, true);
    }
  }
</script>

<svelte:window on:keydown={onKeydown} />

{#if mini}
  <!-- 录制期间的一屏：只留"正在录 + 多久 + 停止"。
       窗口本身被外壳缩成一条细带并置顶，所以这一屏就是那条细带的内容。 -->
  <div class="mini" role="region" aria-label="正在录制">
    <span
      class="mini-drag"
      role="presentation"
      title="按住可以拖动窗口"
      on:pointerdown={dragMini}
    />
    <span class="rec-dot on" aria-hidden="true" />
    <span class="mini-title">正在录制</span>
    <span class="mini-clock">已录 {miniElapsed} 秒</span>
    <span class="mini-hint">操作要录的窗口，Esc 或按钮结束</span>
    <button class="mini-stop" on:click={() => void stopRun()}>
      <span class="square" aria-hidden="true" />
      停止
    </button>
  </div>
{:else}
<div class="shell">
  <header class="topbar">
    <div class="brand">
      <span class="mark" aria-hidden="true">▞</span>
      <span class="product">KeyMouse</span>
      <span class="kind">流程编辑器</span>
    </div>
    <div class="doc">
      <span class="name" class:warn={dirty}>{fileLabel}</span>
      {#if dirty}<span class="unsaved">未保存</span>{/if}
      {#if path}<span class="path" title={path}>{path}</span>{/if}
    </div>
    <div class="file-actions">
      <button class="ghost icon-only" on:click={undo} disabled={!canUndo} aria-label="撤销" title="撤销（Ctrl+Z）">↶</button>
      <button class="ghost icon-only" on:click={redo} disabled={!canRedo} aria-label="重做" title="重做（Ctrl+Shift+Z）">↷</button>
      <span class="sep" aria-hidden="true"></span>
      <button class="ghost" on:click={newFlow} disabled={busy}>新建</button>
      <button class="ghost" on:click={openFlow} disabled={busy}>打开</button>
      <button class="ghost" on:click={saveFlow} disabled={!canSave || busy}>保存</button>
      <button class="ghost" on:click={saveAs} disabled={!doc || busy}>另存为</button>
    </div>
  </header>

  <nav class="toolbar">
    <div class="palette">
      {#each QUICK_TYPES as type}
        <button class="palette-item" on:click={() => addStep(type)} disabled={!doc}>
          <StepIcon name={type} />{stepTitle(type)}
        </button>
      {/each}
      <button
        class="palette-item more"
        class:open={moreOpen}
        on:click={() => (moreOpen = !moreOpen)}
        disabled={!doc}
        aria-expanded={moreOpen}
      >+ 更多</button>

      {#if moreOpen}
        <div class="more-menu" role="menu">
          {#each moreGroups as group}
            <p class="more-group">{group}</p>
            {#each STEP_TEMPLATES.filter((item) => item.group === group) as item}
              <button class="more-item" role="menuitem" on:click={() => addStep(item.type)}>
                <StepIcon name={item.type} size={14} />{stepTitle(item.type)}
              </button>
            {/each}
          {/each}
          <p class="more-tip">新加的一步是半成品：右边补齐还没填的项才算能用。</p>
        </div>
      {/if}
    </div>

    <div class="actions">
      <div class="record-group">
        <button
          class="ghost"
          class:on={recordingPath !== null}
          on:click={startRecord}
          disabled={busy || running}
          title="按当前权限录制：提权窗口上的点击录不到，命令行会说明"
        >
          <span class="rec-dot" aria-hidden="true"></span>{recordingPath ? '录制中' : '录制'}
        </button>
        <button
          class="ghost rec-caret"
          on:click={() => (recordMenuOpen = !recordMenuOpen)}
          disabled={busy || running}
          aria-label="更多录制方式"
          aria-expanded={recordMenuOpen}
          title="更多录制方式"
        >
          ▾
        </button>
        {#if recordMenuOpen}
          <div class="record-menu" role="menu">
            <button role="menuitem" on:click={() => { recordMenuOpen = false; void startRecord(); }}>
              录制（当前权限）
            </button>
            <button role="menuitem" on:click={() => { recordMenuOpen = false; void startRecordElevated(); }}>
              以管理员权限录制（会弹 UAC）
            </button>
            <p class="record-tip">提权窗口上的点击只有提权录制才录得到；那一轮要自己按 Ctrl+Alt+Q 结束。</p>
          </div>
        {/if}
      </div>
      <button class="ghost" on:click={() => startRun(true)} disabled={busy || running}>试运行</button>
      {#if running}
        <button class="stop" on:click={stopRun}>
          <svg viewBox="0 0 16 16" aria-hidden="true" class="play"><rect x="4.5" y="4.5" width="7" height="7" rx="1" /></svg>
          停止
        </button>
      {:else}
        <button class="primary" on:click={() => startRun(false)} disabled={busy}>
          <svg viewBox="0 0 16 16" aria-hidden="true" class="play"><path d="M5 3.5 12 8l-7 4.5z" /></svg>
          播放
        </button>
      {/if}
      <div class="ops-group">
        <button
          class="ghost icon-only"
          aria-label="更多操作"
          aria-expanded={opsOpen}
          title="更多操作"
          on:click={() => (opsOpen = !opsOpen)}
        >
          ···
        </button>
        {#if opsOpen}
          <div class="ops-menu" role="menu">
            <button role="menuitem" disabled={busy} on:click={() => { opsOpen = false; void openFlow(); }}>打开…</button>
            <button role="menuitem" disabled={busy || !doc || !dirty} on:click={() => { opsOpen = false; void saveFlow(); }}>保存</button>
            <button role="menuitem" disabled={busy || !doc} on:click={() => { opsOpen = false; void saveAs(); }}>另存为…</button>
            <hr />
            <button role="menuitem" disabled={busy || running || !current} on:click={() => { opsOpen = false; void pickForSelected(); }}>
              从屏幕取区域…
            </button>
          </div>
        {/if}
      </div>
    </div>
  </nav>

  <main class="body">
    {#if !doc}
      <section class="empty">
        <h2>还没有打开流程</h2>
        <p>一条流程就是一串动作：切到某个窗口、点一下、输入文字、等屏幕上出现某段字。写好后可以先试运行，确认真能走通再播放。</p>
        <div class="empty-actions">
          <button class="primary" on:click={newFlow}>新建流程</button>
          <button class="ghost outline" on:click={openFlow}>打开流程…</button>
          <button class="ghost" on:click={openSample}>看看示例</button>
        </div>
        <p class="tips">流程文件是 JSON，命令行也能跑：<code>KeyMouse run 流程.json --dry-run</code> 先看每一步的窗口在不在。</p>
        {#if recent.length > 0}
          <!-- 最近打开：老外壳的空页也有这一块。文件名在前、目录在后，因为同一目录下的文件名更容易区分。 -->
          <div class="recent">
            <h3>最近</h3>
            <ul>
              {#each recent.slice(0, 5) as item}
                <li>
                  <button class="recent-item" on:click={() => loadFrom(item)} title={item}>
                    <span class="recent-name">{item.split(/[\\/]/).pop()}</span>
                    <span class="recent-dir">{item}</span>
                  </button>
                </li>
              {/each}
            </ul>
          </div>
        {/if}
      </section>
    {:else}
      <section class="list" aria-label="步骤">
        {#each steps as step, index}
          <div class="row-wrap" role="listitem"
              draggable="true"
              class:grouped={childSteps(step).length > 0}
              class:dragging={dragFrom === index}
              class:drop-target={dragOver === index && dragFrom !== null && dragFrom !== index}
              on:dragstart={dragStarter(index)}
              on:dragover|preventDefault={() => (dragOver = index)}
              on:dragleave={() => { if (dragOver === index) dragOver = null; }}
              on:drop|preventDefault={() => dropOn(index)}
              on:dragend={() => { dragFrom = null; dragOver = null; }}
            >
            <button
              class="row"
              class:selected={index === selected}
              on:contextmenu={(event) => openRowMenu(index, event)}
              on:click={() => {
                selected = index;
                moreOpen = false;
                rowMenu = null;
              }}
            >
              <span class="rail" aria-hidden="true"></span>
              <span class="step-dot" aria-hidden="true"></span>
              <span class="number">{rowNumber(index)}</span>
              <span class="chip" aria-hidden="true"><StepIcon name={step.type} size={14} /></span>
              <span class="text">
                <span class="title">{stepTitle(step.type)}</span>
                <span class="summary">{stepSummary(step)}</span>
              </span>
              {#if results[index]}
                <!-- 一步的结果写在行上（老外壳也这样）：编号即执行序号，只对没有分组步的流程成立。 -->
                <span class="result" class:bad={!results[index].ok} title={results[index].text}>
                  {results[index].ok ? '✓' : '✗'} {#if results[index].dry}<span class="dry-tag">检查</span>{/if}<span class="result-text">{results[index].text}</span>
                </span>
              {/if}
              {#if childSteps(step).length > 0}
                <span class="count">{childSteps(step).length} 步</span>
              {/if}
              {#if missingOf(step).length > 0}
                <span class="todo" title={missingHint(step)}>待补</span>
              {/if}
              {#if step.shot}
                <!-- 行里只给一个"有图"的记号，不放大图：一行里塞缩略图会让列表变噪，
                     而且按钮里不能再套按钮。图在右边检查器最下方，选中这一步就能看到。 -->
                <span class="shot-mark" title={`这一步有录制时的证据图：${step.shot}`} aria-label="有证据图">▣</span>
              {/if}
            </button>
            <span class="row-actions">
              {#if childSteps(step).length > 0}
                <!-- 展开/收起放在行外：行本身是 button，里面不能再套 button。 -->
                <button
                  class="tiny caret"
                  on:click={() => (expanded = { ...expanded, [index]: !expanded[index] })}
                  aria-label={expanded[index] ? '收起这一步的子步骤' : '展开这一步的子步骤'}
                  aria-expanded={expanded[index] ?? false}
                  title={expanded[index] ? '收起子步骤' : '展开子步骤'}
                >{expanded[index] ? '▾' : '▸'}</button>
              {/if}
              <button class="tiny" on:click={() => stepUp(index)} disabled={index === 0} aria-label="上移" title="上移（Alt+↑）">↑</button>
              <button class="tiny" on:click={() => stepDown(index)} disabled={index === steps.length - 1} aria-label="下移" title="下移（Alt+↓）">↓</button>
              <button class="tiny" on:click={() => deleteStep(index)} aria-label="删除" title="删除（Delete）">×</button>
            </span>
          </div>

          {#if expanded[index]}
            {#each childSteps(step) as child, childIndex}
              <div class="row child">
                <span class="rail" aria-hidden="true"></span>
                <span class="step-dot" aria-hidden="true"></span>
                <span class="number">{rowNumber(index)}.{childIndex + 1}</span>
                <span class="chip" aria-hidden="true"><StepIcon name={child.type} size={14} /></span>
                <span class="text">
                  <span class="title">{stepTitle(child.type)}</span>
                  <span class="summary">{stepSummary(child)}</span>
                </span>
              </div>
            {/each}
          {/if}
        {/each}

        {#if steps.length === 0}
          <p class="blank">这一步还没加。上面那排按钮各加一步，按顺序往下执行。</p>
        {/if}
      </section>

      {#if rowMenu}
        <!-- 右键菜单：与老外壳同一组动作。位置用鼠标点，所以是 fixed + 页面坐标。 -->
        <div class="row-menu" style="left: {rowMenu.x}px; top: {rowMenu.y}px" role="menu">
          <button role="menuitem" on:click={menuDuplicate}>复制这一步</button>
          <button role="menuitem" disabled={rowMenu.index === 0} on:click={menuUp}>上移</button>
          <button role="menuitem" disabled={rowMenu.index === steps.length - 1} on:click={menuDown}>下移</button>
          <button role="menuitem" on:click={menuDelete}>删除这一步</button>
        </div>
      {/if}

      <aside class="side" aria-label="这一步的设置">
        {#if current}
          <Inspector {schema} step={current} onchange={applyField} onpick={pickField} />

          {#if currentShot}
            <h2 class="section">证据</h2>
            <figure class="shot">
              {#if shots[currentShot]?.url}
                <button class="shot-frame" on:click={() => openShot(currentShot, false)} title="用系统看图程序打开">
                  <img src={shots[currentShot].url} alt="这一步当时的样子" />
                </button>
              {:else if shots[currentShot]?.error}
                <p class="shot-hint">读不到这张图：{shots[currentShot].error}</p>
              {:else}
                <p class="shot-hint">正在读…</p>
              {/if}
              <figcaption>
                <span class="shot-name">{currentShot.split(/[\\/]/).pop()}</span>
                <span class="shot-actions">
                  <button class="linkish" on:click={() => openShot(currentShot, false)}>看大图</button>
                  <button class="linkish" on:click={() => openShot(currentShot, true)}>在文件夹里看</button>
                </span>
              </figcaption>
            </figure>
          {/if}

          <button class="disclosure" on:click={() => (jsonOpen = !jsonOpen)} aria-expanded={jsonOpen}>
            JSON <span aria-hidden="true">{jsonOpen ? '⌃' : '⌄'}</span>
          </button>
          {#if jsonOpen}
            <pre class="json">{JSON.stringify(current, null, 2)}</pre>
          {/if}
        {:else}
          <p class="blank side-blank">左边还没有步骤。选中一步，这里显示它的设置。</p>
        {/if}
      </aside>
    {/if}
  </main>

  <footer class="foot">
    <div class="status">
      {#if runtime}
        <span class="status-dot" class:warn={runtime.elevated || !runtime.cliPath}></span>
        {#if runtime.cliPath}
          <span class="mono">{runtime.cliVersion || '（命令行没有回版本号）'}</span>
          <span class="dim">· {runtime.environment === 'development' ? '开发树' : '安装版'}</span>
        {:else}
          <span class="dim">没找到命令行 KeyMouse.exe</span>
        {/if}
      {:else}
        <span class="dim">外壳没连上（浏览器里预览时正常）</span>
      {/if}
      {#if running && progressText(progress)}
        <span class="dim">· 跑到 {progressText(progress)}</span>
      {/if}
      {#if notice}
        <span class="notice" class:bad={notice.bad}>{notice.text}</span>
      {/if}
    </div>
    <button class="output-pill" on:click={() => (outputOpen = !outputOpen)}>
      <svg viewBox="0 0 16 16" aria-hidden="true"><rect x="2.5" y="3.5" width="11" height="8" rx="1.5" /><path d="M5 6.5h6M5 9h3.5" /></svg>
      输出
    </button>
  </footer>

  {#if outputOpen}
    <section class="output" aria-label="运行输出">
      <header>
        <h2>运行输出</h2>
        <div class="output-actions">
          {#if lines.length}
            <button class="ghost small" on:click={() => (lines = [])}>清空</button>
          {/if}
          <button class="ghost small" on:click={() => (outputOpen = false)}>收起</button>
        </div>
      </header>
      {#if lines.length}
        <pre class="run">{#each lines as item}<span class:err={item.stream === 'err'}>{item.line}{'\n'}</span>{/each}</pre>
      {/if}
      {#if runtime}
        <pre class="environment">环境：{runtime.environment}
命令行：{runtime.cliPath || '（没找到）'}
版本：{runtime.cliVersion || '（没回版本号）'}
外壳：{runtime.editorExe}
提权：{runtime.elevated ? '是（不该如此）' : '否（正确）'}</pre>
      {:else}
        <pre>外壳没连上。浏览器里直接打开预览时是正常的——
只有在 `pnpm tauri dev` 起的窗口里才有 IPC。</pre>
      {/if}
    </section>
  {/if}
  </div>
{/if}

<style>
  .shell {
    height: 100%;
    display: grid;
    grid-template-rows: auto auto 1fr auto auto;
    position: relative;
  }

  /* ── 顶栏 ───────────────────────────────────────────── */
  .topbar {
    display: grid;
    grid-template-columns: auto 1fr auto;
    align-items: baseline;
    gap: var(--kp-gap-xl);
    padding: 14px var(--kp-gap-xl) 10px;
  }

  .brand {
    display: flex;
    align-items: baseline;
    gap: 8px;
    font-size: var(--kp-fs-title);
    letter-spacing: -0.01em;
  }

  .mark {
    color: var(--kp-signal);
    font-size: 18px;
    transform: translateY(-1px);
  }

  .product {
    font-weight: 600;
  }

  .kind {
    color: var(--kp-ink2);
    font-size: var(--kp-fs-body);
  }

  /* 打开的是哪个文件：名字在前、完整路径在后，路径可以被截断，名字不行 */
  .doc {
    display: flex;
    align-items: baseline;
    gap: 8px;
    min-width: 0;
  }

  .doc .name {
    font-size: var(--kp-fs-body);
    color: var(--kp-ink);
    white-space: nowrap;
  }

  .doc .name.warn {
    color: var(--kp-ink2);
  }

  .unsaved {
    flex: none;
    padding: 1px 6px;
    border-radius: 999px;
    background: var(--kp-selected);
    color: var(--kp-signal);
    font-size: var(--kp-fs-small);
  }

  .file-actions {
    display: flex;
    align-items: center;
    gap: 2px;
  }

  .file-actions .ghost:disabled {
    opacity: 0.4;
  }

  .path {
    font-family: var(--kp-font-mono);
    font-size: var(--kp-fs-small);
    color: var(--kp-ink3);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    user-select: text;
  }

  /* ── 工具栏 ─────────────────────────────────────────── */
  .toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--kp-gap-lg);
    padding: 0 var(--kp-gap-xl) 12px;
    border-bottom: 1px solid var(--kp-rule);
  }

  .palette,
  .actions {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .palette-item,
  .ghost {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 32px;
    padding: 0 10px;
    border-radius: var(--kp-radius-control);
    color: var(--kp-ink2);
  }

  .palette-item:hover,
  .ghost:hover {
    background: var(--kp-field);
    color: var(--kp-ink);
  }

  .output-pill svg,
  .primary svg {
    width: 16px;
    height: 16px;
    fill: none;
    stroke: currentColor;
    stroke-width: 1.3;
    stroke-linecap: round;
    stroke-linejoin: round;
  }

  .more {
    color: var(--kp-ink2);
  }

  .rec-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    border: 1.5px solid currentColor;
  }

  /* 录制中：按钮变成朱砂，圆点实心并慢慢呼吸——不看文字也知道还在录 */
  .ghost.on {
    color: var(--kp-signal);
    border-color: var(--kp-signal);
    background: color-mix(in srgb, var(--kp-signal) 8%, transparent);
  }

  .ghost.on .rec-dot {
    background: var(--kp-signal);
    animation: rec-pulse 1.6s ease-in-out infinite;
  }

  @keyframes rec-pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.35; }
  }

  @media (prefers-reduced-motion: reduce) {
    .ghost.on .rec-dot { animation: none; }
  }

  .primary {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    height: 34px;
    padding: 0 16px 0 12px;
    border-radius: var(--kp-radius-control);
    background: var(--kp-signal);
    color: #fff;
    font-weight: 500;
  }

  .primary svg.play {
    fill: currentColor;
    stroke: none;
  }

  .primary:hover {
    background: var(--kp-signal-hover);
  }

  .primary:active {
    background: var(--kp-signal-pressed);
  }

  .icon-only {
    font-size: 15px;
    letter-spacing: 1px;
  }

  /* ── 两栏 ───────────────────────────────────────────── */
  .body {
    display: grid;
    grid-template-columns: minmax(420px, 1fr) minmax(360px, 46%);
    min-height: 0;
    padding: var(--kp-gap-lg) var(--kp-gap-xl);
    gap: var(--kp-gap-xl);
  }

  /* ── 空状态 ─────────────────────────────────────────── */
  /* 最近打开：一行一个文件，名字用正文色，目录用弱色（它只是提醒"在哪"）。 */
  .recent {
    margin-top: 22px;
    max-width: 520px;
  }

  .recent h3 {
    margin: 0 0 8px;
    font-size: 12px;
    font-weight: 600;
    color: var(--kp-ink3);
    letter-spacing: 0.04em;
  }

  .recent ul {
    margin: 0;
    padding: 0;
    list-style: none;
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  .recent-item {
    display: flex;
    align-items: baseline;
    gap: 10px;
    width: 100%;
    padding: 6px 8px;
    border: 0;
    border-radius: var(--kp-r-sm);
    background: transparent;
    text-align: left;
    cursor: pointer;
  }

  .recent-item:hover {
    background: var(--kp-field);
  }

  .recent-name {
    flex: none;
    font-size: 13px;
    color: var(--kp-ink);
  }

  .recent-dir {
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-family: var(--kp-mono);
    font-size: 11px;
    color: var(--kp-ink3);
  }
  .empty {
    grid-column: 1 / -1;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
    padding: 40px 44px;
    max-width: 760px;
    align-self: start;
  }

  .empty h2 {
    margin: 0 0 10px;
    font-size: 21px;
    font-weight: 600;
    color: var(--kp-ink);
  }

  .empty p {
    margin: 0 0 18px;
    max-width: 56ch;
    color: var(--kp-ink2);
    line-height: 1.7;
  }

  .empty-actions {
    display: flex;
    align-items: center;
    gap: 10px;
    margin-bottom: 22px;
  }

  .empty .tips {
    margin: 0;
    padding-top: 16px;
    border-top: 1px solid var(--kp-rule);
    font-size: var(--kp-fs-small);
    color: var(--kp-ink3);
  }

  /* 行内代码不折断：`KeyMouse run 流程.json --dry-run` 从中间断行比换行还难读 */
  .empty code {
    font-family: var(--kp-font-mono);
    font-size: 12px;
    padding: 2px 5px;
    border-radius: 4px;
    background: var(--kp-field);
    color: var(--kp-ink2);
    white-space: nowrap;
  }

  /* 列表为空 / 没有选中项时的一句话，不是错误，所以不用朱砂色 */
  .blank {
    margin: 0;
    padding: 28px 20px;
    color: var(--kp-ink3);
    font-size: var(--kp-fs-small);
    line-height: 1.8;
  }

  .side-blank {
    padding: 0;
    max-width: 40ch;
  }

  .list {
    position: relative;
    overflow: auto;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
  }

  /* 穿起所有步骤的那根线：从第一个点到最后一个点，画在行下面，被点上的一点底色盖住。 */
  .list::before {
    content: '';
    position: absolute;
    left: 21px;
    top: 30px;
    bottom: 30px;
    width: 1px;
    background: var(--kp-rule);
  }

  .row {
    position: relative;
    width: 100%;
    display: grid;
    grid-template-columns: 34px 24px 1fr auto;
    align-items: center;
    gap: 10px;
    padding: 11px 16px 11px 40px;
    border-bottom: 1px solid var(--kp-rule);
    text-align: left;
  }

  .row:last-child {
    border-bottom: 0;
  }

  /* 行本身是按钮（整行可点选中），行尾的上移/下移/删除是另一组按钮：
     按 HTML 规则按钮不能套按钮，所以外面再包一层，悬停/选中时才浮出来。 */
  /* 拖拽排序：拖起来的那行淡下去，鼠标压住的那行左边亮一道线。 */
  .row-wrap.dragging {
    opacity: 0.45;
  }

  .row-wrap.drop-target::before {
    content: '';
    position: absolute;
    left: 8px;
    right: 8px;
    top: -1px;
    height: 2px;
    background: var(--kp-signal);
    border-radius: 2px;
  }
  .row-wrap {
    position: relative;
  }

  .row-actions {
    display: none;
    position: absolute;
    right: 10px;
    top: 50%;
    transform: translateY(-50%);
    gap: 2px;
    padding: 2px;
    border-radius: 6px;
    background: var(--kp-card);
  }

  .row-wrap:hover .row-actions,
  .row-wrap:focus-within .row-actions {
    display: flex;
  }

  /* 分组行总是露着展开箭头：老外壳也是这么做的——收起时看不到箭头就不知道里面有东西。 */
  /* 分组行的操作区常驻，得给行右侧留出位置，否则「1 步」被盖住。 */
  .row-wrap.grouped .row {
    padding-right: 132px;
  }

  .row-wrap.grouped .row-actions {
    display: flex;
  }

  .tiny.caret {
    width: 18px;
    color: var(--kp-ink3);
  }

  .tiny.caret:hover {
    color: var(--kp-ink);
    background: transparent;
  }

  .tiny {
    width: 22px;
    height: 22px;
    border: 1px solid transparent;
    border-radius: 5px;
    background: transparent;
    color: var(--kp-ink3);
    font: inherit;
    font-size: 13px;
    line-height: 1;
    cursor: pointer;
  }

  .tiny:hover:not(:disabled) {
    border-color: var(--kp-rule);
    color: var(--kp-ink);
  }

  .tiny:disabled {
    opacity: 0.3;
    cursor: default;
  }

  /* 待补：这一步还缺必填项。比一个红色错误标温和，比什么都不说清楚。 */
  .todo {
    font-size: var(--kp-fs-small);
    color: var(--kp-signal);
    border: 1px solid rgb(194 65 12 / 0.25);
    border-radius: 999px;
    padding: 1px 7px;
  }


  .sep {
    width: 1px;
    height: 16px;
    margin: 0 6px;
    background: var(--kp-rule);
  }

  /* 「+ 更多」：一张小卡片，按用途分组。刻意不做成系统菜单——它只是四个常用按钮的补充。 */
  .palette {
    position: relative;
  }

  .palette-item.open {
    background: var(--kp-field);
    color: var(--kp-ink);
  }

  .more-menu {
    position: absolute;
    right: 0;
    top: 38px;
    z-index: 20;
    width: 260px;
    padding: 8px;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
    box-shadow: 0 10px 30px rgb(23 20 15 / 0.08);
  }

  .more-group {
    margin: 6px 0 2px;
    padding: 0 8px;
    font-size: var(--kp-fs-small);
    color: var(--kp-ink3);
  }

  .more-item {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    height: 28px;
    padding: 0 8px;
    border: 0;
    border-radius: 5px;
    background: transparent;
    color: var(--kp-ink);
    font: inherit;
    font-size: var(--kp-fs-body);
    text-align: left;
    cursor: pointer;
  }

  .more-item:hover {
    background: var(--kp-field);
  }

  /* 右键菜单：跟着鼠标点出现，所以用 fixed 定位，坐标即 clientX/clientY。 */
  .row-menu {
    position: fixed;
    z-index: 40;
    display: flex;
    flex-direction: column;
    min-width: 132px;
    padding: 4px;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
    box-shadow: 0 8px 24px rgba(23, 20, 15, 0.14);
  }

  .row-menu button {
    padding: 7px 10px;
    border: 0;
    border-radius: 4px;
    background: transparent;
    color: var(--kp-ink);
    font: inherit;
    font-size: 13px;
    text-align: left;
    cursor: pointer;
  }

  .row-menu button:hover:not(:disabled) {
    background: var(--kp-selected);
  }

  .row-menu button:disabled {
    color: var(--kp-ink3);
    cursor: default;
  }

  .row-menu button + button {
    margin-top: 1px;
  }
  .more-tip {
    margin: 8px 0 2px;
    padding: 6px 8px 0;
    border-top: 1px solid var(--kp-rule);
    font-size: var(--kp-fs-small);
    color: var(--kp-ink3);
  }

  .row:hover {
    background: var(--kp-field);
  }

  .row.selected {
    background: var(--kp-selected);
  }

  .row .rail {
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 2px;
    background: transparent;
  }

  .row.selected .rail {
    background: var(--kp-signal);
  }

  /* 步骤前的点：空心是"待跑"，实心朱砂是"当前这一步"。绝对定位是为了压在线上，
     底色把身后那根线遮住，看起来才像串起来的一列。 */
  .step-dot {
    position: absolute;
    left: 17px;
    top: 50%;
    transform: translateY(-50%);
    width: 9px;
    height: 9px;
    border-radius: 50%;
    border: 1px solid var(--kp-ink3);
    background: var(--kp-card);
  }

  .row:hover .step-dot {
    background: var(--kp-field);
  }

  .row.selected .step-dot {
    border-color: var(--kp-signal);
    background: var(--kp-signal);
    box-shadow: 0 0 0 2px var(--kp-card);
  }

  .number {
    font-family: var(--kp-font-mono);
    font-size: var(--kp-fs-small);
    color: var(--kp-ink3);
    text-align: right;
  }

  .row.selected .number {
    color: var(--kp-ink2);
  }

  .chip {
    display: grid;
    place-items: center;
    width: 24px;
    height: 24px;
    border-radius: 6px;
    background: var(--kp-field);
    color: var(--kp-ink2);
  }

  .row.selected .chip {
    background: var(--kp-signal);
    color: #fff;
  }

  .text {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .title {
    font-size: var(--kp-fs-row-title);
    line-height: 1.35;
  }

  .summary {
    font-size: var(--kp-fs-small);
    color: var(--kp-ink2);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  /* 行上的结果：成功用弱色（它只是"跑过了"的痕迹），失败才用警示色。 */
  /* 试运行的行上结果：多一个"检查"标签，免得和真跑混为一谈。 */
  .dry-tag {
    margin-right: 2px;
    padding: 0 4px;
    border: 1px solid var(--kp-rule);
    border-radius: 3px;
    color: var(--kp-ink3);
    font-size: 11px;
  }
  .result {
    display: inline-flex;
    align-items: baseline;
    gap: 4px;
    max-width: 220px;
    font-size: 11px;
    color: var(--kp-ink3);
    white-space: nowrap;
  }

  .result.bad {
    color: var(--kp-critical);
  }

  .result-text {
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .count {
    font-size: var(--kp-fs-small);
    color: var(--kp-ink3);
  }

  .row.child {
    /* 子步骤不再整体右移：点还得落在那根线上，缩进靠序号自己表达。 */
    border-bottom: 0;
    padding-top: 6px;
    padding-bottom: 6px;
  }

  .row.child .title,
  .row.child .summary {
    font-size: var(--kp-fs-small);
    color: var(--kp-ink2);
  }

  /* ── 右栏 ───────────────────────────────────────────── */
  .side {
    /* 网格子项默认 min-height:auto 会撑到内容那么高，overflow:auto 就永远不触发；
       显式给 0 才真的在这里滚。 */
    min-height: 0;
    overflow: auto;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
    padding: 18px 20px 24px;
  }


  h2 {
    margin: 0;
    font-size: 18px;
    font-weight: 600;
    letter-spacing: -0.01em;
  }

  h2.section {
    margin-top: 22px;
    padding-bottom: 10px;
    border-bottom: 1px solid var(--kp-rule);
  }











  .disclosure {
    display: flex;
    width: 100%;
    align-items: center;
    justify-content: space-between;
    margin-top: 22px;
    padding: 10px 0;
    border-top: 1px solid var(--kp-rule);
    color: var(--kp-ink2);
    font-size: var(--kp-fs-small);
    letter-spacing: 0.04em;
  }

  /* 证据图：录制时留下的那一张。图本身就是内容，所以只给一条细边，
     不套卡片阴影——它不该比步骤列表更抢眼。 */
  .shot {
    margin: 12px 0 4px;
  }

  /* 框跟着图走，不铺满整列：铺满的话右边会留一条空白米色，看着像图没加载完 */
  .shot-frame {
    display: inline-block;
    padding: 0;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-control);
    background: var(--kp-field);
    cursor: zoom-in;
    overflow: hidden;
  }

  .shot-frame img {
    display: block;
    max-width: 100%;
    /* 140px 是"一眼认得出来是哪个窗口"的最小值；再大就把检查器的其它字段挤到滚动外面。
       看细节点这张图。 */
    max-height: 140px;
  }

  .shot figcaption {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 12px;
    margin-top: 6px;
    color: var(--kp-ink3);
    font-size: var(--kp-fs-small);
  }

  .shot-name {
    font-family: var(--kp-font-mono);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .shot-actions {
    display: flex;
    gap: 12px;
    flex: none;
  }

  /* 有证据的行：一个安静的记号，不抢标题的位置 */
  .shot-mark {
    flex: none;
    color: var(--kp-ink3);
    font-size: 12px;
    line-height: 1;
  }

  .row.selected .shot-mark {
    color: var(--kp-signal);
  }

  /* 文字按钮：像链接一样安静，但有按钮的点击区与焦点环 */
  .linkish {
    padding: 0;
    border: 0;
    background: none;
    color: var(--kp-ink2);
    font: inherit;
    text-decoration: underline;
    text-decoration-color: var(--kp-rule);
    text-underline-offset: 2px;
    cursor: pointer;
  }

  .linkish:hover {
    color: var(--kp-signal);
    text-decoration-color: currentColor;
  }

  .shot-hint {
    margin: 0;
    padding: 14px;
    border: 1px dashed var(--kp-rule);
    border-radius: var(--kp-radius-control);
    color: var(--kp-ink3);
    font-size: var(--kp-fs-small);
  }

  /* 这一步的原始 JSON：只读，给人核对用；不参与编辑 */
  .json {
    margin: 0 0 4px;
    padding: 12px 14px;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-control);
    background: var(--kp-field);
    color: var(--kp-ink2);
    font-family: var(--kp-font-mono);
    font-size: 12px;
    line-height: 1.65;
    overflow: auto;
    max-height: 260px;
    user-select: text;
  }

  /* 底部那一行"刚才发生了什么"：正常是灰的，出问题是朱砂的 */
  .notice {
    margin-left: 6px;
    color: var(--kp-ink3);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .notice.bad {
    color: var(--kp-critical);
  }

  /* ── 底部 ───────────────────────────────────────────── */
  .foot {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--kp-gap-lg);
    padding: 0 var(--kp-gap-xl) 14px;
  }

  .status {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: var(--kp-fs-small);
    color: var(--kp-ink2);
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  /* 录制期间的细带。窗口被外壳缩成 420×52，这里只排一行。 */
  .mini {
    display: flex;
    align-items: center;
    gap: 10px;
    height: 100%;
    padding: 0 8px 0 4px;
    background: var(--kp-paper);
    border: 1px solid var(--kp-rule);
    border-radius: 8px;
    box-sizing: border-box;
    user-select: none;
  }

  /* 抓手：整条都能拖（外壳按 HTCAPTION 处理不了 webview，所以左端给一块明确区域） */
  .mini-drag {
    flex: none;
    width: 14px;
    align-self: stretch;
    border-radius: 4px 0 0 4px;
    background-image: radial-gradient(var(--kp-ink3) 1px, transparent 1px);
    background-size: 4px 4px;
    background-position: 3px 50%;
    background-repeat: repeat-y;
    cursor: grab;
  }

  .mini-title {
    flex: none;
    white-space: nowrap;
    font-weight: 600;
    font-size: 13px;
    color: var(--kp-ink);
  }

  .mini-clock {
    flex: none;
    white-space: nowrap;
    font-family: var(--kp-mono);
    font-size: 12px;
    color: var(--kp-ink2);
  }

  .mini-hint {
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: 12px;
    color: var(--kp-ink3);
  }

  .mini-stop {
    flex: none;
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 28px;
    padding: 0 12px;
    border: 1px solid var(--kp-signal);
    border-radius: 999px;
    background: var(--kp-signal);
    color: var(--kp-paper);
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
  }

  .mini-stop:hover {
    background: #a8380a;
    border-color: #a8380a;
  }

  .mini-stop .square {
    width: 9px;
    height: 9px;
    border-radius: 2px;
    background: var(--kp-paper);
  }

  .ops-group {
    position: relative;
    display: flex;
    align-items: center;
  }

  .ops-menu {
    position: absolute;
    right: 0;
    top: 38px;
    z-index: 30;
    display: flex;
    flex-direction: column;
    min-width: 190px;
    padding: 5px;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
    box-shadow: 0 10px 26px rgba(23, 20, 15, 0.16);
  }

  .ops-menu button {
    padding: 7px 10px;
    border: 0;
    border-radius: 5px;
    background: transparent;
    color: var(--kp-ink);
    font: inherit;
    text-align: left;
    cursor: pointer;
  }

  .ops-menu button:hover {
    background: var(--kp-selected);
  }

  .ops-menu button:disabled {
    color: var(--kp-ink3);
    cursor: default;
  }

  .ops-menu hr {
    margin: 4px 6px;
    border: 0;
    border-top: 1px solid var(--kp-rule);
  }
  .record-group {
    position: relative;
    display: flex;
    align-items: center;
    gap: 0;
  }

  .record-group .ghost:first-child {
    border-top-right-radius: 0;
    border-bottom-right-radius: 0;
  }

  .record-group .rec-caret {
    padding: 0 7px;
    border-left: 0;
    border-top-left-radius: 0;
    border-bottom-left-radius: 0;
    font-size: 10px;
    color: var(--kp-ink3);
  }

  .record-menu {
    position: absolute;
    right: 0;
    top: 38px;
    z-index: 30;
    display: flex;
    flex-direction: column;
    min-width: 248px;
    padding: 5px;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
    box-shadow: 0 10px 26px rgba(23, 20, 15, 0.16);
  }

  .record-menu button {
    padding: 7px 10px;
    border: 0;
    border-radius: 5px;
    background: transparent;
    color: var(--kp-ink);
    font: inherit;
    text-align: left;
    cursor: pointer;
  }

  .record-menu button:hover {
    background: var(--kp-selected);
  }

  .record-tip {
    margin: 6px 8px 3px;
    max-width: 230px;
    color: var(--kp-ink3);
    font-size: 11px;
    line-height: 1.5;
  }
  .status-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--kp-signal);
  }

  .status-dot.warn {
    background: var(--kp-critical);
  }

  .mono {
    font-family: var(--kp-font-mono);
  }

  .dim {
    color: var(--kp-ink3);
  }

  .output-pill {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    height: 34px;
    padding: 0 16px;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-control);
    background: var(--kp-card);
    color: var(--kp-ink2);
  }

  .output-pill:hover {
    border-color: var(--kp-ink3);
    color: var(--kp-ink);
  }

  /* 运行输出是页面的一部分，不是浮层：它出现时把上面两栏压缩，而不是盖住列表。
     浮层在窄窗口里会把内容挡掉一半，而这一块恰恰是出问题时最需要看的地方。 */
  .output {
    grid-row: 5;
    margin: 0 var(--kp-gap-xl) 12px;
    border: 1px solid var(--kp-rule);
    border-radius: var(--kp-radius-panel);
    background: var(--kp-card);
    padding: 14px 16px 16px;
    max-height: 36vh;
    overflow: auto;
  }

  .output header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding-bottom: 8px;
  }

  .output h2 {
    font-size: var(--kp-fs-body);
    color: var(--kp-ink2);
    font-weight: 500;
  }

  .ghost.small {
    height: 26px;
    font-size: var(--kp-fs-small);
  }

  .output-actions {
    display: flex;
    gap: 4px;
  }

  /* 命令行那一行行原样收着：它是证据，不重排、不截断。 */
  .output pre.run {
    margin-bottom: 10px;
  }

  .output pre.run .err {
    color: var(--kp-critical);
  }

  /* 环境那几行是"关于这次运行的前提"，比输出淡一档，免得抢走失败原因的注意力。 */
  .output pre.environment {
    color: var(--kp-ink2);
    background: transparent;
    border: 1px dashed var(--kp-rule);
    font-size: var(--kp-fs-tiny, 12px);
  }

  /* 停止是"跑着的时候才有"的动作：朱砂实心会让人以为它在吸引你去点，用细边就够。 */
  .stop {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    flex: none;
    white-space: nowrap;
    height: 32px;
    padding: 0 14px;
    border-radius: var(--kp-radius-control);
    border: 1px solid rgb(179 38 30 / 0.45);
    color: var(--kp-critical);
    background: var(--kp-card);
  }

  .stop:hover {
    background: rgb(179 38 30 / 0.06);
  }

  .stop .play {
    fill: currentColor;
  }

  .output pre {
    margin: 0;
    padding: 12px;
    border-radius: var(--kp-radius-control);
    background: var(--kp-field);
    font-family: var(--kp-font-mono);
    font-size: var(--kp-fs-small);
    line-height: 1.7;
    white-space: pre-wrap;
    user-select: text;
  }
</style>
