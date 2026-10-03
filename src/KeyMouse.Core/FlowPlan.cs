using System.Globalization;

namespace KeyMouse;

/// <summary>
/// One frame of variables. A loop body or a subflow sees its own names plus everything its caller
/// had: frames are **chained, not copied**, so a `read-text` inside a subflow writes into that
/// subflow's frame and the caller's values stay the caller's - which is the whole reason a subflow
/// can be reused with different `vars` without surprising the file that called it.
/// </summary>
internal sealed class VariableFrame
{
    private readonly VariableFrame? _parent;

    internal VariableFrame(VariableFrame? parent, IEnumerable<KeyValuePair<string, string>>? values = null)
    {
        _parent = parent;
        Values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (values is null) return;
        foreach (var (name, value) in values) Values[name] = value;
    }

    /// <summary>Names this frame itself defines (a lookup walks up to the parents).</summary>
    internal Dictionary<string, string> Values { get; }

    internal string? Lookup(string name)
    {
        for (VariableFrame? frame = this; frame is not null; frame = frame._parent)
        {
            if (frame.Values.TryGetValue(name, out string? value)) return value;
        }
        return null;
    }

    internal void Set(string name, string value) => Values[name] = value;

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
}

/// <summary>
/// The flat list of steps a file will run, each with the frame it runs in.
///
/// Loops are expanded and subflows are **inlined** here, so the executor still walks one linear list
/// and keeps exactly one place where pause, cancel, per-step events and retries live. A `call` leaves
/// one extra item behind (a step-less one) when the subflow exports variables: that item copies them
/// back into the caller's frame after the subflow's steps have run.
/// </summary>
internal sealed class FlowPlan
{
    /// <summary>
    /// One thing to do. Frame is where the step runs **and** where an export lands (the caller's frame
    /// for an export item); ExportFrom is the subflow frame the values are read from, which is the
    /// difference between "hand the caller the value" and "write it into the subflow again".
    /// </summary>
    internal sealed record Item(
        FlowStep? Step, VariableFrame Frame, IReadOnlyList<string>? Export = null, VariableFrame? ExportFrom = null);

    /// <summary>Calls nested deeper than this are refused: a subflow tree is not a call stack to abuse.</summary>
    internal const int MaxCallDepth = 8;

    internal List<Item> Items { get; } = [];

    internal int StepCount => Items.Count(item => item.Step is not null);

    internal static FlowPlan Build(FlowDocument document, string documentPath)
    {
        string full = Path.GetFullPath(documentPath);
        var plan = new FlowPlan();
        var root = new VariableFrame(null, document.Texts);
        Add(plan, document.Steps, root, document,
            Path.GetDirectoryName(full) ?? ".", [full], 0);
        return plan;
    }

    private static void Add(
        FlowPlan plan, List<FlowStep> steps, VariableFrame frame, FlowDocument document,
        string baseDirectory, List<string> stack, int depth)
    {
        foreach (FlowStep step in steps)
        {
            switch (step.Type)
            {
                case "repeat":
                {
                    int times = Math.Clamp(step.Times ?? 0, 0, FlowDocument.MaxLoopIterations);
                    for (int round = 0; round < times; round++)
                    {
                        var loop = new VariableFrame(frame, [
                            new KeyValuePair<string, string>("index", round.ToString(CultureInfo.InvariantCulture)),
                        ]);
                        Add(plan, step.Steps ?? [], loop, document, baseDirectory, stack, depth);
                    }
                    continue;
                }

                case "foreach":
                {
                    string[] items = step.In is { } name && document.Lists.TryGetValue(name, out string[]? list)
                        ? list
                        : [];
                    for (int round = 0; round < items.Length; round++)
                    {
                        var loop = new VariableFrame(frame, [
                            new KeyValuePair<string, string>("index", round.ToString(CultureInfo.InvariantCulture)),
                            new KeyValuePair<string, string>("item", items[round]),
                        ]);
                        Add(plan, step.Steps ?? [], loop, document, baseDirectory, stack, depth);
                    }
                    continue;
                }

                case "call":
                {
                    string path = Path.GetFullPath(Path.Combine(baseDirectory, step.Flow ?? ""));
                    // Inherited names while validating the subflow: what the caller can see, plus the
                    // names this very call hands down - the subflow may legitimately use either.
                    IEnumerable<string> inherited = step.Vars is { Count: > 0 } passed
                        ? frame.Visible().Concat(passed.Keys)
                        : frame.Visible();
                    FlowDocument sub = FlowDocument.Load(path, null, stack, inherited);

                    // The call's own values are resolved against the caller's frame first, so
                    // `"vars": { "name": "{{item}}" }` passes the loop's value down, not the word.
                    var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var (name, value) in step.Vars ?? [])
                    {
                        overrides[name] = FlowDocument.Expand(value, frame);
                    }

                    var callFrame = new VariableFrame(new VariableFrame(frame, sub.Texts), overrides);
                    Add(plan, sub.Steps, callFrame, sub, Path.GetDirectoryName(path) ?? ".", [.. stack, path], depth + 1);

                    if (step.Export is { Length: > 0 } exported)
                    {
                        plan.Items.Add(new Item(null, frame, exported, callFrame));
                    }
                    continue;
                }
            }

            plan.Items.Add(new Item(step, frame, null));
        }
    }
}
