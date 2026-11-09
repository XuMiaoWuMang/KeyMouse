using System.Globalization;

namespace KeyMouse.Flow;

/// <summary>One executable node in a plan. Origins are the editor's row keys ("3", "6.1").</summary>
internal abstract class FlowStatement
{
    internal string Origin { get; init; } = "";
}

/// <summary>A list of statements plus the number of steps a static walk would execute.</summary>
internal sealed class FlowBlock
{
    internal List<FlowStatement> Statements { get; } = [];
    internal long PlannedSteps { get; set; }
}

/// <summary>A real step: something the runner reports, retries and can be skipped by `when`.</summary>
internal sealed class StepStatement : FlowStatement
{
    internal required FlowStep Step { get; init; }
    internal FlowProbe? Probe { get; init; }

    /// <summary>`set`: the parsed right-hand side.</summary>
    internal Expression? Value { get; init; }

    /// <summary>`assert`: the parsed condition.</summary>
    internal Expression? Assertion { get; init; }
}

internal sealed class IfStatement : FlowStatement
{
    internal required Expression Condition { get; init; }
    internal FlowProbe? Probe { get; init; }
    internal required FlowBlock Then { get; init; }
    internal FlowBlock? Else { get; init; }
}

internal sealed class WhileStatement : FlowStatement
{
    internal required Expression Condition { get; init; }
    internal FlowProbe? Probe { get; init; }
    internal required FlowBlock Body { get; init; }
    internal int MaxRounds { get; init; }
}

internal sealed class DoStatement : FlowStatement
{
    internal required Expression Condition { get; init; }
    internal FlowProbe? Probe { get; init; }
    internal required FlowBlock Body { get; init; }
    internal int MaxRounds { get; init; }
}

/// <summary>`for (init; cond; step)`: init/step are ordered assignments, evaluated before/after each round.</summary>
internal sealed class ForStatement : FlowStatement
{
    internal required IReadOnlyList<(string Name, Expression Value)> Init { get; init; }
    internal required Expression Condition { get; init; }
    internal FlowProbe? Probe { get; init; }
    internal required IReadOnlyList<(string Name, Expression Value)> Step { get; init; }
    internal required FlowBlock Body { get; init; }
    internal int MaxRounds { get; init; }
}

/// <summary>Legacy `repeat`: still expanded per round at run time, with its own frame per round.</summary>
internal sealed class RepeatStatement : FlowStatement
{
    internal int Times { get; init; }
    internal required FlowBlock Body { get; init; }
}

/// <summary>Legacy `foreach`: one child frame per item, `index` and `item` in scope.</summary>
internal sealed class ForeachStatement : FlowStatement
{
    internal required string[] Items { get; init; }
    internal required FlowBlock Body { get; init; }
}

/// <summary>`call`: an inlined subflow with its own frame; only `export` names come back.</summary>
internal sealed class CallStatement : FlowStatement
{
    internal required FlowDocument Document { get; init; }
    internal required FlowBlock Body { get; init; }
    internal Dictionary<string, string> Vars { get; init; } = new(StringComparer.Ordinal);
    internal string[] Export { get; init; } = [];
}

internal sealed class BreakStatement : FlowStatement { }

internal sealed class ContinueStatement : FlowStatement { }

/// <summary>
/// The statement tree a flow will run.
///
/// Why a tree rather than the old flat list: `if`/`while`/`do`/`for` decide at **run time** which
/// statements execute, so a plan built entirely before the first step cannot describe them. The tree
/// keeps exactly one execution engine (a recursive walk) while preserving what the flat plan gave us:
/// one report row per executed step, one `origin` per document row, pause/cancel between steps, and
/// `--delay` / `--retry` / `--dry-run` behaving the same for every step.
///
/// `repeat`/`foreach`/`call` are still static in the sense that their shape is known up front; they
/// are represented as statements so `break`/`continue` and dynamic loops compose with them.
/// </summary>
internal sealed class FlowPlan
{
    /// <summary>Calls nested deeper than this are refused: a subflow tree is not a call stack to abuse.</summary>
    internal const int MaxCallDepth = 8;

    private FlowPlan(FlowBlock root, int plannedSteps, bool hasDynamicControl, bool hasTimeline)
    {
        Root = root;
        PlannedSteps = plannedSteps;
        HasDynamicControl = hasDynamicControl;
        HasTimeline = hasTimeline;
    }

    internal FlowBlock Root { get; }

    /// <summary>
    /// Steps a static walk would execute: legacy loops expanded, `if` counts both branches,
    /// while/do/for count their body once. Exact for flows without control flow (which is what the
    /// progress denominator uses), a useful lower bound otherwise.
    /// </summary>
    internal int PlannedSteps { get; }

    /// <summary>True when the number of executed steps depends on conditions or early exit.</summary>
    internal bool HasDynamicControl { get; }

    /// <summary>True when any executed step carries a recorded timeline (`atMs`).</summary>
    internal bool HasTimeline { get; }

