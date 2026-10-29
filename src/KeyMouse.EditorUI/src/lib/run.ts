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

export function runConclusion(code: number | null, cancelled: boolean): string {
  if (cancelled) return '已停止。';

  if (code === null) return '跑完了，但没拿到退出码——这通常意味着进程被杀掉了。';
  if (code === 0) return '跑完了，每一步都成功。';

  const hint = CODE_HINTS[code];
  return hint ? `没跑通（退出码 ${code}）：${hint}。` : `没跑通（退出码 ${code}）。`;
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
