using System.Reflection;
using System.Text.Json;

namespace KeyMouse;

/// <summary>
/// One field the format declares. Everything the editor needs to render a control - and everything the
/// loader needs to know whether the field is required - comes from the contract file, not from code
/// that was written twice.
/// </summary>
internal sealed class FlowField
{
    internal required string Name { get; init; }
    /// <summary>How to render it: text, multiline, int, bool, enum, point, region, target, steps, vars, stringList, when, const.</summary>
    internal required string Kind { get; init; }
    internal bool Required { get; init; }
    /// <summary>Set when the field is only required in one situation, e.g. a target once coordinates are client-relative.</summary>
    internal string? RequiredWhen { get; init; }
    internal int? Min { get; init; }
    internal int? Max { get; init; }
    internal string[] Values { get; init; } = [];
    internal string? Constant { get; init; }
    internal string? Default { get; init; }
    /// <summary>坐标空间被格式固定成这个值（如拖拽只能客户区），界面上不该让人选。</summary>
    internal string? FixedSpace { get; init; }

    /// <summary>枚举值的中文标签（值本身仍是英文机器值）。</summary>
    internal IReadOnlyDictionary<string, string> ValueLabels { get; init; } = new Dictionary<string, string>();
    internal string LabelZh { get; init; } = "";
    internal string LabelEn { get; init; } = "";
    internal string HelpZh { get; init; } = "";
    internal string HelpEn { get; init; } = "";

    internal FlowField WithLabel(string zh) => new()
    {
        Name = Name, Kind = Kind, Required = Required, RequiredWhen = RequiredWhen, Min = Min, Max = Max,
        Values = Values, Constant = Constant, Default = Default, FixedSpace = FixedSpace, ValueLabels = ValueLabels, LabelZh = zh, LabelEn = LabelEn,
        HelpZh = HelpZh, HelpEn = HelpEn,
    };
}

/// <summary>What one step type accepts: the fields it uses, and which of them are required.</summary>
internal sealed class FlowStepContract
{
    internal required string Type { get; init; }
    internal string SummaryZh { get; init; } = "";
    internal string SummaryEn { get; init; } = "";
    /// <summary>The step's own fields (without the common ones).</summary>
    internal required IReadOnlyList<FlowField> Own { get; init; }
    /// <summary>Fields at least one of which must be present (e.g. a key is either a combo or a single key).</summary>
    internal IReadOnlyList<string> OneOf { get; init; } = [];
    /// <summary>Everything the type accepts: its own fields plus the common ones every step has.</summary>
    internal IReadOnlyList<FlowField> All { get; init; } = [];
}

/// <summary>
/// The flow format's contract, read from the embedded `flow.schema.json`.
///
/// This exists because the editor and the loader used to keep separate ideas of what a step needs, and
/// they drifted: the inspector hid the parameters of `click-text` (region, text, match, timeout, button)
/// while the loader demanded them. Now there is one document, shipped inside the assembly, that says
/// which parameters exist, which are required, what they mean and how to render them - and both sides
/// read it.
/// </summary>
internal static class FlowSchema
{
    private const string ResourceName = "KeyMouse.flow.schema.json";

    internal static IReadOnlyList<FlowField> DocumentFields { get; }
    internal static IReadOnlyList<FlowField> CommonFields { get; }
    internal static IReadOnlyDictionary<string, FlowStepContract> Steps { get; }
    internal static IReadOnlyDictionary<string, IReadOnlyList<FlowField>> Shapes { get; }

    static FlowSchema()
    {
        using Stream stream = typeof(FlowSchema).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"程序集里没有内嵌 {ResourceName}");
        using JsonDocument schema = JsonDocument.Parse(stream);

        JsonElement root = schema.RootElement;
        DocumentFields = ReadFields(root.GetProperty("document").GetProperty("fields"));
        CommonFields = ReadFields(root.GetProperty("common"));

        var steps = new Dictionary<string, FlowStepContract>(StringComparer.Ordinal);
        var shapes = new Dictionary<string, IReadOnlyList<FlowField>>(StringComparer.Ordinal);
        foreach (JsonProperty shape in root.GetProperty("shapes").EnumerateObject())
        {
            shapes[shape.Name] = ReadFields(shape.Value.GetProperty("fields"));
        }

