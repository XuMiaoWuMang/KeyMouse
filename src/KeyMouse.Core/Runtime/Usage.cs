namespace KeyMouse.Runtime;
/// <summary>
/// 帮助文档。
///
/// 这里存的是**条目**，不是对齐好的字符串：命令列与说明列的宽度由渲染器按终端当前宽度算，
/// 中文字按两格算（见 ConsoleText.DisplayWidth）。手对齐的大字符串一旦改一行就会整片歪掉，
/// 而且换台机器、换字号、换码页都会歪——那不是排版，那是运气。
/// </summary>
internal static class Usage
{
    internal sealed record Entry(string Command, string Description);

    internal sealed record Section(string Title, string[]? Note, Entry[] Entries);

    public static void Print() => Console.Write(Render());

    /// <summary>命令列的宽（单元格）。宽于它的命令独占一行，说明另起一行并保持同一左缘。</summary>
    private const int Gutter = 34;

    private const int MinWidth = 64;

    private const int MaxWidth = 100;

    private static readonly Section[] Sections =
    [
        new("鼠标", new[] { "键：left（默认）| right | middle | x1 | x2" },
        [
            new("mouse pos", "打印当前光标坐标"),
            new("mouse move <x> <y>", "移动到绝对屏幕像素"),
            new("mouse move -wx <cx> -wy <cy> <选择器>", "移动到某窗口客户区的点"),
            new("mouse moveby <dx> <dy>", "相对移动"),
            new("mouse click [键] [-x X -y Y] [-n 次数] [-i 毫秒]", ""),
            new("mouse dblclick [键] [-x X -y Y]", "双击（等价于 click -n 2）"),
            new("mouse down [键] / mouse up [键]", "按住 / 松开"),
            new("mouse wheel <增量> [-x X -y Y]", "120 = 一格，向上为正（向下滚 = -120）"),
            new("mouse hwheel <增量>", "横向滚轮"),
            new("mouse drag <x1> <y1> <x2> <y2> [--button 键] [--steps 步数] [--duration 毫秒]", ""),
            new("mouse drag -wx <cx1> -wy <cy1> --wx2 <cx2> --wy2 <cy2> <选择器>", "在两个客户区坐标点之间拖拽"),
        ]),

        new("键盘", null,
        [
            new("key press <按键> [-n 次数] [-i 毫秒]", "敲击，可连按"),
            new("key down <按键> / key up <按键>", "按下 / 抬起"),
            new("key combo <键1+键2+...> [--hold 毫秒]", "组合键，如 ctrl+shift+s、win+r、alt+f4"),
            new("key type <文本> [--interval 毫秒]", "输入文本（Unicode 注入，不依赖输入法/键盘布局）。默认每字 15ms；--interval 0 最快但可能丢字"),
        ]),

        new("窗口", null,
        [
            new("window list [--filter <标题子串>] [--process <进程名>] [--all]", "列出顶层窗口及其状态"),
            new("window inspect <选择器>", "说明某个窗口为什么可用 / 不可用"),
            new("window focus <选择器>", "只做聚焦与验证，不发送任何输入"),
        ]),

        new("识图（读，不是发）", new[]
        {
            "probe 把窗口的一块区域读成文字，只报告看到什么，不做任何判断。",
            "--region 是客户区相对坐标（--space window 则相对整个窗口）；省略即整个客户区。越界一律拒绝，不猜也不截断。",
        },
        [
            new("probe <选择器> [--region x,y,w,h] [选项]", ""),
            new("probe --pick-region [选项]", "先用鼠标框一块区域（单击则选整个客户区），然后读它；不要再给选择器 / --region"),
            new("--region x,y,w,h", "要读的那一块"),
            new("--space client|window", "区域坐标系，默认 client（标题栏在 client 之外）"),
            new("--pick-region", "打开全屏选区浮层：拖动框选、单击选整个客户区、ESC 取消"),
            new("--reads N", "重复读取次数，默认 2；N 次必须逐字一致才算看清"),
            new("--lang <语言>", "默认 eng+chi_sim"),
            new("--engine <命令>", "OCR 引擎，默认 tesseract（会自动找常见安装位置）"),
            new("--tessdata-dir <目录>", "模型目录；默认 %LOCALAPPDATA%\\KeyMouse\\tessdata"),
            new("--min-conf N", "置信度地板，默认 30；低于它判为没看清"),
            new("--scale N / --pad N", "放大倍数（默认 1）与白边像素（默认 0）：默认把抓到的原始像素直接交给引擎，不做任何变换"),
            new("--normalize", "先把对比度拉到黑白（暗底浅字的窗口读不出来时用）"),
            new("--resample nearest|bilinear", "放大时的取样方式（默认 bilinear；nearest 把每个源像素复制成 N×N 方块，不发明像素）"),
            new("--capture screen|print", "取像方式：默认 screen；print 用 PrintWindow 不画光标，但不能用于 --space window（标题栏），且本机几何对不齐（见 docs/design.md）"),
            new("--json", "输出 JSON（引擎身份、几何、逐词置信度）"),
            new("--keep-image <路径>", "原样保存抓到的像素（不放大、不加白边、不做对比度归一化）——截图就是屏幕上那一块"),
            new("--keep-prepared <路径>", "保存送进引擎的那张图（放大 + 白边 + 归一化之后）"),
            new("--find <文字>", "读完顺手找这几个字：找到了退出码 0，没找到退出码 3（读成功与否仍按前面几条判）"),
            new("--match contains|exact|fuzzy", "--find 的比较方式，默认 contains"),
            new("--max-errors N", "--match fuzzy 的容错预算（按字符计的编辑距离，默认 1，上限 20）"),
        ]),

        new("选区（给人挑坐标，不读文字）", null,
        [
            new("region pick [--rect x,y,w,h] [--point] [--json]", "框一块区域，报告它落在哪个窗口的哪个坐标系里，并给出一条可直接粘贴的 probe 命令。--point 改成单击取一个点（点击类动作用它）；--rect 跳过浮层（脚本与测试用）"),
        ]),

        new("录制（把一次操作变成可回放的 JSON 流程）", new[]
        {
            "开始录制：全局监听键鼠（只观察、不拦截，你照常操作），每个动作连同它发生的窗口与客户区相对坐标写成 JSON。",
            "Ctrl+Alt+Q 停止（连按两次 ESC 也停）；--duration 到点自动停。鼠标轨迹按距离抽稀后一起录；点击/输入等步骤会在 <流程>.shots/ 里各存一张 320x200 的截图。回放就是：KeyMouse run 流程.json",
        },
        [
            new("record [--out 流程.json] [--duration 毫秒] [--no-shots] [--min-gap 毫秒] [--move-threshold 像素]", ""),
        ]),

        new("常驻服务（给图形界面用）", new[]
        {
            "启动本地 Runner：一个进程里常驻着同一套能力层，客户端通过命名管道连它（默认 \\.\\pipe\\keymouse-runner-<用户名>）拿流式事件：",
            "run / record / validate / pick-region / ocr / cancel / pause / resume / status / list / shutdown，一行一个 JSON 对象。",
            "编辑器连的就是它；CLI 的其余命令照旧是一次性执行，两者跑的是同一份派发。",
        },
        [
            new("serve", ""),
        ]),

        new("流程（录制出来的 JSON，以及图形编辑器）", new[]
        {
            "用 WinUI 图形编辑器打开一个流程：左边是步骤列表，右边改参数，「从屏幕取区域」直接拉选区浮层，「试运行/播放」调用的还是 run 本身。",
            "编辑器在 src\\KeyMouse.FlowEditor，需要单独构建：pwsh tools\\dev\\km.ps1 ba（km ba 同时会重新打包命令行）。",
        },
        [
            new("flow edit <流程.json>", ""),
        ]),

        new("脚本", new[]
        {
            "run 按行顺序执行文件（或 stdin）里的命令；一行一条命令，# 开头是注释，另有三个伪命令：sleep、waitfor、waitgone。",
            "选择器与焦点闸门逐行生效；脚本必须是 UTF-8。给 .json 文件就是回放录制出来的流程，选项含义相同（--dry-run / --report / --retry）。",
            "window focus 在脚本里只要写一次：它同时成为\"当前目标\"，后面的 mouse / key 行不带选择器时自动继承（继承的行仍然各自过一遍焦点与闸门验证）。",
            "repeat <次数> [as <名字>] …… end 是唯一的循环；循环变量默认叫 i，从 0 开始计，嵌套时内层必须 as 取名，结构错误在第一条命令执行之前就带着行号报错。",
        },
        [
            new("run <文件|-> [选项]", ""),
            new("sleep <毫秒>", "等待"),
            new("waitfor <选择器> [--timeout 毫秒] [--interval 毫秒]", "等到有可用窗口匹配；超时则退出码 3（默认 5000 / 200 毫秒）"),
            new("waitgone <选择器> [--timeout 毫秒] [--interval 毫秒]", "等到没有可用窗口匹配（--timeout 0 = 立刻断言）"),
            new("--delay 毫秒", "每条命令之间等待多久（默认 0）"),
            new("--keep-going", "失败也继续（默认首错即停）"),
            new("--dry-run", "照常做资格检查，但不发送任何输入、也不抢焦点"),
            new("--echo", "连子命令自己的输出一起打印"),
            new("--retry 次数", "失败后最多再试几次——只对\"确定没发出输入\"的失败重试（退出码 3/4/5）；做了一半的动作绝不重复"),
            new("--retry-delay 毫秒", "重试前等待（默认 300）"),
            new("--set 名=值", "定义变量，脚本里用 ${name} 引用（可重复给；含空格的值仍然是一个参数）"),
            new("--report 文件.json", "写出机器可读的执行报告"),
        ]),

        new("窗口选择器（多个条件之间是 AND；鼠标 / 键盘 / 窗口命令都接受）", null,
        [
            new("--title <子串>", "标题子串，大小写不敏感"),
            new("--title-exact <文本>", "标题完全匹配"),
            new("--class <类名>", "窗口类名"),
            new("--process <进程名>", "例如 notepad、QQ"),
            new("--pid <进程号>", "进程 ID"),
            new("--hwnd <0x1234|1234>", "窗口句柄"),
            new("--pick <n>", "多个可用候选时选第 n 个（1-based，Z 序从上到下）"),
        ]),

        new("提权", null,
        [
            new("KeyMouse --elevate <命令…>", "以管理员身份重跑同一条命令（UAC 提示一次），退出码原样带回；用户拒绝时返回 1223 且不执行任何操作"),
            new("（编辑器无需此参数）", "编辑器自带 requireAdministrator：启动即提权，否则录不到管理员窗口上的操作"),
        ]),

        new("窗口策略", null,
        [
            new("--focus-policy never|gentle|none", "never（默认）：不碰焦点，窗口只用来换算坐标与校验资格。gentle：SetForegroundWindow 后回读验证，最多 3 次。none：要求它已经是前台窗口"),
            new("--focus-attempts <n>", "温和模式的尝试次数（默认 3）"),
            new("--allow-restore", "允许 KeyMouse 还原最小化窗口（默认关；流程文件里可写 allowRestore: true，两者任一即可）"),
            new("-wx <cx> -wy <cy>", "客户区坐标（必须成对出现，且必须带选择器）；在 mouse drag 里它是起点，终点是 --wx2 <cx2> --wy2 <cy2>（四个必须一起给）"),
            new("--strict-point", "额外要求该屏幕点下方的窗口就是目标本身"),
        ]),

        new("按键名", new[]
        {
            "a-z  0-9  f1-f24  esc enter tab space backspace delete insert home end",
            "pageup pagedown up down left right ctrl shift alt win rwin apps capslock",
            "num0-num9 numadd numsub nummul numdiv numdecimal numenter numlock",
            "printscreen pause  semicolon equals comma minus period slash grave",
            "lbracket rbracket backslash quote  vk:0x5B（裸虚拟键逃生口，0x 可省略）",
        }, []),

        new("例子", new[]
        {
            "KeyMouse mouse move 100 200",
            "KeyMouse mouse click right -x 640 -y 480",
            "KeyMouse mouse click left -wx 120 -wy 340 --title \"记事本\"",
            "KeyMouse key type \"hello 世界\" --process notepad",
            "KeyMouse window list --process QQ",
            "KeyMouse window inspect --title \"记事本\"",
        }, []),

        new("环境与排障", new[]
        {
            "开发树与安装版的目录形态不同：开发树里命令行在 artifacts\\cli\\KeyMouse.exe、编辑器在 src\\KeyMouse.FlowEditor\\bin\\…；",
            "安装版里编辑器在 <安装目录>\\、命令行在 <安装目录>\\cli\\。doctor 会把这一次判定用的每一个候选位置都列出来。",
        },
        [
            new("doctor", "打印环境判定、版本、权限、注册表，以及命令行/编辑器各自是在哪儿找到的"),
        ]),

        new("退出码", new[]
        {
            "0 = 成功        1 = 运行时失败（例如 SendInput 被 UIPI 拦截）",
            "2 = 参数错误    3 = 没有匹配的窗口 / 多候选未 --pick / waitfor 超时 / 选区取消",
            "4 = 目标不可用（隐藏、最小化、被 DWM cloak、无响应）   5 = 焦点没验证通过",
            "6 = 没读到内容   7 = 被取消。3/4/5/6 都属于\"没有发出任何输入\"，可以直接重试。",
        }, []),
    ];

