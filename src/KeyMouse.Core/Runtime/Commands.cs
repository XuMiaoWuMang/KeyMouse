using System.Globalization;

namespace KeyMouse.Runtime;
internal static partial class Commands
{
    /// <summary>
    /// 引擎（Core）版本：与产品版本分开发。取 Core 程序集版本，字面量只在根
    /// Directory.Build.props 的 EngineVersion 里出现一次（tools\dev\check-version.ps1 把关）。
    /// </summary>
    internal static string Version => Format(typeof(Commands).Assembly.GetName().Version);

    /// <summary>
    /// 产品版本，从入口程序集的版本取：CLI、编辑器、安装程序都是同一个 $(ProductVersion)。
    /// `--version` 以前只打印引擎版本（2.1.0），于是"我这是不是最新构建"根本看不出来。
    /// </summary>
    internal static string ProductVersion
    {
        get
        {
            System.Reflection.Assembly? entry = System.Reflection.Assembly.GetEntryAssembly();
            return Format((entry ?? typeof(Commands).Assembly).GetName().Version);
        }
    }

    private static string Format(System.Version? version) =>
        version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";

    /// <summary>打印流程格式的接口契约（前端照着它渲染；`schema 类型` 只看一种）。</summary>
    private static int Schema(string[] rest)
    {
        if (rest.Length == 0)
        {
            Console.WriteLine(FlowSchema.Json);
            return 0;
        }

        string type = rest[0];
        if (!FlowSchema.TryGetStep(type, out FlowStepContract contract))
        {
            Console.WriteLine($"不认识的步骤类型 '{type}'（可用：{string.Join(" | ", FlowSchema.Steps.Keys)}）");
            return 2;
        }

        Console.WriteLine($"{type}：{contract.SummaryZh}");
        foreach (FlowField field in contract.All)
        {
            string mark = field.Required ? "必须" : field.RequiredWhen is { } when ? $"条件必须（{when}）" : "可选";
            string latin = field.Values.Length > 0 ? $"  取值：{string.Join(" | ", field.Values)}" : field.Kind == "int" && (field.Min is not null || field.Max is not null) ? $"  范围：{field.Min} .. {field.Max}" : "";
            Console.WriteLine($"  {field.Name,-14} {mark,-22} {field.LabelZh}{latin}");
        }

        if (contract.OneOf.Count > 0)
        {
            Console.WriteLine($"  以上 {string.Join(" / ", contract.OneOf)} 至少给一个。");
        }
        return 0;
    }

    /// <summary>
    /// STA because the region picker is a WinForms window: an interactive desktop selection needs
    /// a single-threaded apartment, and everything else here is unaffected by the choice.
    /// </summary>
    [STAThread]
    internal static int Execute(string[] args)
    {
        // Speak the console's code page, so that whoever reads us decodes what we wrote.
        ConsoleText.ConfigureOutputEncoding();

        try
        {
            string[] rest = ExtractGlobalOptions(args, out GlobalOptions global);
            if (rest.Length == 0) { PrintUsage(); return 2; }

            switch (rest[0].ToLowerInvariant())
            {
                case "-h" or "--help" or "help" or "/?":
                    PrintUsage();
                    return 0;
                case "-v" or "--version" or "version":
                    Console.WriteLine($"KeyMouse {ProductVersion}（引擎 {Version}）");
                    return 0;
                case "schema":
                    // The contract itself, for a frontend that would rather read it than be told about it.
                    return Schema(rest[1..]);
                case "mouse":
                    return Mouse(rest[1..], global);
                case "key" or "keyboard":
                    return Keyboard(rest[1..], global);
                case "window":
                    return WindowGroup(rest[1..], global);
                case "run":
                    // One `run`, two file formats: the text syntax, or a recorded JSON flow. The flow
                    // is detected by content and both go through the same dispatcher below - which is
                    // also what the resident Runner drives, so a job and a typed command cannot drift.
                    if (FlowRunner.LooksLikeJson(rest[1..]))
                        return FlowRunner.Run(rest[1..], global, Execute);
                    return ScriptRunner.Run(rest[1..], global, Execute);
                case "record":
                    return Recorder.Run(rest[1..], global);
                case "probe":
                    return Probe.Run(rest[1..], global);
                case "region":
                    return RegionCommand.Run(rest[1..], global);
                case "doctor":
                    // 环境诊断：在开发树还是安装版、命令行与编辑器各自在哪找到的。
                    // 它不做别的，所以哪怕别的都跑不起来，这条也应当能跑。
                    return Doctor.Run(rest[1..]);
                default:
                    return Fail(2, $"未知命令组 '{rest[0]}'（可用：mouse | key | window | run | record | flow | probe | region | doctor | help）");
            }
        }
        catch (CommandFailure ex)
        {
            return Fail(ex.Code, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Fail(2, ex.Message);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误：{ex.Message}");
            return 1;
        }
    }
}
