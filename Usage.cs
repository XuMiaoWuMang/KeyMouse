namespace KeyMouse;

/// <summary>The built-in help text, kept out of Program.cs so the command surface and the
/// dispatcher do not have to share a file. $$""" because the text contains ${name}.</summary>
internal static class Usage
{
    public static void Print() => Console.WriteLine(Text);

    private const string Text = $$"""
        KeyMouse {{Program.Version}} —— 在 Windows 上模拟真实的鼠标与键盘事件（执行一次即退出，无常驻进程）

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
          --region x,y,w,h         客户区相对坐标（--space window 则相对整个窗口）；
                                   省略即整个客户区。越界一律拒绝，不猜也不截断
          --space client|window    区域坐标系，默认 client（标题栏在 client 之外）
          --reads N                重复读取次数，默认 2；N 次必须逐字一致才算看清
          --lang <语言>            默认 eng+chi_sim
          --engine <命令>          OCR 引擎，默认 tesseract（会自动找常见安装位置）
          --tessdata-dir <目录>    模型目录；默认 %LOCALAPPDATA%\KeyMouse\tessdata
          --min-conf N             置信度地板，默认 30；低于它判为没看清
          --scale N / --pad N      放大倍数（默认 3）与白边像素（默认 16）
          --capture screen|print   取像方式：默认 screen；print 用 PrintWindow 不画光标，
                                   但在本机几何对不齐（见 docs/design.md）
          --json                   输出 JSON（引擎身份、几何、逐词置信度）
          --keep-image <路径>      把送进引擎的那张图（BMP）留下来，便于自查

        脚本
          run <文件|-> [选项]                          按行顺序执行文件（或 stdin）里的命令；
                                                      一行一条命令，# 开头是注释，另有三个伪命令：
                                                      sleep、waitfor、waitgone。
                                                      选择器与焦点闸门逐行生效；脚本必须是 UTF-8。

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
          --allow-restore            允许 KeyMouse 还原最小化窗口（默认关）
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
          2 = 参数错误          3 = 没有匹配的窗口 / 多候选未 --pick / waitfor 超时
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
