using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KeyMouse.Flow;

/// <summary>
/// 流程文件的加载期检查：结构、必填、范围、占位符，以及（新加的）条件表达式。
///
/// 这里的原则只有一条：**能在开跑前查出来的，绝不留给运行时**。条件里的变量名拼错、
/// <c>break</c> 写在循环外、<c>text()</c> 没给读哪块区域，代价必须是在加载时用一句带位置的
/// 中文说清楚，而不是跑完三分钟才炸。
/// </summary>
internal static class FlowValidator
{
    internal const int DefaultWhileRounds = 200;
    internal const int DefaultForRounds = 10_000;
    internal const int MaxRounds = 10_000;

    /// <summary>
    /// 变量名：字母（含中文）或下划线开头，其余可以是字母、数字、下划线、点。
    /// 点留给 <c>--set user.name=…</c> 这种文档变量，占位符与表达式都按同一个名字找。
    /// </summary>
    private static readonly Regex VariableName = new(@"^[\p{L}_][\p{L}\p{Nd}_.]*$", RegexOptions.Compiled);

    /// <summary>表达式里的关键字 / 运行期维护的值，不能被 set 覆盖。</summary>
    private static readonly string[] ReservedNames = ["true", "false", "last"];

    internal static void Validate(
        FlowDocument document, string path, IReadOnlyList<string>? stack, IEnumerable<string>? inherited)
    {
        string full = Path.GetFullPath(path);
        var calls = new CallContext(Path.GetDirectoryName(full) ?? ".", stack ?? [], 0);
        var scope = new HashSet<string>(document.Texts.Keys, StringComparer.Ordinal) { "last" };
        foreach (string name in inherited ?? []) scope.Add(name);
        ValidateSteps(document, document.Steps, scope, prefix: string.Empty, loopDepth: 0, calls);
    }

