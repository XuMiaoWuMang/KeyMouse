using System.Globalization;

namespace KeyMouse.Flow;

/// <summary>
/// 一个流程变量的值。三种就够：数字、文本、布尔。
///
/// 为什么不是"全都是字符串"：条件要能写 <c>i &lt; 3</c>，而字符串比较会把 10 排到 9 前面；
/// 为什么也不是动态类型对象：流程文件、编辑器和报告都是人能读的文本，数组与对象留给
/// <c>foreach</c> 的列表，不进表达式。
/// </summary>
internal enum FlowValueKind
{
    Number,
    Text,
    Bool,
}

/// <summary>
/// 表达式的值。变量帧（<see cref="VariableFrame"/>）里存的也是它：文档变量和 <c>--set</c>
/// 进来的是文本，<c>for</c> 的初值、<c>set</c> 的算术结果是数字，<c>text()</c> 一类谓词给布尔。
///
/// 文本与数字之间只在**需要数字的地方**才转换（算术、比较），而且是宽松的：
/// <c>--set i=3</c> 得到的 "3" 写 <c>i &lt; 10</c> 一样成立。两个文本的**相等**按序号比较
/// （"01" 与 "1" 不相等，从屏幕读回来的文字不会被悄悄当成数）；只有**比大小**时，两个都像
/// 数字才按数字比，否则按序号。
/// </summary>
internal readonly struct FlowValue
{
    private readonly double _number;
    private readonly string? _text;
    private readonly bool _bool;

    private FlowValue(FlowValueKind kind, double number, string? text, bool boolean)
    {
        Kind = kind;
        _number = number;
        _text = text;
        _bool = boolean;
    }

    internal FlowValueKind Kind { get; }

    internal static FlowValue Number(double value) => new(FlowValueKind.Number, value, null, false);

    internal static FlowValue Text(string value) => new(FlowValueKind.Text, 0, value ?? "", false);

    internal static FlowValue Bool(bool value) => new(FlowValueKind.Bool, 0, null, value);

    /// <summary>The value as text, which is what a <c>{{name}}</c> placeholder and a log line want.</summary>
    internal string AsText() => Kind switch
    {
        FlowValueKind.Number => Format(_number),
        FlowValueKind.Text => _text ?? "",
        _ => _bool ? "true" : "false",
    };

    /// <summary>Numbers, and text that reads as a number. Everything else is a type error with the caller's location.</summary>
    internal double AsNumber(string where)
    {
        if (Kind == FlowValueKind.Number) return _number;
        if (Kind == FlowValueKind.Text && TryParseNumber(_text, out double value)) return value;
        throw new CommandFailure(2, $"{where}需要一个数字，得到的是 {Describe()}");
    }

    internal bool TryNumber(out double value)
    {
        value = 0;
        if (Kind == FlowValueKind.Number) { value = _number; return true; }
        return Kind == FlowValueKind.Text && TryParseNumber(_text, out value);
    }

    /// <summary>Conditions must be actually true/false: C's "any non-zero number" is a foot-gun in a GUI flow.</summary>
    internal bool AsBool(string where)
    {
        if (Kind == FlowValueKind.Bool) return _bool;
        throw new CommandFailure(2, $"{where}需要一个 true/false，得到的是 {Describe()}");
    }

    /// <summary>For diagnostics: what the value is, not how it was computed.</summary>
    internal string Describe() => Kind switch
    {
        FlowValueKind.Number => $"数字 {Format(_number)}",
        FlowValueKind.Text => $"文本「{_text}」",
        _ => _bool ? "true" : "false",
    };

    /// <summary>
    /// Invariant formatting, and integers without a trailing ".0": <c>set i = 3</c> must interpolate
    /// as "3", not "3.0", because the same value is also used as a placeholder in text fields.
    /// </summary>
    internal static string Format(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "Infinity" : "-Infinity";
        if (value == Math.Truncate(value) && Math.Abs(value) < 1e15)
        {
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }
        return value.ToString("0.############", CultureInfo.InvariantCulture);
    }

    /// <summary>Locale-free and whitespace tolerant: OCR output often arrives with spaces around it.</summary>
    internal static bool TryParseNumber(string? text, out double value)
    {
        value = 0;
        if (text is null) return false;
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value);
    }

    public override string ToString() => AsText();
}

/// <summary>
/// One frame of variables. A loop body or a subflow sees its own names plus everything its caller
/// had: frames are **chained, not copied**, so a `read-text` inside a subflow writes into that
/// subflow's frame and the caller's values stay the caller's - which is the whole reason a subflow
/// can be reused with different `vars` without surprising the file that called it.
/// </summary>
internal sealed class VariableFrame
{
    private readonly VariableFrame? _parent;

    internal VariableFrame(VariableFrame? parent) : this(parent, (IEnumerable<KeyValuePair<string, FlowValue>>?)null) { }

    internal VariableFrame(VariableFrame? parent, IEnumerable<KeyValuePair<string, FlowValue>>? values = null)
    {
        _parent = parent;
        Values = new Dictionary<string, FlowValue>(StringComparer.Ordinal);
        if (values is null) return;
        foreach (var (name, value) in values) Values[name] = value;
    }

    /// <summary>String values are what document variables and <c>--set</c> hand in; kept as an overload so
    /// callers that already hold text do not have to wrap every pair.</summary>
    internal VariableFrame(VariableFrame? parent, IEnumerable<KeyValuePair<string, string>> values)
    {
        _parent = parent;
        Values = new Dictionary<string, FlowValue>(StringComparer.Ordinal);
        foreach (var (name, value) in values) Values[name] = FlowValue.Text(value);
    }

    /// <summary>Names this frame itself defines (a lookup walks up to the parents).</summary>
    internal Dictionary<string, FlowValue> Values { get; }

    internal FlowValue? LookupValue(string name)
    {
        for (VariableFrame? frame = this; frame is not null; frame = frame._parent)
        {
            if (frame.Values.TryGetValue(name, out FlowValue value)) return value;
        }
        return null;
    }

    /// <summary>The value as text; what <see cref="FlowDocument.Expand"/> and every string field needs.</summary>
    internal string? Lookup(string name) => LookupValue(name)?.AsText();

    internal void Set(string name, FlowValue value) => Values[name] = value;

    internal void Set(string name, string value) => Values[name] = FlowValue.Text(value);

    internal void SetText(string name, string value) => Values[name] = FlowValue.Text(value);

    /// <summary>
    /// C-like assignment: an existing name is updated **where it lives**, a new one is defined here.
    /// That is what makes a counter declared outside a loop keep counting inside it, while a name
    /// first introduced inside a loop still goes away with that loop's frame.
    /// </summary>
    internal bool Assign(string name, FlowValue value)
    {
        for (VariableFrame? frame = this; frame is not null; frame = frame._parent)
        {
            if (!frame.Values.ContainsKey(name)) continue;
            frame.Values[name] = value;
            return true;
        }
        Values[name] = value;
        return false;
    }

    /// <summary>The names visible here, nearest frame first (diagnostics only).</summary>
    internal IEnumerable<string> Visible()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (VariableFrame? frame = this; frame is not null; frame = frame._parent)
        {
            foreach (string name in frame.Values.Keys)
            {
                if (seen.Add(name)) yield return name;
            }
        }
    }

    /// <summary>Convenience for the places that still hold document variables as strings.</summary>
    internal static IEnumerable<KeyValuePair<string, FlowValue>> FromTexts(IEnumerable<KeyValuePair<string, string>> texts)
    {
        foreach (var (name, value) in texts) yield return new KeyValuePair<string, FlowValue>(name, FlowValue.Text(value));
    }
}