    internal static string Render()
    {
        int width = ContentWidth();
        var sb = new System.Text.StringBuilder();
        string title = "KeyMouse " + Commands.ProductVersion;
        sb.AppendLine(title + " " + new string('─', Math.Max(4, width - ConsoleText.DisplayWidth(title) - 1)));
        sb.AppendLine("在 Windows 上模拟真实的鼠标与键盘事件（执行一次即退出，无常驻进程）");
        sb.AppendLine();
        sb.AppendLine("用法");
        sb.AppendLine("  KeyMouse <命令组> <子命令> [参数] [选项]");

        foreach (Section section in Sections)
        {
            sb.AppendLine();
            string header = section.Title;
            sb.AppendLine(header + " " + new string('─', Math.Max(2, width - ConsoleText.DisplayWidth(header) - 1)));

            foreach (Entry entry in section.Entries) AppendEntry(sb, entry, width);
            foreach (string note in section.Note ?? []) AppendBlock(sb, note, width);
        }

        return sb.ToString();
    }

    /// <summary>命令列 + 说明列。命令太长就独占一行，说明另起一行并保持同一左缘。</summary>
    private static void AppendEntry(System.Text.StringBuilder sb, Entry entry, int width)
    {
        int commandWidth = ConsoleText.DisplayWidth(entry.Command);
        if (entry.Description.Length == 0)
        {
            sb.AppendLine("  " + entry.Command);
            return;
        }

        if (commandWidth >= Gutter)
        {
            sb.AppendLine("  " + entry.Command);
            AppendWrapped(sb, entry.Description, Gutter + 2, width);
            return;
        }

        int room = width - Gutter - 2;
        string[] lines = Wrap(entry.Description, room);
        sb.AppendLine("  " + ConsoleText.Pad(entry.Command, Gutter) + lines[0]);
        for (int i = 1; i < lines.Length; i++) sb.AppendLine(new string(' ', Gutter + 2) + lines[i]);
    }

