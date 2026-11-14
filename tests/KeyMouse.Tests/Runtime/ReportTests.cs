using KeyMouse.Runtime;

namespace KeyMouse.Tests.Runtime;

/// <summary>
/// 报告只是补充，不能让一次成功运行因为临时目录被锁而看起来失败。
/// 这里故意把主路径指向一个目录（Windows 上写它一定失败），再确认报告落到了备用位置。
/// </summary>
internal static class ReportTests
{
    public static void Run()
    {
        Harness.Group("report: 主路径写不了时改写到临时目录");

        string? oldTmp = Environment.GetEnvironmentVariable("TMP");
        string? oldTemp = Environment.GetEnvironmentVariable("TEMP");
        string root = Path.Combine(Path.GetTempPath(), "KeyMouse.Tests", Guid.NewGuid().ToString("N"));
        string blocked = Path.Combine(root, "blocked");
        Directory.CreateDirectory(blocked);

        try
        {
            Environment.SetEnvironmentVariable("TMP", root);
            Environment.SetEnvironmentVariable("TEMP", root);

            string fallback = Path.Combine(root, "KeyMouse", "editor-run-report.json");

            ScriptRunner.WriteReport(blocked, new ScriptReport
            {
                Script = "report-test.json",
                Total = 2,
                Succeeded = 2,
                ExitCode = 0,
            });

            Harness.Check("主路径是目录时报告写到备用位置", File.Exists(fallback));
            if (File.Exists(fallback))
            {
                string json = File.ReadAllText(fallback);
                Harness.Check("备用报告仍带完整结果", json.Contains("\"succeeded\": 2") &&
                                                       json.Contains("\"exitCode\": 0"));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("TMP", oldTmp);
            Environment.SetEnvironmentVariable("TEMP", oldTemp);
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // 清理失败不影响结论；测试目录在系统临时目录下。
            }
        }
    }
}
