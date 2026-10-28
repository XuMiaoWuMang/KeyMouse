using System.Diagnostics;

namespace KeyMouse.Product;
/// <summary>
/// 这一份 KeyMouse 现在跑在哪种目录形态里。
/// 只有两种形态，混淆它们是最容易犯的错——开发时编辑器旁边根本没有 `cli` 文件夹，
/// 装好之后才有；反过来，装好的目录里也不会有仓库、sln 和 artifacts。
/// </summary>
internal enum ProductEnvironment
{
    /// <summary>从源码仓库里跑（sln 在附近），命令行产物在 artifacts\。</summary>
    Development,

    /// <summary>安装版：编辑器在 &lt;安装目录&gt;\，命令行在 &lt;安装目录&gt;\cli\。</summary>
    Installed,
}

/// <summary>一个"这里可能有"的位置，以及为什么认为它可能在。</summary>
internal readonly record struct LayoutCandidate(string Path, string Why)
{
    internal bool Exists => Path.Length > 0 && File.Exists(Path);
}

/// <summary>
/// 目录约定：同一件东西在开发树与安装版里放在不同的地方，而**只有这一个类**说得清这件事。
///
/// 约定（改动前先看 docs/guide/environments.md）：
///
///   开发树（检出本仓库）                     安装版（KeyMouseSetup 装出来）
///   ──────────────────────────────          ──────────────────────────────
///   artifacts\cli\KeyMouse.exe                &lt;安装目录&gt;\cli\KeyMouse.exe
///   src\KeyMouse.FlowEditor\bin\…\*.exe       &lt;安装目录&gt;.exe
///   src\KeyMouse.Cli\bin\…\KeyMouse.exe       （同上，只有一份）
///
/// 查找顺序一律是"离自己最近的、最像当前形态的"在前，注册表与 PATH 在最后兜底：
/// 开发时注册表里往往写着一个旧安装版，如果让它优先，新编辑器会拉起旧命令行——
/// 旧命令行不认识新参数，报的错跟编辑器本身毫无关系（--parent 就这么炸过一次）。
/// </summary>
internal static class ProductLayout
{
    internal const string CliName = "KeyMouse.exe";
    internal const string EditorName = "KeyMouse.FlowEditor.exe";

    /// <summary>产品自己的覆盖开关，测试与排障用：指哪儿用哪儿。</summary>
    internal const string CliOverride = "KEYMOUSE_CLI";
    internal const string EditorOverride = "KEYMOUSE_EDITOR";

    private static readonly Lazy<string?> Repository = new(FindRepository);

    /// <summary>本进程可执行文件所在目录（不带结尾分隔符）。</summary>
    internal static string Here { get; } = Normalize(AppContext.BaseDirectory);

    /// <summary>开发树的根（有 KeyMouse.sln 的那一层）；安装版是 null。</summary>
    internal static string? RepositoryRoot => Repository.Value;

    internal static ProductEnvironment Environment =>
        Repository.Value is { } root && IsUnder(Here, root) ? ProductEnvironment.Development : ProductEnvironment.Installed;

    internal static string EnvironmentName =>
        Environment == ProductEnvironment.Development ? "开发树" : "安装版";

    /// <summary>命令行可能在哪，按优先级排。第一个存在的就是答案。</summary>
    internal static IReadOnlyList<LayoutCandidate> CliCandidates() => Build(
    [
        (System.Environment.GetEnvironmentVariable(CliOverride), $"环境变量 {CliOverride}"),
        (Repository.Value is { } repo ? Path.Combine(repo, "artifacts", "cli", CliName) : null,
            @"开发树：artifacts\cli（tools\build\build.ps1 的产物）"),
        (Repository.Value is { } repo1 ? Path.Combine(repo1, "src", "KeyMouse.Cli", "bin", "Release", "net10.0-windows", CliName) : null,
            @"开发树：src\KeyMouse.Cli\bin\Release（dotnet build 的产物）"),
        (Repository.Value is { } repo2 ? Path.Combine(repo2, "src", "KeyMouse.Cli", "bin", "Debug", "net10.0-windows", CliName) : null,
            @"开发树：src\KeyMouse.Cli\bin\Debug"),
        (Path.Combine(Here, CliName), "自己所在目录"),
        (Path.Combine(Here, "cli", CliName), @"安装版：<安装目录>\cli（编辑器启动命令行时用）"),
        (ProductRegistry.Read(ProductRegistry.CliValue), @"注册表 HKCU\Software\KeyMouse 的 CliPath"),
        (OnPath(CliName), "PATH"),
    ]);

