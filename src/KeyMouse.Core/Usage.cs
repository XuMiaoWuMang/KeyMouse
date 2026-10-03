namespace KeyMouse;

/// <summary>The built-in help text, kept out of Commands.cs so the command surface and the
/// dispatcher do not have to share a file. $$""" because the text contains ${name}.</summary>
internal static class Usage
{
    public static void Print() => Console.WriteLine(Text);

    private const string Text = $$"""
        KeyMouse {{Commands.Version}} —— 在 Windows 上模拟真实的鼠标与键盘事件（执行一次即退出，无常驻进程）

        用法
          KeyMouse <命令组> <子命令> [参数] [选项]

        鼠标
          mouse pos                                   打印当前光标坐标
          mouse move <x> <y>                          移动到绝对屏幕像素
          mouse move -wx <cx> -wy <cy> <选择器>        移动到某窗口客户区的点
          mouse moveby <dx> <dy>                      相对移动
          mouse click [键] [-x X -y Y] [-n 次数] [-i 毫秒]
          mouse dblclick [键] [-x X -y Y]             双击（等价于 click -n 2）
          mouse down [键] / mouse up [键]             按住 / 松开
          mouse wheel <增量> [-x X -y Y]              120 = 一格，向上为正（向下滚 = -120）
          mouse hwheel <增量>                         横向滚轮
          mouse drag <x1> <y1> <x2> <y2> [--button 键] [--steps 步数] [--duration 毫秒]
          mouse drag -wx <cx1> -wy <cy1> --wx2 <cx2> --wy2 <cy2> <选择器>
                                                      在两个客户区坐标点之间拖拽

          键：left（默认）| right | middle | x1 | x2

        键盘
          key press <按键> [-n 次数] [-i 毫秒]         敲击，可连按
          key down <按键> / key up <按键>              按下 / 抬起
          key combo <键1+键2+...> [--hold 毫秒]        组合键，如 ctrl+shift+s、win+r、alt+f4
          key type <文本> [--interval 毫秒]            输入文本（Unicode 注入，不依赖输入法/键盘布局）
                                                      默认每字 15ms；--interval 0 最快但可能丢字

        窗口
          window list [--filter <标题子串>] [--process <进程名>] [--all]
                                                      列出顶层窗口及其状态
          window inspect <选择器>                      说明某个窗口为什么可用 / 不可用
          window focus <选择器>                        只做聚焦与验证，不发送任何输入

        识图（读，不是发）
          probe <选择器> [--region x,y,w,h] [选项]     把窗口的一块区域读成文字。
                                                      它只报告看到什么，不做任何判断。
          probe --pick-region [选项]                   先用鼠标框一块区域（单击则选整个客户区），
                                                      然后读它；不要再给选择器 / --region
          --region x,y,w,h         客户区相对坐标（--space window 则相对整个窗口）；
                                   省略即整个客户区。越界一律拒绝，不猜也不截断
          --space client|window    区域坐标系，默认 client（标题栏在 client 之外）
          --pick-region            打开全屏选区浮层：拖动框选、单击选整个客户区、ESC 取消
          --reads N                重复读取次数，默认 2；N 次必须逐字一致才算看清
          --lang <语言>            默认 eng+chi_sim
          --engine <命令>          OCR 引擎，默认 tesseract（会自动找常见安装位置）
          --tessdata-dir <目录>    模型目录；默认 %LOCALAPPDATA%\KeyMouse\tessdata
          --min-conf N             置信度地板，默认 30；低于它判为没看清
          --scale N / --pad N      放大倍数（默认 1）与白边像素（默认 0）：默认把抓到的
                                   原始像素直接交给引擎，不做任何变换
          --normalize              先把对比度拉到黑白（暗底浅字的窗口读不出来时用）
          --resample nearest|bilinear
                                   放大时的取样方式（默认 bilinear；nearest 把每个源
                                   像素复制成 N×N 方块，不发明像素）
          --capture screen|print   取像方式：默认 screen；print 用 PrintWindow 不画光标，
                                   但它不能用于 --space window（标题栏），且本机几何对不齐
                                   （见 docs/design.md）
          --json                   输出 JSON（引擎身份、几何、逐词置信度）
          --keep-image <路径>      原样保存抓到的像素（不放大、不加白边、不做对比度
                                   归一化）——截图就是屏幕上那一块
          --keep-prepared <路径>   保存送进引擎的那张图（放大+白边+归一化之后）

        选区（给人挑坐标，不读文字）
          region pick [--rect x,y,w,h] [--json]        框一块区域，报告它落在哪个窗口的哪个
                                                      坐标系里，并给出一条可直接粘贴的 probe 命令。
                                                      只报坐标；--rect 跳过浮层（脚本与测试用）

        录制（把一次操作变成可回放的 JSON 流程）
          record [--out 流程.json] [--duration 毫秒] [--no-shots]
                 [--min-gap 毫秒] [--move-threshold 像素]
                                    开始录制：全局监听键鼠（只观察、不拦截，你照常操作），
                                    每个动作连同它发生的窗口与客户区相对坐标写成 JSON。
                                    Ctrl+Alt+Q 停止（连按两次 ESC 也停）；--duration 到点自动停。
                                    鼠标轨迹按距离抽稀后一起录；点击/输入等步骤会在
                                    <流程>.shots/ 里各存一张 320x200 的截图。
                                    回放就是：KeyMouse run 流程.json

        常驻服务（给图形界面用）
          serve                                       启动本地 Runner：一个进程里常驻着同一套
                                                      能力层，客户端通过命名管道连它（默认
                                                      \\.\pipe\keymouse-runner-<用户名>）拿流式事件：
                                                      run / record / validate / pick-region / ocr /
                                                      cancel / pause / resume / status / list /
                                                      shutdown，一行一个 JSON 对象。
                                                      编辑器连的就是它；CLI 的其余命令照旧是一次性
                                                      执行，两者跑的是同一份派发。
        流程（录制出来的 JSON，以及图形编辑器）
          flow edit <流程.json>                       用 WinUI 图形编辑器打开一个流程：
                                                      左边是步骤列表（可拖拽排序），右边改参数，
                                                      「从屏幕取区域」直接拉选区浮层，
                                                      「试运行/播放」调用的还是 run 本身。
                                                      编辑器在 editor\KeyMouse.FlowEditor，
                                                      需要单独构建：dotnet build editor\KeyMouse.FlowEditor -c Release

        脚本
          run <文件|-> [选项]                          按行顺序执行文件（或 stdin）里的命令；
                                                      一行一条命令，# 开头是注释，另有三个伪命令：
                                                      sleep、waitfor、waitgone。
                                                      选择器与焦点闸门逐行生效；脚本必须是 UTF-8。
                                                      **给 .json 文件就是回放录制出来的流程**，
                                                      选项含义相同（--dry-run / --report / --retry）。

          window focus <选择器>            脚本里只要写一次：它同时成为"当前目标"，
                                           后面的 mouse / key 行不带选择器时自动继承
                                           （继承的行仍然各自过一遍焦点与闸门验证）

          repeat <次数> [as <名字>]        循环开始；循环变量默认叫 i，从 0 开始计
            ...                           例如：
          end                               repeat 3 as row
                                              key type "第 ${row} 行"
                                              key press enter
                                            end
                                           嵌套时内层必须 as 取名；结构错误在第一条
                                           命令执行之前就会带着行号报错

          sleep <毫秒>                     等待
          waitfor <选择器> [--timeout 毫秒] [--interval 毫秒]
                                           等到有可用窗口匹配；超时则退出码 3（默认 5000 / 200 毫秒）
          waitgone <选择器> [--timeout 毫秒] [--interval 毫秒]
                                           等到没有可用窗口匹配（--timeout 0 = 立刻断言）

          --delay 毫秒            每条命令之间等待多久（默认 0）
          --keep-going           失败也继续（默认首错即停）
          --dry-run              照常做资格检查，但不发送任何输入、也不抢焦点
          --echo                 连子命令自己的输出一起打印
          --retry 次数            失败后最多再试几次——只对"确定没发出输入"的失败重试
                                 （退出码 3/4/5）；做了一半的动作绝不重复
          --retry-delay 毫秒      重试前等待（默认 300）
          --set 名=值             定义变量，脚本里用 ${name} 引用（可重复给；
                                 含空格的值仍然是一个参数）
          --report 文件.json      写出机器可读的执行报告

        窗口选择器（多个条件之间是 AND；鼠标 / 键盘 / 窗口命令都接受）
          --title <子串>            标题子串，大小写不敏感
          --title-exact <文本>      标题完全匹配
          --class <类名>            窗口类名
          --process <进程名>        例如 notepad、QQ
          --pid <进程号>            进程 ID
          --hwnd <0x1234|1234>      窗口句柄
          --pick <n>                多个可用候选时选第 n 个（1-based，Z 序从上到下）

        窗口策略
          --focus-policy gentle|none  gentle（默认）：SetForegroundWindow 后回读真实的前台窗口，
                                      最多 3 次；仍不成功则以退出码 5 结束。
                                      none：要求它已经是前台窗口。
          --focus-attempts <n>       温和模式的尝试次数（默认 3）
          --allow-restore            允许 KeyMouse 还原最小化窗口（默认关；流程文件里可写 "allowRestore": true，两者任一即可）
          -wx <cx> -wy <cy>          客户区坐标（必须成对出现，且必须带选择器）；
                                     在 mouse drag 里它是起点，终点是
                                     --wx2 <cx2> --wy2 <cy2>（四个必须一起给）
          --strict-point             额外要求该屏幕点下方的窗口就是目标本身

        按键名
          a-z  0-9  f1-f24  esc enter tab space backspace delete insert home end
          pageup pagedown up down left right ctrl shift alt win rwin apps capslock
          num0-num9 numadd numsub nummul numdiv numdecimal numenter numlock
          printscreen pause  semicolon equals comma minus period slash grave
          lbracket rbracket backslash quote  vk:0x5B（裸虚拟键逃生口，0x 可省略）

        例子
          KeyMouse mouse move 100 200
          KeyMouse mouse click right -x 640 -y 480
          KeyMouse mouse click left -wx 120 -wy 340 --title "记事本"
          KeyMouse key type "hello 世界" --process notepad
          KeyMouse window list --process QQ
          KeyMouse window inspect --title "记事本"

        退出码
          0 = 成功              1 = 运行时失败（例如 SendInput 被 UIPI 拦截）
          2 = 参数错误          3 = 没有匹配的窗口 / 多候选未 --pick / waitfor 超时 / 选区取消
          4 = 目标不可用（隐藏 / 最小化 / 被 DWM 遮盖 / 无响应 / 被禁用）
          5 = 焦点验证失败——未发送任何输入
          6 = 没看清（probe：N 次读取不一致 / 置信度低于地板 / 没有可用引擎）——同样未发送任何输入
          （脚本返回第一条失败命令的退出码）

        说明
          * 事件会落在当前前台窗口 / 光标下方的窗口。带选择器时 KeyMouse 会先聚焦目标，
            验证不过就一个字节都不发——fail closed，绝不猜。
          * 隐藏窗口一律拒绝：强行显示隐藏窗口（托盘应用、Electron）可能只得到一帧静止画面。
          * 目标以管理员权限运行时 SendInput 会被 UIPI 拦截，请也用管理员权限运行 KeyMouse。
        """;
}
