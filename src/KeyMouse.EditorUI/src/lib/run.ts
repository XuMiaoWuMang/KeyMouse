/**
 * 跑完之后的结论怎么说。
 *
 * 三件事分开说：被"停止"掐掉的不是失败；退出码 0 才是全成功；拿不到退出码要如实讲。
 * 这几句是界面唯一需要"理解"退出码的地方，所以单独放这儿，配单测。
 */

/**
 * 退出码的含义，逐条对齐 `docs/reference/cli.md` 的「退出码」表。
 *
 * 这里曾把 4 写成"编辑器路径不对"——那一版是凭印象编的，用户看到的是
 * "退出码 4：编辑器路径不对"，而真相是"目标窗口被最小化了"。界面上报原因时
 * 要么照抄文档，要么只报数字，别猜。
 */
const CODE_HINTS: Record<number, string> = {
  1: '有一步运行时失败（比如输入被系统拦下）',
  2: '流程文件读不懂（参数或结构不对）',
  3: '没找到符合条件的窗口或文字',
  4: '目标窗口不可用（最小化、隐藏或被系统挡住）',
  5: '焦点没抢到，这一步没有发送任何输入',
  6: '没看清（读屏多次结果不一致）',
  7: '被停止或取消，取消点之后没有发送输入',
};

export function runConclusion(
  code: number | null,
  cancelled: boolean,
  dryRun = false,
  reason = '',
): string {
  if (cancelled) return '已停止。';

  // 试运行和播放的输出长得一模一样，唯一区别是它**没有真的发输入**。
  // 所以这里必须把"检查过了"和"做完了"分开说，否则用户会对着空白的记事本
  // 看到"每一步都成功"。
  const only = dryRun ? '（试运行只检查，没有真的执行）' : '';

  if (code === null) return `跑完了，但没拿到退出码——这通常意味着进程被杀掉了。${only}`;
  if (code === 0) {
    return dryRun ? `检查通过：每一步都找得到窗口。${only}` : '跑完了，每一步都成功。';
  }

  const hint = CODE_HINTS[code];
  // 光有"退出码 4 是什么"还不够：真正让用户明白的是那一步自己说了什么
  //（"目标窗口不可用：HYP(27036) 米哈游启动器 已最小化"）。有就带上，没有就不编。
  const why = reason ? (hint ? `${hint}；${reason}` : reason) : hint;
  return why ? `没跑通（退出码 ${code}）：${why}。${only}` : `没跑通（退出码 ${code}）。${only}`;
}

/** 一句话版的失败原因：命令行 `output` 的第一行，去掉前缀、压成一行、截短。 */
export function firstLine(text: string): string {
  const line =
    text
      .split(/\r?\n/)
      .map((part) => part.trim())
      .find((part) => part.length > 0) ?? '';
  const plain = line.replace(/^错误[：:]\s*/, '');
  return plain.length > 72 ? `${plain.slice(0, 72)}…` : plain;
}

/** 结束时的语气是不是"坏消息"：只有真的失败才把底部那行标红。 */
export function runIsBad(code: number | null, cancelled: boolean): boolean {
  return !cancelled && code !== 0;
}

/** 试运行与播放用的是同一条命令，只差 `--dry-run`；这句提示写给"跑之前"看。 */
export function runHint(dryRun: boolean): string {
  return dryRun
    ? '试运行：只做资格检查，不发送任何输入。'
    : '播放：会真的操作鼠标键盘，跑的时候别抢窗口。';
}

/** 命令行报告的一步结果。 */
export interface StepResult {
  ok: boolean;
  /** 命令行给的描述，例如 `等 6000 ms`、`退出码 3：窗口不满足条件`。 */
  text: string;
  /** 这一行是试运行那一轮的结果：界面上要标"检查"，否则和真跑长得一模一样。 */
  dry?: boolean;
}

