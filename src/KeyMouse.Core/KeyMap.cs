using System.Globalization;

namespace KeyMouse;

// 一个映射字典，包含 按键->虚拟键码（VK）的正向映射，也包含虚拟键码（VK）->按键名称的反向映射。
// 用于解析按键名称和虚拟键码之间的关系。
internal static class KeyMap
{
    // 构建一个只读的正向映射字典，用于根据按键名称快速查找对应的虚拟键码（VK）和是否为扩展键（Ext）。
    private static readonly Dictionary<string, (ushort Vk, bool Ext)> Map = Build();
    // 构建一个只读的反向映射字典，用于根据虚拟键码快速查找对应的按键名称。
    private static readonly Dictionary<ushort, string> Reverse = BuildReverse();

    // 查询指定虚拟键码（VK）的对应的按键名称，不存在则返回null
    public static string? NameOf(int vk) =>
        Reverse.TryGetValue((ushort)vk, out string? name) ? name : null;

    // 根据已有的正向映射 Map（名称 → 虚拟键码），构建一个反向映射 reverse（虚拟键码 → 名称），用于通过 VK 码快速查出对应的按键名称。
    private static Dictionary<ushort, string> BuildReverse()
    {
        var reverse = new Dictionary<ushort, string>();
        foreach (var (name, entry) in Map)
        {
            // 当多个名称对应同一个 VK 时，保留最先遍历到的那个名称
            if (!reverse.ContainsKey(entry.Vk)) reverse[entry.Vk] = name;
        }
        return reverse;
    }

    // 根据传入的按键名称字符串，解析出对应的虚拟键码（VK）和是否为扩展键（Ext）。
    public static (ushort Vk, bool Ext) Resolve(string name)
    {
        string n = name.Trim();
        if (Map.TryGetValue(n, out var hit)) return hit;

        // 如果名称不在预定义的映射表中，还支持通过 vk: 前缀直接指定十六进制虚拟键码。
        if (n.StartsWith("vk:", StringComparison.OrdinalIgnoreCase))
        {
            ReadOnlySpan<char> digits = n.AsSpan(3);
            if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) digits = digits[2..];
            if (ushort.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort raw))
                return (raw, false);
        }

        throw new ArgumentException($"未知按键 '{name}'（按键名列表见 KeyMouse help）");
    }

    // 提供一个公开的、只读的、按忽略大小写排序后的 Map 键名集合，方便外部遍历、显示或处理所有名称。
    // 返回值是IEnumerable，只有当外部真正需要遍历时才会进行排序和枚举，避免不必要的性能开销。
    public static IEnumerable<string> Names => Map.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

    // 初始化字典 
    private static Dictionary<string, (ushort, bool)> Build()
    {
        var m = new Dictionary<string, (ushort, bool)>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, ushort vk, bool ext = false) => m[name] = (vk, ext);

        for (char c = 'a'; c <= 'z'; c++) Add(c.ToString(), (ushort)(0x41 + (c - 'a')));
        for (char c = '0'; c <= '9'; c++) Add(c.ToString(), (ushort)(0x30 + (c - '0')));
        for (int i = 1; i <= 24; i++) Add($"f{i}", (ushort)(0x70 + i - 1));

        Add("backspace", 0x08); Add("bksp", 0x08);
        Add("tab", 0x09);
        Add("enter", 0x0D); Add("return", 0x0D);
        Add("shift", 0x10);
        Add("ctrl", 0x11); Add("control", 0x11);
        Add("alt", 0x12);
        Add("pause", 0x13);
        Add("capslock", 0x14); Add("caps", 0x14);
        Add("esc", 0x1B); Add("escape", 0x1B);
        Add("space", 0x20); Add("spacebar", 0x20);
        Add("pageup", 0x21, true); Add("pgup", 0x21, true);
        Add("pagedown", 0x22, true); Add("pgdn", 0x22, true);
        Add("end", 0x23, true);
        Add("home", 0x24, true);
        Add("left", 0x25, true);
        Add("up", 0x26, true);
        Add("right", 0x27, true);
        Add("down", 0x28, true);
        Add("printscreen", 0x2C, true); Add("prtsc", 0x2C, true);
        Add("insert", 0x2D, true); Add("ins", 0x2D, true);
        Add("delete", 0x2E, true); Add("del", 0x2E, true);
        Add("win", 0x5B, true); Add("lwin", 0x5B, true); Add("rwin", 0x5C, true);
        Add("apps", 0x5D, true); Add("menu", 0x5D, true);
        Add("numlock", 0x90); Add("scrolllock", 0x91);

        for (int i = 0; i <= 9; i++) Add($"num{i}", (ushort)(0x60 + i));
        Add("nummul", 0x6A); Add("numadd", 0x6B); Add("numsub", 0x6D);
        Add("numdecimal", 0x6E); Add("numdiv", 0x6F, true); Add("numenter", 0x0D, true);

        Add("semicolon", 0xBA); Add("equals", 0xBB); Add("comma", 0xBC); Add("minus", 0xBD);
        Add("period", 0xBE); Add("slash", 0xBF); Add("grave", 0xC0); Add("backtick", 0xC0);
        Add("lbracket", 0xDB); Add("backslash", 0xDC); Add("rbracket", 0xDD); Add("quote", 0xDE);

        return m;
    }
}
