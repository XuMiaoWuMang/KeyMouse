using System.Globalization;

namespace KeyMouse.Input;

internal sealed record BackgroundRequest(string Action, int Pid, nint Root, nint? Control, string? Id, string? Text)
{
    internal const string Help = """
        KeyMouse background controls --pid N --hwnd H
        KeyMouse background insert-text --pid N --hwnd H (--control H | --id AutomationId) --text TEXT
        KeyMouse background set-value   --pid N --hwnd H (--control H | --id AutomationId) --text TEXT
        KeyMouse background invoke      --pid N --hwnd H (--control H | --id AutomationId)
        仅同用户、同权限的标准 Edit / Button。KeyMouse 不主动聚焦、还原窗口或回退到 SendInput。
        """;

    internal static BackgroundRequest Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("controls" or "insert-text" or "set-value" or "invoke"))
            throw new ArgumentException(Help);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
        {
            string key = args[i];
            if (key is not ("--pid" or "--hwnd" or "--control" or "--id" or "--text") || i + 1 == args.Length ||
                !options.TryAdd(key, args[i + 1])) throw new ArgumentException("未知、重复或缺少值的选项：" + key);
        }
        if (!int.TryParse(options.GetValueOrDefault("--pid"), NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid < 1)
            throw new ArgumentException("必须提供目标窗口的 --pid 正整数。");
        static nint Handle(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("缺少窗口句柄。");
            nint h = WindowLocator.ParseHandle(input);
            if (h <= 0 || h.ToInt64() > uint.MaxValue) throw new ArgumentException("需要非零的 Windows 窗口句柄。");
            return h;
        }
        var root = Handle(options.GetValueOrDefault("--hwnd"));
        nint? control = options.TryGetValue("--control", out var raw) ? Handle(raw) : null;
        var id = options.GetValueOrDefault("--id");
        var text = options.GetValueOrDefault("--text");
        if (id is not null && string.IsNullOrWhiteSpace(id)) throw new ArgumentException("--id 不能为空。");
        if ((control.HasValue ? 1 : 0) + (id != null ? 1 : 0) != (args[0] == "controls" ? 0 : 1))
            throw new ArgumentException("动作必须且只能指定一个 --control 或 --id；列举不接受控件选择器。");
        if ((args[0] is "insert-text" or "set-value") != (text != null) || text is { Length: > 4096 } ||
            (args[0] == "insert-text" && text == "") || text?.Contains('\0') == true)
            throw new ArgumentException("仅文本动作接受 --text，最多 4096 个 UTF-16 单元；仅 set-value 允许空文本。");
        return new(args[0], pid, root, control, id, text);
    }
}
