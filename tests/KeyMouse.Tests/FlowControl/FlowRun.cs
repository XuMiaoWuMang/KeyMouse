using System.Text.Json;
using KeyMouse;
using KeyMouse.Flow;
using KeyMouse.Runtime;

namespace KeyMouse.Tests;

/// <summary>
/// 跑一条流程但不碰桌面：普通步骤交给一个假的 dispatcher（记下它收到的 argv），
/// set / assert / 条件循环由引擎自己执行，结果从 <c>--report</c> 里读回来。
///
/// 为什么用报告而不是解析控制台：控制台是给人看的，随时可以改措辞；报告里的 origin、退出码、
/// 成功/失败计数才是编辑器与测试共同依赖的契约。
/// </summary>
internal static class FlowRun
{
    private static int _seq;

    internal sealed record Result(
        int ExitCode,
        ScriptReport? Report,
        string Output,
        IReadOnlyList<string[]> Dispatched);

    /// <summary>Writes the JSON to a fresh temp directory, runs it, and cleans the directory up.</summary>
    internal static Result Run(string flowJson, params string[] options) =>
        Run(flowJson, null, null, options);

    internal static Result Run(string flowJson, Func<string[], int> execute, params string[] options) =>
        Run(flowJson, null, execute, options);

    internal static Result Run(string flowJson, string? fileName, Func<string[], int>? execute, params string[] options)
    {
        string directory = Path.Combine(Path.GetTempPath(),
            $"keymouse-flow-control-{Environment.ProcessId}-{Interlocked.Increment(ref _seq)}");
        Directory.CreateDirectory(directory);
        try
        {
            string flow = Path.Combine(directory, fileName ?? "flow.json");
            File.WriteAllText(flow, flowJson);
            return RunPath(flow, execute, options);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Runs a flow that already exists (call tests need sibling subflows next to it).</summary>
    internal static Result RunPath(string flowPath, Func<string[], int>? execute = null, params string[] options)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(flowPath))!;
        string report = Path.Combine(directory, $"report-{Interlocked.Increment(ref _seq)}.json");
        var dispatched = new List<string[]>();
        Func<string[], int> dispatcher = execute ?? (args =>
        {
            dispatched.Add(args);
            return 0;
        });

        TextWriter oldOut = Console.Out;
        TextWriter oldError = Console.Error;
        var output = new StringWriter();
        int exitCode;
        Console.SetOut(output);
        Console.SetError(output);
        try
        {
            exitCode = FlowRunner.Run([flowPath, "--report", report, .. options],
                new Commands.GlobalOptions(), dispatcher);
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldError);
        }

        ScriptReport? parsed = null;
        if (File.Exists(report))
        {
            parsed = JsonSerializer.Deserialize<ScriptReport>(File.ReadAllText(report), FlowDocument.Json);
            File.Delete(report);
        }
        return new Result(exitCode, parsed, output.ToString(), dispatched);
    }

    /// <summary>A one-line JSON document, for tests that only care about the steps.</summary>
    internal static string Flow(string steps, string variables = "{}") =>
        $$"""{"format":"keymouse-flow","version":1,"variables":{{variables}},"steps":{{steps}}}""";
}
