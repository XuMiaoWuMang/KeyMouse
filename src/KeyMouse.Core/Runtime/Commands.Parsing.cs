using System.Globalization;

namespace KeyMouse.Runtime;

/// <summary>
/// 命令行解析与公共助手：全局选项、键值解析、数值校验、失败出口。
/// </summary>
internal static partial class Commands
{

    internal sealed class GlobalOptions
    {
        public string? Title;
        public string? TitleExact;
        public string? ClassName;
        public string? ProcessName;
        public uint? Pid;
        public IntPtr? Hwnd;
        public int? Pick;
        /// <summary>
    /// never（默认）：**不碰焦点**——给了窗口只用来换算坐标与校验资格，绝不 SetForegroundWindow。
    /// 工具相信调用方知道自己在做什么；"我点哪儿就发哪儿"比"我先帮你切窗口"更少意外。
    /// gentle / none 是显式选择的严格模式。
    /// </summary>
    public string FocusPolicy = "never";
        public int FocusAttempts = WindowFocus.DefaultAttempts;
        public bool AllowRestore;
        public bool StrictPoint;
        public int? Wx;
        public int? Wy;

        /// <summary>Client-area end point: only `mouse drag` uses it.</summary>
        public int? Wx2;
        public int? Wy2;

        public bool HasSelector => Title is not null || TitleExact is not null || ClassName is not null ||
                                   ProcessName is not null || Pid is not null || Hwnd is not null;

        public WindowSelector ToSelector() => new()
        {
            Title = Title,
            TitleExact = TitleExact,
            ClassName = ClassName,
            ProcessName = ProcessName,
            ProcessId = Pid,
            Handle = Hwnd
        };
    }

    /// <summary>Pulls window/focus options out of argv; everything else stays for the command parser.</summary>
    internal static string[] ExtractGlobalOptions(string[] args, out GlobalOptions g)
    {
        g = new GlobalOptions();
        var rest = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--")
            {
                for (int j = i + 1; j < args.Length; j++) rest.Add(args[j]);
                break;
            }
            if (arg.Length < 2 || arg[0] != '-')
            {
                rest.Add(arg);
                continue;
            }

            string name = arg.TrimStart('-');
            string? inline = null;
            int eq = name.IndexOf('=');
            if (eq >= 0)
            {
                inline = name[(eq + 1)..];
                name = name[..eq];
            }

            string Take(string what)
            {
                if (inline is not null) return inline;
                if (i + 1 >= args.Length) throw new ArgumentException($"--{name} 需要一个值（{what}）");
                return args[++i];
            }

            switch (name.ToLowerInvariant())
            {
                case "title": g.Title = Take("标题子串"); break;
                case "title-exact": g.TitleExact = Take("完整标题"); break;
                case "class": g.ClassName = Take("窗口类名"); break;
                case "process": g.ProcessName = Take("进程名，如 notepad"); break;
                case "pid": g.Pid = (uint)IntArg(Take("进程 ID"), "--pid"); break;
                case "hwnd": g.Hwnd = WindowLocator.ParseHandle(Take("窗口句柄")); break;
                case "pick": g.Pick = IntArg(Take("候选序号"), "--pick"); break;
                case "focus-policy": g.FocusPolicy = Take("never|gentle|none"); break;
                case "focus-attempts": g.FocusAttempts = Math.Max(1, IntArg(Take("尝试次数"), "--focus-attempts")); break;
                case "allow-restore": g.AllowRestore = true; break;
                case "strict-point": g.StrictPoint = true; break;
                case "wx": g.Wx = IntArg(Take("客户区 x"), "-wx"); break;
                case "wx2": g.Wx2 = IntArg(Take("拖拽终点的客户区 x"), "--wx2"); break;
                case "wy2": g.Wy2 = IntArg(Take("拖拽终点的客户区 y"), "--wy2"); break;
                case "wy": g.Wy = IntArg(Take("客户区 y"), "-wy"); break;
                default: rest.Add(arg); break;
            }
        }

        return rest.ToArray();
    }

    private static bool IsModifier(ushort vk) => vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C;

    /// <summary>Splits argv into positional values and -flag/--flag[=value] options.</summary>
    internal static (List<string> Positional, Dictionary<string, string> Options) Parse(string[] args, params string[] valueFlags)
    {
        var positional = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            bool looksLikeOption = a.Length > 1 && a[0] == '-' &&
                                   !int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            if (!looksLikeOption)
            {
                positional.Add(a);
                continue;
            }

            string name = a.TrimStart('-');
            string? value = null;
            int eq = name.IndexOf('=');
            if (eq >= 0)
            {
                value = name[(eq + 1)..];
                name = name[..eq];
            }
            if (value is null && valueFlags.Contains(name, StringComparer.OrdinalIgnoreCase) && i + 1 < args.Length)
                value = args[++i];
            options[name] = value ?? "true";
        }

        return (positional, options);
    }

    internal static int IntArg(string raw, string what) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : throw new ArgumentException($"'{raw}' 不是合法的整数（{what}）");

    internal static int IntOr(Dictionary<string, string> options, string name, int fallback) =>
        options.TryGetValue(name, out string? raw) &&
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : fallback;

    internal static int Fail(int code, string message)
    {
        Console.Error.WriteLine("错误：" + message);
        return code;
    }

    private static void PrintUsage() => Usage.Print();
}