/** 报告里的一步。字段名与命令行 `--report` 写出的 JSON 一致。 */
interface ReportCommand {
  index?: number;
  origin?: string;
  command?: string;
  exitCode?: number;
  skipped?: boolean;
  /** 这一步自己说的话：失败时是错误原因（首行就够），成功时是它做了什么。 */
  output?: string;
}

/**
 * 把命令行的 `--report` 读成"每个界面行一个结果"，键是行在文档里的位置（"3"、"6.1"）。
 *
 * 为什么不再靠解析 `[ 3/8 ]`：那个编号是**执行序号**，分组步会展开、子流程会内联，于是
 * 它对不上行号。报告里的 `origin` 是命令行按文档结构算出来的，分组步的每一次执行都指向
 * 同一行走；循环跑三遍时最后一次的结果留在那一行上——这正是"这一行长什么样"该表达的。
 */
export function resultsFromReport(text: string, dry: boolean): Record<string, StepResult> {
  if (!text.trim()) return {};

  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    return {};
  }

  const commands = (parsed as { commands?: unknown }).commands;
  if (!Array.isArray(commands)) return {};

  const results: Record<string, StepResult> = {};
  for (const raw of commands) {
    const entry = raw as ReportCommand;
    const origin = typeof entry.origin === 'string' ? entry.origin : '';
    if (!origin) continue; // 脚本模式的报告没有行号，跳过比编一个更诚实

    const code = typeof entry.exitCode === 'number' ? entry.exitCode : 0;
    const command = (entry.command ?? '').trim();
    // 失败时把命令行自己说的话摆到行上：用户要知道的是"窗口被最小化了"，
    // 而不是"KeyMouse window focus --process HYP"。
    const said = !entry.skipped && code !== 0 ? firstLine(entry.output ?? '') : '';
    let text: string;
    if (entry.skipped) text = command ? `跳过：${command}` : '跳过';
    else if (code === 0) text = command;
    else text = said ? `退出码 ${code}：${said}` : command ? `退出码 ${code}：${command}` : `退出码 ${code}`;

    results[origin] = { ok: !entry.skipped && code === 0, text, dry };
  }

  // 子流程内联、循环展开时，命令行给的是 `2.1`、`2.2` 这种位置，而"调用另一个流程"
  // 那一行自己没有命令。往上卷一层：父行只要没有自己的结果，就用孩子的结果汇总一句，
  // 否则用户会看到「调用另一个流程」那行永远是空的——明明它跑过了。
  for (const key of Object.keys(results)) {
    if (!key.includes('.')) continue;
    const parent = key.slice(0, key.lastIndexOf('.'));
    if (results[parent]) continue;

    const kids = Object.entries(results).filter(([child]) => child.startsWith(parent + '.'));
    if (kids.length === 0) continue;

    const failed = kids.find(([, value]) => !value.ok);
    results[parent] = failed
      ? { ok: false, text: failed[1].text, dry }
      : { ok: true, text: `${kids.length} 步都成功`, dry };
  }

  return results;
}




/** 第一个失败的行：结论里那句"为什么"取它的原因，而不是再猜一遍。 */
export function firstFailure(results: Record<string, StepResult>): string {
  const failing = Object.keys(results).filter((key) => !results[key].ok);
  if (failing.length === 0) return '';

  // `6.e1` 里的 `e` 不是数字：否则分支排在成立时之后，而不是被 Number('e1') 当成 0 混到前面。
  const order = (key: string) =>
    key.split('.').map((part) => {
      const branch = /^e(\d+)$/.exec(part);
      return branch ? 1000 + Number(branch[1]) : Number(part) || 0;
    });
  failing.sort((a, b) => {
    const left = order(a);
    const right = order(b);
    for (let i = 0; i < Math.max(left.length, right.length); i += 1) {
      const diff = (left[i] ?? -1) - (right[i] ?? -1);
      if (diff !== 0) return diff;
    }
    return 0;
  });

  return results[failing[0]].text.replace(/^退出码 \d+：/, '');
}