/**
 * 跑完之后的结论怎么说。
 *
 * 三件事分开说：被"停止"掐掉的不是失败；退出码 0 才是全成功；拿不到退出码要如实讲。
 * 这几句是界面唯一需要"理解"退出码的地方，所以单独放这儿，配单测。
 */

/** 命令行文档里约定的退出码含义。0 之外都可能出现，这里只给最常见的几个。 */
const CODE_HINTS: Record<number, string> = {
  1: '有一步没做成',
  2: '流程文件本身读不懂',
  3: '窗口不满足条件（没找到、没最小化、没解锁）',
  4: '编辑器路径不对',
  5: '锁屏或桌面不可用',
  6: '读屏没读出东西',
};

export function runConclusion(code: number | null, cancelled: boolean, dryRun = false): string {
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
  return hint ? `没跑通（退出码 ${code}）：${hint}。${only}` : `没跑通（退出码 ${code}）。${only}`;
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
    const detail = (entry.command ?? '').trim();
    let text: string;
    if (entry.skipped) text = detail ? `跳过：${detail}` : '跳过';
    else if (code === 0) text = detail;
    else text = detail ? `退出码 ${code}：${detail}` : `退出码 ${code}`;

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



