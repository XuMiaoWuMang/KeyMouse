using System.Globalization;

namespace KeyMouse;

/// <summary>Maps readable key names to virtual-key codes.</summary>
internal static class KeyMap
{
    private static readonly Dictionary<string, (ushort Vk, bool Ext)> Map = Build();

    public static (ushort Vk, bool Ext) Resolve(string name)
    {
        string n = name.Trim();
        if (Map.TryGetValue(n, out var hit)) return hit;

        // escape hatch: vk:0x5B / vk:5B
        if (n.StartsWith("vk:", StringComparison.OrdinalIgnoreCase) &&
            ushort.TryParse(n.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort raw))
            return (raw, false);

        throw new ArgumentException($"unknown key '{name}' (see 'KeyMouse help' for the key list)");
    }

    public static IEnumerable<string> Names => Map.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

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
