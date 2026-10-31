namespace KeyMouse.Runtime;

/// <summary>
/// 键盘：press / down / up / combo / type。
/// </summary>
internal static partial class Commands
{

    private static int Keyboard(string[] args, GlobalOptions g)
    {
        if (args.Length == 0)
            return Fail(2, "key：需要一个子命令（press | down | up | combo | type）");

        string cmd = args[0].ToLowerInvariant();
        var (pos, opt) = Parse(args[1..], "n", "i", "interval", "hold");

        WindowInfo? cached = null;
        void EnsureTarget() { if (g.HasSelector) cached ??= PrepareTarget(g); }

        switch (cmd)
        {
            case "press":
            case "tap":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key press <按键> [-n 次数] [-i 毫秒]");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                int count = IntOr(opt, "n", 1);
                int interval = IntOr(opt, "i", 60);
                for (int i = 0; i < count; i++)
                {
                    NativeInput.Tap(vk, ext);
                    if (i < count - 1) Thread.Sleep(Math.Max(1, interval));
                }
                Console.WriteLine($"已敲击 {pos[0]}{(count > 1 ? $" ×{count}" : "")}");
                return 0;
            }

            case "down":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key down <按键>");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                NativeInput.Key(vk, ext, false);
                Console.WriteLine($"已按下 {pos[0]}");
                return 0;
            }

            case "up":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key up <按键>");
                var (vk, ext) = KeyMap.Resolve(pos[0]);
                NativeInput.Key(vk, ext, true);
                Console.WriteLine($"已松开 {pos[0]}");
                return 0;
            }

            case "combo":
            {
                EnsureTarget();
                if (pos.Count != 1) return Fail(2, "用法：KeyMouse key combo <键1+键2+...>   例如 ctrl+shift+s");
                var keys = new List<(ushort Vk, bool Ext)>();
                foreach (string part in pos[0].Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    keys.Add(KeyMap.Resolve(part));

                keys = keys.OrderBy(k => IsModifier(k.Vk) ? 0 : 1).ToList();
                NativeInput.Chord(keys, IntOr(opt, "hold", 30));
                Console.WriteLine("已发送组合键 " + string.Join("+", keys.Select(k => $"0x{k.Vk:X2}")));
                return 0;
            }

            case "type":
            {
                EnsureTarget();
                if (pos.Count == 0) return Fail(2, "用法：KeyMouse key type <文本> [--interval 毫秒]");
                string text = string.Join(' ', pos);
                // 空串不是"什么都不做"，是流程里漏了 text——让它在这里就红，
                // 别等到跑完才让人对着"已输入 0 个字符"发愣（试运行走的也是这条）。
                if (text.Length == 0) return Fail(2, "要输入的文字是空的：检查流程里这一步的 text");
                NativeInput.TypeText(text, IntOr(opt, "interval", IntOr(opt, "i", 15)));
                Console.WriteLine($"已输入 {text.Length} 个字符");
                return 0;
            }

            default:
                return Fail(2, $"key：未知子命令 '{cmd}'");
        }
    }

}