    internal static FlowPlan Build(FlowDocument document, string documentPath)
    {
        string full = Path.GetFullPath(documentPath);
        var builder = new Builder([full]);
        var scope = new HashSet<string>(document.Texts.Keys, StringComparer.Ordinal) { "last" };
        FlowBlock root = builder.BuildBlock(document, document.Steps, scope, prefix: string.Empty, depth: 0);
        long planned = Math.Min(root.PlannedSteps, int.MaxValue);
        return new FlowPlan(root, (int)planned, builder.HasDynamicControl, builder.HasTimeline);
    }

    private sealed class Builder
    {
        private readonly string _baseDirectory;
        private readonly List<string> _stack;

        internal Builder(List<string> stack)
        {
            _stack = stack;
            _baseDirectory = Path.GetDirectoryName(stack[0]) ?? ".";
        }

        internal bool HasDynamicControl { get; private set; }
        internal bool HasTimeline { get; private set; }

        internal FlowBlock BuildBlock(
            FlowDocument document,
            List<FlowStep> steps,
            HashSet<string> scope,
            string prefix,
            int depth,
            string separator = ".")
        {
            var block = new FlowBlock();
            var names = new HashSet<string>(scope, StringComparer.Ordinal);

            for (int index = 0; index < steps.Count; index++)
            {
                FlowStep step = steps[index];
                string origin = prefix.Length == 0
                    ? (index + 1).ToString(CultureInfo.InvariantCulture)
                    : $"{prefix}{separator}{index + 1}";

                FlowStatement statement = BuildStatement(document, step, origin, names, depth);
                block.Statements.Add(statement);
                block.PlannedSteps = Add(block.PlannedSteps, Count(statement));

                // Names a later `call` may legitimately pass down / see. Kept in step with the
                // validator: legacy read-text and set define names for the rest of the block.
                if (step.Type == "set" && step.Name is { Length: > 0 } set) names.Add(set);
                if (step.Type == "read-text" && step.Into is { Length: > 0 } into) names.Add(into);
                if (step.Type == "call")
                {
                    foreach (string name in step.Export ?? []) names.Add(name);
                }
            }

            return block;
        }

        private FlowStatement BuildStatement(
            FlowDocument document, FlowStep step, string origin, HashSet<string> names, int depth)
        {
            switch (step.Type)
            {
                case "repeat":
                {
                    FlowBlock body = BuildBlock(document, step.Steps ?? [], Add(names, "index"), origin, depth);
                    return new RepeatStatement
                    {
                        Origin = origin,
                        Times = Math.Clamp(step.Times ?? 0, 0, FlowDocument.MaxLoopIterations),
                        Body = body,
                    };
                }

                case "foreach":
                {
                    string[] items = step.In is { } list && document.Lists.TryGetValue(list, out string[]? values)
                        ? values
                        : [];
                    FlowBlock body = BuildBlock(document, step.Steps ?? [], Add(names, "index", "item"), origin, depth);
                    return new ForeachStatement { Origin = origin, Items = items, Body = body };
                }

                case "call":
                    return BuildCall(document, step, origin, names, depth);

                case "if":
                {
                    HasDynamicControl = true;
                    FlowBlock then = BuildBlock(document, step.Steps ?? [], Copy(names), origin, depth);
                    FlowBlock? otherwise = step.Else is { Count: > 0 } alternative
                        ? BuildBlock(document, alternative, Copy(names), $"{origin}.e", depth, separator: "")
                        : null;
                    return new IfStatement
                    {
                        Origin = origin,
                        Condition = Parse(step.Cond!, origin, "cond"),
                        Probe = step.Probe,
                        Then = then,
                        Else = otherwise,
                    };
                }

                case "while":
                {
                    HasDynamicControl = true;
                    FlowBlock body = BuildBlock(document, step.Steps ?? [], Copy(names), origin, depth);
                    return new WhileStatement
                    {
                        Origin = origin,
                        Condition = Parse(step.Cond!, origin, "cond"),
                        Probe = step.Probe,
                        Body = body,
                        MaxRounds = step.MaxRounds ?? FlowValidator.DefaultRounds(step.Type),
                    };
                }

                case "do":
                {
                    HasDynamicControl = true;
                    FlowBlock body = BuildBlock(document, step.Steps ?? [], Copy(names), origin, depth);
                    return new DoStatement
                    {
                        Origin = origin,
                        Condition = Parse(step.Cond!, origin, "cond"),
                        Probe = step.Probe,
                        Body = body,
                        MaxRounds = step.MaxRounds ?? FlowValidator.DefaultRounds(step.Type),
                    };
                }

                case "for":
                {
                    HasDynamicControl = true;
                    var loop = Copy(names);
                    var init = new List<(string, Expression)>();
                    foreach (var (name, value) in step.Init ?? [])
                    {
                        init.Add((name, Parse(value, origin, $"init 里的 {name}")));
                        loop.Add(name);
                    }
                    var stepUpdates = new List<(string, Expression)>();
                    foreach (var (name, value) in step.Step ?? [])
                    {
                        stepUpdates.Add((name, Parse(value, origin, $"step 里的 {name}")));
                    }
                    FlowBlock body = BuildBlock(document, step.Steps ?? [], loop, origin, depth);
                    return new ForStatement
                    {
                        Origin = origin,
                        Init = init,
                        Condition = Parse(step.Cond!, origin, "cond"),
                        Probe = step.Probe,
                        Step = stepUpdates,
                        Body = body,
                        MaxRounds = step.MaxRounds ?? FlowValidator.DefaultRounds(step.Type),
                    };
                }

                case "break":
                    HasDynamicControl = true;
                    return new BreakStatement { Origin = origin };

                case "continue":
                    HasDynamicControl = true;
                    return new ContinueStatement { Origin = origin };

                case "set":
                    if (step.AtMs is not null) HasTimeline = true;
                    return new StepStatement
                    {
                        Origin = origin,
                        Step = step,
                        Probe = step.Probe,
                        Value = Parse(step.Value!, origin, "value"),
                    };

                case "assert":
                    if (step.AtMs is not null) HasTimeline = true;
                    return new StepStatement
                    {
                        Origin = origin,
                        Step = step,
                        Probe = step.Probe,
                        Assertion = Parse(step.Cond!, origin, "cond"),
                    };

                default:
                    if (step.AtMs is not null) HasTimeline = true;
                    if (step.Steps is not null || step.Else is not null)
                    {
                        // A non-block type that carries children is a format error the validator
                        // already refused; keep the plan honest if it ever sneaks through.
                        throw new CommandFailure(2, $"第 {origin} 步的 {step.Type} 不能有子步骤");
                    }
                    return new StepStatement { Origin = origin, Step = step };
            }
        }