    private static void ValidateSteps(
        FlowDocument document, List<FlowStep> steps, HashSet<string> scope, string prefix, int loopDepth, CallContext calls)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            string where = prefix.Length == 0 ? $"第 {i + 1} 步" : $"{prefix}第 {i + 1} 步";
            FlowStep step = steps[i] ?? throw new CommandFailure(2, $"{where}是 null");
            ValidateStep(document, step, scope, where, loopDepth, calls);
        }
    }

    private static void ValidateStep(
        FlowDocument document, FlowStep step, HashSet<string> scope, string where, int loopDepth, CallContext calls)
    {
        if (!FlowDocument.KnownTypes.Contains(step.Type))
        {
            throw new CommandFailure(2,
                $"{where}的类型 '{step.Type}' 不认识（可用：{string.Join(" | ", FlowDocument.KnownTypes)}）");
        }

        RejectStray(step, where);
        if (step.Steps is { } children && !FlowDocument.BlockTypes.Contains(step.Type))
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 steps（只有循环和 if 有子步骤）");
        if (step.Type != "if" && step.Else is not null)
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 else（只有 if 有另一条分支）");
        if (step.When is { } when && step.Type is "if" or "while" or "do" or "for" or "break" or "continue" or "repeat" or "foreach")
        {
            throw new CommandFailure(2,
                $"{where}的 {step.Type} 不能带 when：条件请写进 cond；循环体内每一步各自判断");
        }

        if (step.When is { } condition) CheckWhen(step, condition, where);
        if (step.Axis is { Length: > 0 } axis &&
            !string.Equals(axis, FlowAxis.Vertical, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(axis, FlowAxis.Horizontal, StringComparison.OrdinalIgnoreCase))
        {
            throw new CommandFailure(2,
                $"{where}的 axis '{axis}' 不认识（可用：{FlowAxis.Vertical} | {FlowAxis.Horizontal}）");
        }
        CheckRequired(step, where);
        CheckRanges(step, where);
        if (step.Probe is { } probe)
        {
            if (step.Type is not ("if" or "while" or "do" or "for" or "assert"))
                throw new CommandFailure(2, $"{where}的 {step.Type} 没有 probe：它只在条件步骤上使用");
            CheckProbe(probe, where);
        }
        CheckPlaceholders(step, scope, where);

        switch (step.Type)
        {
            case "repeat":
            {
                if (step.Times is not int times || times < 0 || times > FlowDocument.MaxLoopIterations)
                    throw new CommandFailure(2,
                        $"{where}的 repeat 需要 times（0 到 {FlowDocument.MaxLoopIterations} 之间）");
                RequireSteps(step, where, "repeat");
                var inner = Copy(scope);
                inner.Add("index");
                ValidateSteps(document, step.Steps!, inner, $"{where}里", loopDepth + 1, calls);
                return;
            }

            case "foreach":
            {
                if (string.IsNullOrEmpty(step.In))
                    throw new CommandFailure(2, $"{where}的 foreach 需要 in（遍历哪个列表变量）");
                if (!document.Lists.ContainsKey(step.In!))
                    throw new CommandFailure(2,
                        $"{where}的 foreach 遍历 '{step.In}'，但文档 variables 里没有这个列表");
                RequireSteps(step, where, "foreach");
                var inner = Copy(scope);
                inner.Add("index");
                inner.Add("item");
                ValidateSteps(document, step.Steps!, inner, $"{where}里", loopDepth + 1, calls);
                return;
            }

            case "call":
                ValidateCall(step, where, calls);
                foreach (string name in step.Export ?? []) scope.Add(name);
                return;

            case "if":
            {
                RequireCond(step, where, "if");
                RequireSteps(step, where, "if");
                if (step.Else is { Count: 0 })
                    throw new CommandFailure(2, $"{where}的 if 的 else 里没有步骤");
                ValidateExpression(step.Cond!, step, scope, where, "cond");
                ValidateSteps(document, step.Steps!, Copy(scope), $"{where}里", loopDepth, calls);
                if (step.Else is { } otherwise)
                    ValidateSteps(document, otherwise, Copy(scope), $"{where}的 else 里", loopDepth, calls);
                return;
            }

            case "while":
            {
                RequireCond(step, where, "while");
                RequireSteps(step, where, "while");
                CheckMaxRounds(step, where);
                ValidateExpression(step.Cond!, step, scope, where, "cond");
                ValidateSteps(document, step.Steps!, Copy(scope), $"{where}里", loopDepth + 1, calls);
                return;
            }

            case "do":
            {
                RequireCond(step, where, "do");
                RequireSteps(step, where, "do");
                CheckMaxRounds(step, where);
                // do-while 先跑一遍再判断；判断里要用的名字必须在进循环前就有值，加载期按这个查。
                ValidateExpression(step.Cond!, step, scope, where, "cond");
                ValidateSteps(document, step.Steps!, Copy(scope), $"{where}里", loopDepth + 1, calls);
                return;
            }

            case "for":
            {
                RequireCond(step, where, "for");
                RequireSteps(step, where, "for");
                CheckMaxRounds(step, where);
                var loop = Copy(scope);
                foreach (var (name, value) in step.Init ?? [])
                {
                    CheckAssignableName(name, where, "init");
                    ValidateExpression(value, step, loop, where, $"init 里的 {name}");
                    loop.Add(name);
                }
                ValidateExpression(step.Cond!, step, loop, where, "cond");
                // step 里的名字在**每轮结束后**才存在，cond/body 第一轮看不到它们；但 step 内部
                // 是按顺序求值的，后一个赋值可以引用前一个。
                var stepScope = Copy(loop);
                foreach (var (name, value) in step.Step ?? [])
                {
                    CheckAssignableName(name, where, "step");
                    ValidateExpression(value, step, stepScope, where, $"step 里的 {name}");
                    stepScope.Add(name);
                }
                ValidateSteps(document, step.Steps!, Copy(loop), $"{where}里", loopDepth + 1, calls);
                return;
            }

            case "break":
            case "continue":
                RejectSteps(step, where);
                if (loopDepth == 0)
                    throw new CommandFailure(2,
                        $"{where}的 {step.Type} 不在循环里（要放在 while / do / for / repeat / foreach 的体内）");
                return;

            case "set":
            {
                RejectSteps(step, where);
                if (string.IsNullOrWhiteSpace(step.Name))
                    throw new CommandFailure(2, $"{where}的 set 需要 name（写哪个变量）");
                if (string.IsNullOrWhiteSpace(step.Value))
                    throw new CommandFailure(2, $"{where}的 set 需要 value（表达式）");
                CheckAssignableName(step.Name!, where, "set");
                ValidateExpression(step.Value!, step, scope, where, "value");
                scope.Add(step.Name!);
                return;
            }

            case "assert":
            {
                RejectSteps(step, where);
                RequireCond(step, where, "assert");
                ValidateExpression(step.Cond!, step, scope, where, "cond");
                return;
            }
        }

        // Reading steps and every plain command share the same rules.
        if (FlowDocument.ReadingTypes.Contains(step.Type))
        {
            if (step.Region is null)
                throw new CommandFailure(2, $"{where}的 {step.Type} 需要 region（读哪一块）");
            if (step.Match is { } mode && !TextPredicate.IsKnownMode(mode))
                throw new CommandFailure(2,
                    $"{where}的 {step.Type} 的 match '{mode}' 不认识（可用：{string.Join(" | ", TextPredicate.Modes)}）");
            if (step.Region.Space is not (FlowSpace.Client or FlowSpace.Window))
                throw new CommandFailure(2,
                    $"{where}的 {step.Type} 的 region 只支持 {FlowSpace.Client}（客户区）或 {FlowSpace.Window}（整个窗口）：" +
                    "读屏必须落在某个窗口里，没有窗口就没有可比对的坐标系");
            if (step.Type is "wait-text" or "click-text" && string.IsNullOrEmpty(step.Text))
                throw new CommandFailure(2, $"{where}的 {step.Type} 需要 text（等什么字）");
            if (step.Type == "read-text" && string.IsNullOrEmpty(step.Into))
                throw new CommandFailure(2, $"{where}的 read-text 需要 into（读到的东西存进哪个变量）");
        }

        if (step.Type == "read-text" && !string.IsNullOrEmpty(step.Into)) scope.Add(step.Into!);
    }

    /// <summary>Fields that belong to another step type are a typo, not "ignored on purpose".</summary>
    private static void RejectStray(FlowStep step, string where)
    {
        if (step.Cond is not null && step.Type is not ("if" or "while" or "do" or "for" or "assert"))
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 cond（只有 if/while/do/for/assert 有）");
        if (step.MaxRounds is not null && step.Type is not ("while" or "do" or "for"))
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 maxRounds（只有 while/do/for 有）");
        if (step.Init is not null && step.Type != "for")
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 init（只有 for 有）");
        if (step.Step is not null && step.Type != "for")
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 step（只有 for 有）");
        if (step.Name is not null && step.Type != "set")
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 name（只有 set 有）");
        if (step.Value is not null && step.Type != "set")
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 value（只有 set 有）");
        if (step.Message is not null && step.Type != "assert")
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 message（只有 assert 有）");
    }

    private static void RequireSteps(FlowStep step, string where, string type)
    {
        if (step.Steps is not { Count: > 0 })
            throw new CommandFailure(2, $"{where}的 {type} 里没有步骤");
    }

    private static void RejectSteps(FlowStep step, string where)
    {
        if (step.Steps is not null)
            throw new CommandFailure(2, $"{where}的 {step.Type} 不能有 steps");
    }

    private static void RequireCond(FlowStep step, string where, string type)
    {
        if (string.IsNullOrWhiteSpace(step.Cond))
            throw new CommandFailure(2, $"{where}的 {type} 需要 cond（条件表达式，例如 i < 3 或 found(\"保存\")）");
    }

    private static void CheckMaxRounds(FlowStep step, string where)
    {
        (int min, int max) = RoundsRange(step.Type);
        int rounds = step.MaxRounds ?? DefaultRounds(step.Type);
        if (rounds < min || rounds > max)
            throw new CommandFailure(2, $"{where}的 maxRounds = {rounds}，要求 {min} 到 {max}（默认 {DefaultRounds(step.Type)}）");
    }

    /// <summary>
    /// The contract is the single source of truth for a loop's default and bounds; the constants here
    /// are only the fallback for a schema that has not been reloaded (or a programmatic document).
    /// </summary>
    internal static int DefaultRounds(string type) =>
        FlowSchema.TryGetNumberRule(type, "maxRounds", out NumberRule rule) && rule.Default is int fallback
            ? fallback
            : type == "for" ? DefaultForRounds : DefaultWhileRounds;

    private static (int Min, int Max) RoundsRange(string type)
    {
        if (FlowSchema.TryGetNumberRule(type, "maxRounds", out NumberRule rule))
            return (rule.Min, rule.Max);
        return (1, MaxRounds);
    }

    /// <summary>
    /// Checks one expression: parse, then every bare name must exist in the scope the runtime will
    /// have at that point. <c>text()</c> additionally needs the step's probe - there is nowhere else
    /// to learn which window and rectangle to read.
    /// </summary>
    private static void ValidateExpression(string source, FlowStep owner, HashSet<string> scope, string where, string field)
    {
        if (source.Contains("{{", StringComparison.Ordinal))
            throw new CommandFailure(2,
                $"{where}的 {field} 是表达式：变量直接写名字（例如 i < 3），不要用 {{{{名字}}}}");

        Expression expression = Expression.Parse(source, $"{where}的 {field}");
        foreach (string name in expression.Variables)
        {
            if (scope.Contains(name)) continue;
            throw new CommandFailure(2,
                $"{where}的 {field} 用了未定义的变量 '{name}'（文档 variables、set、read-text、循环变量，或者 --set）");
        }

        if (!expression.Functions.Contains("text")) return;
        if (owner.Probe is { Region: not null, Target: { } target } &&
            (!string.IsNullOrWhiteSpace(target.Process) || !string.IsNullOrWhiteSpace(target.Class))) return;
        throw new CommandFailure(2,
            $"{where}的 {field} 用了 text()，需要给这一步的 probe（target + region：读哪个窗口的哪块区域）");
    }

    private static void CheckProbe(FlowProbe probe, string where)
    {
        if (probe.Region is null)
            throw new CommandFailure(2, $"{where}的 probe 需要 region（读哪一块）");
        if (probe.Target is null ||
            string.IsNullOrWhiteSpace(probe.Target.Process) && string.IsNullOrWhiteSpace(probe.Target.Class))
            throw new CommandFailure(2, $"{where}的 probe 需要 target（至少给 process 或 class）");
        if (probe.Region.Space is not (FlowSpace.Client or FlowSpace.Window))
            throw new CommandFailure(2,
                $"{where}的 probe 的 region 只支持 {FlowSpace.Client} 或 {FlowSpace.Window}");
        if (probe.Match is { } mode && !TextPredicate.IsKnownMode(mode))
            throw new CommandFailure(2,
                $"{where}的 probe 的 match '{mode}' 不认识（可用：{string.Join(" | ", TextPredicate.Modes)}）");
        if (probe.MaxErrors is int errors && (errors < 0 || errors > 20))
            throw new CommandFailure(2, $"{where}的 probe 的 maxErrors = {errors}，要求 0 到 20");
    }

    private static void CheckAssignableName(string name, string where, string field)
    {
        if (!VariableName.IsMatch(name))
            throw new CommandFailure(2,
                $"{where}的 {field} 里 '{name}' 不是合法的变量名（字母或下划线开头，只能用字母、数字、下划线、点）");
        if (ReservedNames.Contains(name, StringComparer.Ordinal))
            throw new CommandFailure(2, $"{where}的 {field} 不能用保留名 '{name}'（表达式关键字，或运行期维护的值）");
    }

    // ---------------------------------------------------------------- generic rules

    private static void CheckWhen(FlowStep step, FlowCondition when, string where)
    {
        if (when.Region is null || string.IsNullOrEmpty(when.Text))
            throw new CommandFailure(2, $"{where}的 when 需要 region 与 text（前提是什么）");
        if (when.Match is { } mode && !TextPredicate.IsKnownMode(mode))
            throw new CommandFailure(2,
                $"{where}的 when 的 match '{mode}' 不认识（可用：{string.Join(" | ", TextPredicate.Modes)}）");
        FlowTarget? target = when.Target ?? step.Target;
        if (string.IsNullOrWhiteSpace(target?.Process) && string.IsNullOrWhiteSpace(target?.Class))
            throw new CommandFailure(2,
                $"{where}的 when 需要 target（自己给，或者步骤本身有）——否则不知道该读哪个窗口");
        if (when.Region.Space is not (FlowSpace.Client or FlowSpace.Window))
            throw new CommandFailure(2, $"{where}的 when 的 region 只支持 {FlowSpace.Client} 或 {FlowSpace.Window}");
        if (when.Else is { } otherwise && otherwise is not ("skip" or "fail"))
            throw new CommandFailure(2, $"{where}的 when 的 else '{otherwise}' 不认识（可用：skip | fail）");
    }

    private static void CheckRequired(FlowStep step, string where)
    {
        if (!FlowSchema.TryGetStep(step.Type, out FlowStepContract contract)) return;

        foreach (FlowField field in contract.Own.Where(f => f.Required))
        {
            // Block bodies get a better message from RequireSteps below ("里没有步骤"); the contract
            // still declares the field required so the editor marks it.
            if (field.Name == "steps" && FlowDocument.BlockTypes.Contains(step.Type)) continue;
            if (!FlowSchema.HasValue(step, field.Name))
                throw new CommandFailure(2, $"{where}的 {step.Type} 需要 {field.Name}（{field.LabelZh}）");
        }

        if (contract.OneOf.Count > 0 && !contract.OneOf.Any(name => FlowSchema.HasValue(step, name)))
        {
            string labels = string.Join(" 或 ", contract.OneOf.Select(name =>
                contract.Own.FirstOrDefault(f => f.Name == name)?.LabelZh is { Length: > 0 } label ? label : name));
            throw new CommandFailure(2, $"{where}的 {step.Type} 需要 {labels} 之一");
        }
    }

    private static void CheckRanges(FlowStep step, string where)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(step, FlowDocument.Json));
        JsonElement root = document.RootElement;

        if (FlowSchema.TryGetStep(step.Type, out FlowStepContract contract))
        {
            string owner = $"{where}的 {step.Type}";
            foreach (FlowField field in contract.Own) CheckRange(root, field, owner);
        }

        if (step.When is not null && root.TryGetProperty("when", out JsonElement when))
        {
            string owner = $"{where}的 when";
            foreach (FlowField field in FlowSchema.ShapeFields("when")) CheckRange(when, field, owner);
        }

        if (step.Probe is not null && root.TryGetProperty("probe", out JsonElement probe))
        {
            string owner = $"{where}的 probe";
            foreach (FlowField field in FlowSchema.ShapeFields("probe")) CheckRange(probe, field, owner);
        }
    }

    private static void CheckRange(JsonElement owner, FlowField field, string at)
    {
        if (field.Kind != "int" || (field.Min is null && field.Max is null)) return;
        if (!TryReadNumber(owner, field.Name, out double value)) return;

        string range = field.Min is int low && field.Max is int high
            ? $"{low}..{high}"
            : field.Min is int only ? $"不小于 {only}" : $"不大于 {field.Max}";
        bool bad = field.Min is int min && value < min || field.Max is int max && value > max;
        if (!bad) return;
        string label = field.LabelZh is { Length: > 0 } zh ? $"（{zh}）" : "";
        throw new CommandFailure(2, $"{at} 里 {field.Name} = {value.ToString("0.###", CultureInfo.InvariantCulture)}，契约要求 {range}{label}");
    }

    private static bool TryReadNumber(JsonElement owner, string path, out double value)
    {
        value = 0;
        JsonElement cursor = owner;
        foreach (string part in path.Split('.'))
        {
            if (cursor.ValueKind != JsonValueKind.Object || !cursor.TryGetProperty(part, out cursor)) return false;
        }
        return cursor.ValueKind == JsonValueKind.Number && cursor.TryGetDouble(out value);
    }

    private static void CheckPlaceholders(FlowStep step, HashSet<string> scope, string where)
    {
        foreach (string name in FlowDocument.Placeholders(step))
        {
            if (scope.Contains(name)) continue;
            throw new CommandFailure(2,
                $"{where}用了未定义的变量 {{{{{name}}}}}（文档 variables、set、read-text、循环变量，或者 --set）");
        }
    }

    /// <summary>Where a `call` is being validated from: its own directory, the files already on the
    /// call stack (a subflow may not call back into one of them), and how deep we already are.</summary>
    private readonly record struct CallContext(string BaseDirectory, IReadOnlyList<string> Stack, int Depth);

    private static void ValidateCall(FlowStep step, string where, CallContext calls)
    {
        if (string.IsNullOrEmpty(step.Flow))
            throw new CommandFailure(2, $"{where}的 call 需要 flow（子流程文件，相对当前文件）");

        string path = Path.GetFullPath(Path.Combine(calls.BaseDirectory, step.Flow!));
        if (!File.Exists(path))
            throw new CommandFailure(2, $"{where}的 call 指向的子流程不存在：{path}");
        if (calls.Depth + 1 > FlowPlan.MaxCallDepth)
            throw new CommandFailure(2, $"{where}的 call 嵌套超过 {FlowPlan.MaxCallDepth} 层（子流程树不是用来堆栈的）");
        if (calls.Stack.Contains(path, StringComparer.OrdinalIgnoreCase))
            throw new CommandFailure(2,
                $"{where}的 call 成环了：{string.Join(" → ", calls.Stack)} → {path}");
    }

    private static HashSet<string> Copy(HashSet<string> scope) => new(scope, StringComparer.Ordinal);
}
