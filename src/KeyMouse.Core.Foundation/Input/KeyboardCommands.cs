using static KeyMouse.Runtime.CommandLine;

namespace KeyMouse.Input;

/// <summary>
/// 键盘：press / down / up / combo / type。
/// </summary>
internal static class KeyboardCommands
{

    internal static int Run(string[] args, GlobalOptions g, bool allowTextStdin)
    {
        if (args.Length == 0)
            return Fail(2, "key：需要一个子命令（press | down | up | combo | type）");

        string cmd = args[0].ToLowerInvariant();
        var (pos, opt) = Parse(args[1..], "n", "i", "interval", "hold");


        switch (cmd)
        {
            case "press":
            case "tap":
                {
                    if (pos.Count != 1) return Fail(2, "用法：KeyMouse key press <按键> [-n 次数] [-i 毫秒]");
                    CommandTarget.PrepareTarget(g);
                    var (vk, ext) = KeyMap.Resolve(pos[0]);
                    int count = IntOr(opt, "n", 1);
                    int interval = IntOr(opt, "i", 60);
                    for (int i = 0; i < count; i++)
                    {
                        NativeInput.Tap(vk, ext);
                        if (i < count - 1) Thread.Sleep(Math.Max(1, interval));
                    }
                    CommandOutput.Out.WriteLine($"已敲击 {pos[0]}{(count > 1 ? $" ×{count}" : "")}");
                    return 0;
                }

            case "down":
                {
                    if (pos.Count != 1) return Fail(2, "用法：KeyMouse key down <按键>");
                    return KeyState(g, pos[0], false);
                }
            case "up":
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key up <按键>");
                return KeyState(g, pos[0], true);
            case "combo":
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key combo <键1+键2+...>");
                return Combo(g, pos[0], IntOr(opt, "hold", 30));

            case "type":
                {
                    string text;
                    if (opt.ContainsKey("stdin"))
                    {
                        if (!allowTextStdin || pos.Count != 0 || !Console.IsInputRedirected)
                            return Fail(2, "key type --stdin 仅限独立 CLI，从重定向的标准输入读 UTF-8 文本，不接受位置参数");
                        // The stream is literal text. Preserve even a leading U+FEFF.
                        using var reader = new StreamReader(Console.OpenStandardInput(),
                            new System.Text.UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
                        text = reader.ReadToEnd();
                    }
                    else
                    {
                        if (pos.Count == 0) return Fail(2, "用法：KeyMouse key type <文本> [--interval 毫秒]");
                        text = string.Join(' ', pos);
                    }
                    return Type(g, text, IntOr(opt, "interval", IntOr(opt, "i", 15)));
                }

            default:
                return Fail(2, $"key：未知子命令 '{cmd}'");
        }
    }

    internal static int Type(GlobalOptions g, string text, int interval)
    {
        if (text.Length == 0) return Fail(2, "要输入的文字是空的：检查流程里这一步的 text");
        CommandTarget.PrepareTarget(g);
        NativeInput.TypeText(text, interval);
        CommandOutput.Out.WriteLine($"已输入 {text.Length} 个字符");
        return 0;
    }

    internal static int KeyState(GlobalOptions g, string name, bool up)
    {
        CommandTarget.PrepareTarget(g);
        var (vk, ext) = KeyMap.Resolve(name);
        NativeInput.Key(vk, ext, up);
        CommandOutput.Out.WriteLine($"已{(up ? "松开" : "按下")} {name}");
        return 0;
    }

    internal static int Combo(GlobalOptions g, string chord, int hold)
    {
        CommandTarget.PrepareTarget(g);
        var keys = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(KeyMap.Resolve).OrderBy(k => k.Vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C ? 0 : 1).ToList();
        if (keys.Count == 0) return Fail(2, "组合键不能为空");
        NativeInput.Chord(keys, hold);
        CommandOutput.Out.WriteLine("已发送组合键 " + string.Join("+", keys.Select(k => $"0x{k.Vk:X2}")));
        return 0;
    }
}