        private CallStatement BuildCall(
            FlowDocument document, FlowStep step, string origin, HashSet<string> names, int depth)
        {
            if (depth + 1 > MaxCallDepth)
                throw new CommandFailure(2, $"第 {origin} 步的 call 嵌套超过 {MaxCallDepth} 层（子流程树不是用来堆栈的）");

            string path = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(_stack[^1]) ?? _baseDirectory, step.Flow ?? ""));

            // Names handed down by this call are in scope while validating the subflow too: a
            // subflow that documents "call me with `extra`" must not be refused for using it.
            var inherited = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var (name, _) in step.Vars ?? []) inherited.Add(name);
            FlowDocument sub = FlowDocument.Load(path, null, _stack, inherited);

            var subScope = new HashSet<string>(sub.Texts.Keys, StringComparer.Ordinal);
            subScope.UnionWith(inherited);

            var stack = new List<string>(_stack) { path };
            var child = new Builder(stack);
            FlowBlock body = child.BuildBlock(sub, sub.Steps, subScope, origin, depth + 1);
            HasDynamicControl |= child.HasDynamicControl;
            HasTimeline |= child.HasTimeline;

            return new CallStatement
            {
                Origin = origin,
                Document = sub,
                Body = body,
                Vars = step.Vars is null
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(step.Vars, StringComparer.Ordinal),
                Export = step.Export ?? [],
            };
        }

        private static Expression Parse(string source, string origin, string field)
        {
            try
            {
                return Expression.Parse(source, $"第 {origin} 步的 {field}");
            }
            catch (CommandFailure ex)
            {
                // The validator normally catches this first with a better location; this is the
                // safety net for programmatically built documents (the editor's in-memory model).
                throw new CommandFailure(2, ex.Message);
            }
        }

        private static long Count(FlowBlock block) => block.PlannedSteps;

        /// <summary>
        /// Saturating arithmetic: a pathological nest of loops can multiply past long.MaxValue, and a
        /// negative "planned" number would be worse than an obvious cap in the header.
        /// </summary>
        private static long Add(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;

        private static long Multiply(long a, long b) =>
            a == 0 || b == 0 ? 0 : a > long.MaxValue / b ? long.MaxValue : a * b;

        private static long Count(FlowStatement statement) => statement switch
        {
            StepStatement => 1,
            IfStatement branch => Add(Count(branch.Then), branch.Else is null ? 0 : Count(branch.Else)),
            WhileStatement loop => Count(loop.Body),
            DoStatement loop => Count(loop.Body),
            ForStatement loop => Count(loop.Body),
            RepeatStatement loop => Multiply(loop.Times, Count(loop.Body)),
            ForeachStatement loop => Multiply(loop.Items.Length, Count(loop.Body)),
            CallStatement call => Count(call.Body),
            _ => 0,
        };

        private static HashSet<string> Add(HashSet<string> scope, params string[] names)
        {
            var copy = Copy(scope);
            copy.UnionWith(names);
            return copy;
        }

        private static HashSet<string> Copy(HashSet<string> scope) => new(scope, StringComparer.Ordinal);
    }
}