    private static void AppendBlock(System.Text.StringBuilder sb, string text, int width) =>
        AppendWrapped(sb, text, 2, width);

    private static void AppendWrapped(System.Text.StringBuilder sb, string text, int indent, int width)
    {
        foreach (string line in Wrap(text, width - indent)) sb.AppendLine(new string(' ', indent) + line);
    }

    /// <summary>
    /// 按**单元格**折行：中文没有空格可断，所以先在标点处找断点，找不到就硬断。
    /// 硬断总比溢出好——溢出会把右边那列挤成一条参差的边。
    /// </summary>
    private static string[] Wrap(string text, int width)
    {
        if (width < 8) width = 8;
        var lines = new System.Collections.Generic.List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string rest = paragraph;
            while (ConsoleText.DisplayWidth(rest) > width)
            {
                int cut = 0, cells = 0, lastBreak = -1;
                foreach (char c in rest)
                {
                    int step = ConsoleText.DisplayWidth(c.ToString());
                    if (cells + step > width) break;
                    cells += step;
                    cut++;
                    if (c is '，' or '、' or '；' or '。' or '：' or ' ' or '/') lastBreak = cut;
                }
                int take = lastBreak > cut - 14 ? lastBreak : cut;
                if (take <= 0) take = 1;
                lines.Add(rest[..take].TrimEnd());
                rest = rest[take..].TrimStart();
            }
            lines.Add(rest);
        }
        return [.. lines];
    }

    /// <summary>终端当前宽度；拿不到时按 88 算。</summary>
    private static int ContentWidth()
    {
        int width;
        try
        {
            width = Console.WindowWidth;
        }
        catch
        {
            width = 0;
        }
        if (width <= 0) width = 88;
        return Math.Clamp(width - 2, MinWidth, MaxWidth);
    }
}
