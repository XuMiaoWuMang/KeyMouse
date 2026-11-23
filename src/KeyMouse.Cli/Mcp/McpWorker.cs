using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace KeyMouse.Cli;

/// <summary>Keep Core's console output (and native OCR output) off the protocol stream. Each call
/// runs this same binary with a fixed argument vector, never a shell. Serialize access to the desktop.</summary>
internal sealed class McpWorker
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    internal async Task<CallToolResult> Run(IReadOnlyList<string> args, bool json, CancellationToken token, string? input = null)
    {
        await gate.WaitAsync(token);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(60));
            var ct = deadline.Token;
            var info = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, StandardInputEncoding = Utf8,
                RedirectStandardOutput = true, StandardOutputEncoding = Utf8,
                RedirectStandardError = true, StandardErrorEncoding = Utf8,
            };
            // Also support `dotnet KeyMouse.dll mcp`; published releases are apphosts/single files.
            if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                info.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
            foreach (string arg in args) info.ArgumentList.Add(arg);
            info.Environment["KEYMOUSE_OUTPUT_ENCODING"] = "utf-8";
            ct.ThrowIfCancellationRequested();
            using var child = Process.Start(info) ?? throw new IOException("Cannot start KeyMouse worker.");
            try
            {
                var stdout = child.StandardOutput.ReadToEndAsync(ct);
                var stderr = child.StandardError.ReadToEndAsync(ct);
                if (input is not null) await child.StandardInput.WriteAsync(input.AsMemory(), ct);
                child.StandardInput.Close();
                await child.WaitForExitAsync(ct);
                string output = (await stdout).Trim();
                string error = (await stderr).Trim();
                if (child.ExitCode != 0) return Result(child.ExitCode, error.Length > 0 ? error : output);
                return json ? Result(0, data: JsonSerializer.Deserialize<JsonElement>(output)) : Result(0, output);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return Result(1, "KeyMouse worker timed out. Input may have partially completed; inspect before retrying.");
            }
            finally
            {
                if (!child.HasExited)
                {
                    try { child.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) when (child.HasExited) { }
                    await child.WaitForExitAsync();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or JsonException)
        {
            return Result(1, $"KeyMouse worker failed: {ex.Message}");
        }
        finally { gate.Release(); }
    }

    internal static CallToolResult Result(int exitCode, string? message = null, JsonElement? data = null)
    {
        var result = JsonSerializer.SerializeToElement(new { exitCode, message, data });
        return new CallToolResult
        {
            IsError = exitCode != 0,
            StructuredContent = result,
            Content = [new TextContentBlock { Text = result.GetRawText() }],
        };
    }
}