    /// <summary>编辑器可能在哪，按优先级排。</summary>
    internal static IReadOnlyList<LayoutCandidate> EditorCandidates() => Build(
    [
        (System.Environment.GetEnvironmentVariable(EditorOverride), $"环境变量 {EditorOverride}"),
        (Path.Combine(Here, EditorName), @"自己所在目录（安装版：编辑器与命令行同在 <安装目录>\ 时）"),
        (Path.Combine(Parent(Here), EditorName), @"上一级（从 <安装目录>\cli\ 回头看编辑器）"),
        (DevEditor("Release"), @"开发树：src\KeyMouse.FlowEditor\bin\Release\…\win-x64"),
        (DevEditor("Debug"), @"开发树：src\KeyMouse.FlowEditor\bin\Debug\…\win-x64"),
        (ProductRegistry.Read(ProductRegistry.GuiValue) is { } gui ? Path.Combine(gui, EditorName) : null,
            @"注册表 HKCU\Software\KeyMouse 的 GuiPath"),
        (OnPath(EditorName), "PATH"),
    ]);

    internal static string? CliPath => First(CliCandidates());
    internal static string? EditorPath => First(EditorCandidates());

    /// <summary>给人看的环境说明，`KeyMouse doctor` 与 `km env` 都打这一份。</summary>
    internal static IReadOnlyList<string> Describe()
    {
        var lines = new List<string>
        {
            $"环境：{EnvironmentName}",
        };

        lines.Add(RepositoryRoot is { } root
            ? $"仓库：{root}"
            : "仓库：无（安装版没有源码树，这是正常的）");
        lines.Add($"自己：{Here}");
        lines.Add("");

        AppendFind(lines, "命令行", CliCandidates(), require: true);
        lines.Add("");
        AppendFind(lines, "编辑器", EditorCandidates(), require: false);

        if (Environment == ProductEnvironment.Development)
        {
            lines.Add("");
            lines.Add(CliPath is { } cli
                ? $"开发树：编辑器用的就是上面这个命令行（{cli}），不是安装版那个。"
                : @"开发树：还没有可用的命令行——先跑 pwsh tools\build\build.ps1。");
            lines.Add(@"        改了引擎代码要重新生成它（pwsh tools\build\build.ps1），否则编辑器里跑的还是旧逻辑。");
        }

        return lines;
    }

    private static void AppendFind(List<string> lines, string what, IReadOnlyList<LayoutCandidate> candidates, bool require)
    {
        var hit = candidates.FirstOrDefault(c => c.Exists);
        if (hit.Exists)
        {
            lines.Add($"{what}：{hit.Path}");
            lines.Add($"        来自：{hit.Why}");
            return;
        }

        lines.Add($"{what}：找不到");
        foreach (LayoutCandidate candidate in candidates)
        {
            if (candidate.Path.Length == 0) continue;
            lines.Add($"        - 没有 {candidate.Path}（{candidate.Why}）");
        }
        if (require) lines.Add("        → 没有它，播放与试运行都跑不了。");
    }

    private static IReadOnlyList<LayoutCandidate> Build((string? Path, string Why)[] raw)
    {
        var list = new List<LayoutCandidate>(raw.Length);
        foreach ((string? path, string why) in raw)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (list.Any(c => string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(new LayoutCandidate(path, why));
        }
        return list;
    }

    private static string? First(IReadOnlyList<LayoutCandidate> candidates) =>
        candidates.Where(c => c.Exists).Select(c => c.Path).FirstOrDefault();

    /// <summary>编辑器在开发树里的位置：TFM 与 RID 目录名由 csproj 决定，不在这里写死。</summary>
    private static string? DevEditor(string configuration)
    {
        if (Repository.Value is not { } repo) return null;
        string bin = Path.Combine(repo, "src", "KeyMouse.FlowEditor", "bin", configuration);
        if (!Directory.Exists(bin)) return null;

        try
        {
            // 新版在前：换了 TargetPlatformVersion 之后旧目录还在，但人们想看的是刚编的那份。
            return Directory.EnumerateFiles(bin, EditorName, SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static string? FindRepository()
    {
        var directory = new DirectoryInfo(Here);
        for (int depth = 0; depth < 12 && directory is not null; depth++, directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KeyMouse.sln")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return Normalize(directory.FullName);
            }
        }
        return null;
    }

    private static string? OnPath(string name)
    {
        string? path = System.Environment.GetEnvironmentVariable("PATH");
        if (path is null) return null;
        foreach (string folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(folder.Trim().Trim('"'), name);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // PATH 里混进了一个非法路径不该让整个查找失败
            }
        }
        return null;
    }

    private static bool IsUnder(string path, string root) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, root, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string Parent(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        return parent is null ? path : Normalize(parent);
    }
}
