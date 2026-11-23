using KeyMouse.Runtime;

namespace KeyMouse.Tests.Runtime;

/// <summary>
/// 报告落盘应保留旧文件直到新报告完成；替换失败时改用备用位置并清理临时文件。
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

            Harness.Group("report: 替换受读共享锁阻止时保留旧报告并使用备用路径");
            string primaryDirectory = Path.Combine(root, "primary");
            Directory.CreateDirectory(primaryDirectory);
            string primary = Path.Combine(primaryDirectory, "report.json");
            const string previousReport = "旧报告必须完整保留\r\n";
            File.WriteAllText(primary, previousReport);

            // Allow in-place writes, but deny deletion/rename. Direct WriteAllText would
            // overwrite the old report; committing a completed replacement must fall back.
            using (var reader = new FileStream(primary, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite))
            {
                ScriptRunner.WriteReport(primary, new ScriptReport
                {
                    Script = "locked-report-test.json",
                    Total = 3,
                    Succeeded = 3,
                    ExitCode = 0,
                });

                Harness.Equal("替换失败后旧报告逐字不变", previousReport, File.ReadAllText(primary));
                string fallbackJson = File.ReadAllText(fallback);
                Harness.Check("替换失败后备用报告包含本次完整结果",
                    fallbackJson.Contains("\"script\": \"locked-report-test.json\"") &&
                    fallbackJson.Contains("\"succeeded\": 3") &&
                    fallbackJson.Contains("\"exitCode\": 0"));
                Harness.Sequence("替换失败后主目录无临时文件残留", [primary],
                    Directory.GetFiles(primaryDirectory));
                Harness.Sequence("备用目录无临时文件残留", [fallback],
                    Directory.GetFiles(Path.GetDirectoryName(fallback)!));
            }

            Harness.Group("report: 正常替换完整报告且不吞无关异常");
            ScriptRunner.WriteReport(primary, new ScriptReport
            {
                Script = "替换成功.json",
                Total = 4,
                Succeeded = 4,
                ExitCode = 0,
            });
            string replacement = File.ReadAllText(primary);
            Harness.Check("解锁后主报告可完整替换且保留中文",
                replacement.Contains("\"script\": \"替换成功.json\"") &&
                replacement.Contains("\"succeeded\": 4") &&
                replacement.EndsWith(Environment.NewLine, StringComparison.Ordinal));
            Harness.Check("主报告使用无 BOM 的 UTF-8",
                !File.ReadAllBytes(primary).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
            Harness.Sequence("正常替换后无临时文件残留", [primary],
                Directory.GetFiles(primaryDirectory));
            Harness.Throws<ArgumentException>("无效路径异常仍向调用方传播",
                () => ScriptRunner.WriteReport("invalid\0report.json", new ScriptReport()));
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