        foreach (JsonProperty step in root.GetProperty("steps").EnumerateObject())
        {
            IReadOnlyList<FlowField> own = ReadFields(step.Value.GetProperty("fields"));
            var oneOf = new List<string>();
            if (step.Value.TryGetProperty("oneOf", out JsonElement oneOfElement))
            {
                oneOf.AddRange(oneOfElement.EnumerateArray().Select(e => e.GetString() ?? ""));
            }
            steps[step.Name] = new FlowStepContract
            {
                Type = step.Name,
                SummaryZh = Text(step.Value, "summary", "zh"),
                SummaryEn = Text(step.Value, "summary", "en"),
                Own = own,
                OneOf = oneOf,
                All = [.. own, .. CommonFields],
            };
        }

        Steps = steps;
        Shapes = shapes;
    }

    internal static bool TryGetStep(string type, out FlowStepContract contract) => Steps.TryGetValue(type, out contract!);

    /// <summary>The fields a step of this type shows: its own, then the common ones.</summary>
    internal static IReadOnlyList<FlowField> FieldsFor(string type) =>
        TryGetStep(type, out FlowStepContract contract) ? contract.All : CommonFields;

    /// <summary>The fields of a nested shape (`target`, `region`, `point`, `when`), or nothing if it has none.</summary>
    internal static IReadOnlyList<FlowField> ShapeFields(string kind) =>
        Shapes.TryGetValue(kind, out IReadOnlyList<FlowField>? fields) ? fields : [];

    /// <summary>The image of the contract as JSON, for `KeyMouse schema` and for tests.</summary>
    internal static string Json
    {
        get
        {
            using Stream stream = typeof(FlowSchema).Assembly.GetManifestResourceStream(ResourceName)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>Reads `{ "zh": "...", "en": "..." }.language`, tolerating a missing block.</summary>
    private static string Text(JsonElement element, string property, string language)
    {
        if (!element.TryGetProperty(property, out JsonElement block) || block.ValueKind != JsonValueKind.Object) return "";
        if (!block.TryGetProperty(language, out JsonElement text) || text.ValueKind != JsonValueKind.String) return "";
        return text.GetString() ?? "";
    }

    private static IReadOnlyList<FlowField> ReadFields(JsonElement array) =>
        array.EnumerateArray().Select(ReadField).ToList();

    private static FlowField ReadField(JsonElement element) => new()
    {
        Name = element.GetProperty("name").GetString() ?? "",
        Kind = element.GetProperty("kind").GetString() ?? "text",
        Required = element.TryGetProperty("required", out JsonElement required) && required.GetBoolean(),
        RequiredWhen = element.TryGetProperty("requiredWhen", out JsonElement when) ? when.GetString() : null,
        Min = element.TryGetProperty("min", out JsonElement min) ? min.GetInt32() : null,
        Max = element.TryGetProperty("max", out JsonElement max) ? max.GetInt32() : null,
        Values = element.TryGetProperty("values", out JsonElement values)
            ? values.EnumerateArray().Select(v => v.GetString() ?? "").ToArray()
            : [],
        Constant = element.TryGetProperty("value", out JsonElement constant) ? constant.GetString() : null,
        Default = element.TryGetProperty("default", out JsonElement fallback) ? fallback.ToString() : null,
        FixedSpace = element.TryGetProperty("fixedSpace", out JsonElement fixedSpace) ? fixedSpace.GetString() : null,
        ValueLabels = ReadLabels(element),
        LabelZh = Text(element, "label", "zh"),
        LabelEn = Text(element, "label", "en"),
        HelpZh = Text(element, "help", "zh"),
        HelpEn = Text(element, "help", "en"),
    };

    /// <summary>True when the step carries a usable value for this field (absent and empty count as no).</summary>
    /// <summary>读枚举值的中文标签；没写就返回空表（界面回落到显示机器值）。</summary>
    private static IReadOnlyDictionary<string, string> ReadLabels(JsonElement element)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!element.TryGetProperty("valueLabels", out JsonElement map) || map.ValueKind != JsonValueKind.Object) return labels;
        foreach (JsonProperty pair in map.EnumerateObject())
        {
            if (pair.Value.ValueKind == JsonValueKind.Object && pair.Value.TryGetProperty("zh", out JsonElement zh))
            {
                labels[pair.Name] = zh.GetString() ?? pair.Name;
            }
            else if (pair.Value.ValueKind == JsonValueKind.String)
            {
                labels[pair.Name] = pair.Value.GetString() ?? pair.Name;
            }
        }
        return labels;
    }

    internal static bool HasValue(FlowStep step, string field)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(step, FlowDocument.Json));

        // 支持点分路径（region.y）：嵌套结构里的必填项也要能问"填了没有"。
        JsonElement value = document.RootElement;
        foreach (string part in field.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return false;
        }
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => value.GetString() is { Length: > 0 },
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => true,
        };
    }
}
