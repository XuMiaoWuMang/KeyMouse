using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using KeyMouse.Runner;

namespace KeyMouse.Tests;

/// <summary>
/// 前后端通信契约的一致性检查。
///
/// 契约本体在 src/KeyMouse.Runner/runner-protocol.schema.json —— 那是唯一的真相，两侧都必须对上：
/// 后端（RunnerHost / RunnerEvent）提供什么，前端（编辑器的 KeyMouseBridge）调用什么，
/// 以及协议本身的规则（终结事件、id 回显、参数袋）。
///
/// 这条测试的价值全在"改一边就当场红"：谁加了一个方法、改了一个字段名、或者忘了把它写进契约，
/// 这里立刻失败，而不是等到运行时表现为"点了没反应"。
/// </summary>
internal static class RunnerProtocolTests
{
    public static void Run()
    {
        Harness.Group("protocol: 前后端通信契约");

        string root = RepoRoot();
        string schemaPath = Path.Combine(root, "src", "KeyMouse.Runner", "runner-protocol.schema.json");
        Harness.Check("契约文件存在", File.Exists(schemaPath), schemaPath);
        if (!File.Exists(schemaPath)) return;

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        JsonElement root_ = schema.RootElement;

        Harness.Equal("契约格式名", "keymouse-runner-protocol", root_.GetProperty("format").GetString());
        Harness.Equal("契约版本", 1, root_.GetProperty("version").GetInt32());

        // ---- 方法：后端真的处理这些，前端真的只调用这些 ----
        var declared = Methods(root_).ToHashSet(StringComparer.Ordinal);
        var handled = RunnerProtocol.Methods.ToHashSet(StringComparer.Ordinal);
        Harness.Check(
            $"契约里的方法 = 后端实现的方法（契约 {declared.Count} 个 / 后端 {handled.Count} 个）",
            declared.SetEquals(handled),
            "只在契约里: " + string.Join(", ", declared.Except(handled)) +
            " | 只在后端: " + string.Join(", ", handled.Except(declared)));

        var called = FrontEndCalls(root);
        Harness.Check(
            $"前端调用的方法都在契约里（前端调用了 {called.Count} 个）",
            called.IsSubsetOf(declared),
            "契约里没有: " + string.Join(", ", called.Except(declared)));

        // ---- 事件：工厂造得出的，契约都要认识 ----
        var eventKinds = EventKinds(root_).ToHashSet(StringComparer.Ordinal);
        var produced = new[]
        {
            RunnerEvent.Log(1, 1, "x").Kind,
            RunnerEvent.Step(1, 1, 1, 1, "running", "click").Kind,
            RunnerEvent.Finished(1, 1, 0, 1).Kind,
            RunnerEvent.Error(1, 1, "x").Kind,
            RunnerEvent.Result(1, default).Kind,
            RunnerEvent.Hello("1").Kind,
        };
        Harness.Check(
            "后端能发出的事件种类都写进了契约",
            produced.All(eventKinds.Contains),
            "契约里没有: " + string.Join(", ", produced.Where(k => !eventKinds.Contains(k))));

        // ---- 终结事件规则：客户端靠这三个结束等待 ----
        var completion = new[] { "finished", "result", "error" };
        var terminals = root_.GetProperty("requests").EnumerateArray()
            .SelectMany(r => r.TryGetProperty("terminal", out var t) ? t.EnumerateArray().Select(x => x.GetString()!) : [])
            .Distinct().ToArray();
        Harness.Check(
            "终结事件只有 result / finished / error",
            terminals.All(completion.Contains),
            string.Join(", ", terminals.Except(completion)));

        Harness.Check(
            "执行类方法以 finished/error 终结",
            MethodTerminals(root_, "run").SetEquals(["finished", "error"]) &&
            MethodTerminals(root_, "record").SetEquals(["finished", "error"]),
            "run=" + string.Join("/", MethodTerminals(root_, "run")));

        // ---- 参数袋：契约声明的字段 == RunnerParameters 的属性 ----
        var declaredParams = root_.GetProperty("parameters").EnumerateObject()
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var realParams = typeof(RunnerParameters).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]).ToHashSet(StringComparer.Ordinal);
        Harness.Check(
            $"参数袋两侧一致（契约 {declaredParams.Count} 个 / 类型 {realParams.Count} 个）",
            declaredParams.SetEquals(realParams),
            "只在契约里: " + string.Join(", ", declaredParams.Except(realParams)) +
            " | 只在类型里: " + string.Join(", ", realParams.Except(declaredParams)));

        // ---- 事件字段：线上能出现的字段都要有定义 ----
        var declaredFields = root_.GetProperty("events").EnumerateObject()
            .SelectMany(e => e.Value.GetProperty("fields").EnumerateArray().Select(f => f.GetString()!))
            .ToHashSet(StringComparer.Ordinal);
        var realFields = typeof(RunnerEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]).ToHashSet(StringComparer.Ordinal);
        Harness.Check(
            "事件字段都有定义（ExtraFields 之外的都在契约里）",
            realFields.IsSubsetOf(declaredFields),
            "契约里没有: " + string.Join(", ", realFields.Except(declaredFields)));

        // ---- 不变量：每个请求都必须有终结事件（hello 曾经的缺口正是破了这一条） ----
        var terminalLess = root_.GetProperty("requests").EnumerateArray()
            .Where(r => MethodTerminals(root_, r.GetProperty("method").GetString()!).Count == 0)
            .Select(r => r.GetProperty("method").GetString()!)
            .ToArray();
        Harness.Check("每个请求都声明了终结事件", terminalLess.Length == 0, "没有终结事件: " + string.Join(", ", terminalLess));
        Harness.Check(
            "hello 与别的方法同一套规则（以 result 终结）",
            MethodTerminals(root_, "hello").SetEquals(["result"]),
            "hello 的终结事件 = " + string.Join("/", MethodTerminals(root_, "hello")));

        // ---- 连接后的问候是问候，不是应答：契约写明，代码也真的在连上时就发 ----
        var greeting = root_.GetProperty("greeting");
        Harness.Equal("问候的 id 固定 0", 0, greeting.GetProperty("id").GetInt32());
        Harness.Check(
            "问候不算任何请求的终结事件",
            !greeting.TryGetProperty("terminal", out _),
            "greeting 里出现了 terminal");
        Harness.Check(
            "连上就发问候（PipeServer 接上后立刻 Emit 一条 hello）",
            File.ReadAllText(Path.Combine(root, "src", "KeyMouse.Runner", "PipeServer.cs"))
                .Contains("Emit(RunnerEvent.Hello(_host.Version))"),
            "PipeServer.cs 里找不到问候语句");

        // ---- 已知缺口：必须是空的；真发现差异，先写进去 ----
        var gaps = root_.GetProperty("knownGaps").GetProperty("items").EnumerateArray()
            .Select(g => g.GetString()!).ToArray();
        Harness.Check("契约没有未记录的缺口", gaps.Length == 0, string.Join(" / ", gaps));

        // ---- client / version：契约声明了，两侧真的在用 ----
        Harness.Equal(
            "契约版本 = RunnerProtocol.ContractVersion",
            root_.GetProperty("version").GetInt32().ToString(),
            RunnerProtocol.ContractVersion);
        Harness.Check(
            "客户端声明协议版本（RunnerClient 无条件带上 client / version）",
            File.ReadAllText(Path.Combine(root, "src", "KeyMouse.Runner", "RunnerClient.cs"))
                .Contains("parameters.Version ??= RunnerProtocol.ContractVersion"),
            "RunnerClient.cs 里没有声明版本");
        Harness.Check(
            "版本判定：不说就放行，同主版本放行，别的版本挡下",
            RunnerProtocol.AcceptsVersion(null) && RunnerProtocol.AcceptsVersion("  ") &&
            RunnerProtocol.AcceptsVersion("1") && RunnerProtocol.AcceptsVersion("1.0") &&
            !RunnerProtocol.AcceptsVersion("2") && !RunnerProtocol.AcceptsVersion("10"),
            "AcceptsVersion 的判定与契约不一致");

        // 行为：直接问 RunnerHost，不走管道 —— 契约里的语义与实机行为必须是一回事。
        var host = new RunnerHost();
        var hello = new List<RunnerEvent>();
        host.HandleAsync(new RunnerRequest { Id = 42, Method = "hello" }, hello.Add).GetAwaiter().GetResult();
        Harness.Check(
            "hello 回一个 result，id 就是请求的 id",
            hello.Count == 1 && hello[0].Kind == "result" && hello[0].Id == 42,
            string.Join(" / ", hello.Select(e => $"{e.Kind}#{e.Id}")));
        Harness.Check(
            "hello 的 result 带着版本与协议号",
            hello.Count == 1 && hello[0].Value is { } v &&
            v.GetProperty("version").GetString() == host.Version &&
            v.GetProperty("protocol").GetString() == RunnerProtocol.ContractVersion,
            hello.Count == 1 ? hello[0].Value?.ToString() ?? "(空)" : "(事件数不对)");

        var mismatch = new List<RunnerEvent>();
        host.HandleAsync(
            new RunnerRequest
            {
                Id = 43, Method = "status",
                Params = new RunnerParameters { Version = "99", Client = "协议自检" },
            },
            mismatch.Add).GetAwaiter().GetResult();
        Harness.Check(
            "版本不合的客户端被挡下（error code=2，中文说明）",
            mismatch.Count == 1 && mismatch[0].Kind == "error" && mismatch[0].Code == 2 &&
            (mismatch[0].Message ?? "").Contains("协议版本"),
            string.Join(" / ", mismatch.Select(e => $"{e.Kind}:{e.Code}:{e.Message}")));

        // 行为：作业记下了"谁让它跑的" —— 一个必然失败的 run 也能建出作业。
        var refused = new List<RunnerEvent>();
        host.HandleAsync(
            new RunnerRequest
            {
                Id = 44, Method = "run",
                Params = new RunnerParameters
                {
                    Flow = Path.Combine(root, "没有这个流程.json"),
                    Client = "协议自检",
                },
            },
            refused.Add).GetAwaiter().GetResult();
        var listing = new List<RunnerEvent>();
        host.HandleAsync(new RunnerRequest { Id = 45, Method = "list" }, listing.Add).GetAwaiter().GetResult();
        string jobs = listing.Count == 1 ? listing[0].Value?.ToString() ?? "" : "";
        Harness.Check(
            "服务端把 client 记在作业上（list 看得到谁让它跑的）",
            jobs.Contains("协议自检"),
            jobs.Length > 0 ? jobs : "(list 没有回 result)");

        // 形状：result 里的具名形状与两侧类型必须一一对应。
        var declaredJobFields = root_.GetProperty("shapes").GetProperty("RunnerJobInfo").EnumerateArray()
            .Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
        var realJobFields = typeof(RunnerJobInfo).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]).ToHashSet(StringComparer.Ordinal);
        Harness.Check(
            $"作业信息形状两侧一致（契约 {declaredJobFields.Count} 个 / 类型 {realJobFields.Count} 个）",
            declaredJobFields.SetEquals(realJobFields),
            "只在契约里: " + string.Join(", ", declaredJobFields.Except(realJobFields)) +
            " | 只在类型里: " + string.Join(", ", realJobFields.Except(declaredJobFields)));

        // ---- 传输：编码、分帧、命名策略 ----
        var transport = root_.GetProperty("transport");
        Harness.Equal("分帧方式", "json-lines", transport.GetProperty("framing").GetString());
        Harness.Equal("命名策略", "camelCase", transport.GetProperty("json").GetProperty("naming").GetString());
        // 契约里写的是模板 keymouse-runner-{user}：这里把 {user} 换成真实用户名再比。
        Harness.Equal(
            "契约里的管道名与实现一致",
            transport.GetProperty("pipeName").GetString()!.Replace("{user}", Environment.UserName),
            RunnerProtocol.PipeName);
    }

    /// <summary>契约里的方法名。</summary>
    private static IEnumerable<string> Methods(JsonElement root) =>
        root.GetProperty("requests").EnumerateArray().Select(r => r.GetProperty("method").GetString()!);

    private static IEnumerable<string> EventKinds(JsonElement root) =>
        root.GetProperty("events").EnumerateObject().Select(e => e.Name);

    private static HashSet<string> MethodTerminals(JsonElement root, string method)
    {
        JsonElement request = root.GetProperty("requests").EnumerateArray()
            .First(r => r.GetProperty("method").GetString() == method);
        return request.TryGetProperty("terminal", out var t)
            ? t.EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal)
            : [];
    }

    /// <summary>
    /// 前端真正调用的方法名：从编辑器源码里扫 SendAsync("...") 。刻意读源码而不是反射，
    /// 因为要抓的正是"前端偷偷多调了一个后端不认识的方法"这种事。
    /// </summary>
    private static HashSet<string> FrontEndCalls(string repoRoot)
    {
        var calls = new HashSet<string>(StringComparer.Ordinal);
        string bridge = Path.Combine(repoRoot, "editor", "KeyMouse.FlowEditor", "KeyMouseBridge.cs");
        if (!File.Exists(bridge)) return calls;
        foreach (Match m in Regex.Matches(File.ReadAllText(bridge), "SendAsync\\(\"([a-z-]+)\""))
        {
            calls.Add(m.Groups[1].Value);
        }

        return calls;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KeyMouse.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
