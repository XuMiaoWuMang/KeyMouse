using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KeyMouse.Cli;

internal sealed class McpTools
{
    private readonly McpWorker worker = new();

    [McpServerTool(Name = "list_windows", ReadOnly = true), Description("List visible titled windows. Returns HWND, title, process, PID and foreground state. Optional process name is an exact match.")]
    public Task<CallToolResult> ListWindows(CancellationToken cancellationToken, string? process = null)
    {
        if (process is { Length: > 260 } || process?.Contains('\0') == true) return Invalid("Invalid process name.");
        string[] args = process is null ? ["window", "list", "--json"] : ["window", "list", "--json", "--process=" + process];
        return worker.Run(args, json: true, cancellationToken);
    }

    [McpServerTool(Name = "read_text", ReadOnly = true), Description("OCR the specified window's client area without focusing it. Omit region for the whole client area. Region and returned geometry use pixels. Returns the CLI probe report (lines, confidence, reads).")]
    public Task<CallToolResult> ReadText(string hwnd, CancellationToken cancellationToken, ClientRegion? region = null)
    {
        List<string> args = ["probe", "--capture", "print", "--json"];
        if (region is not null)
        {
            if (region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0 ||
                (long)region.X + region.Width > int.MaxValue || (long)region.Y + region.Height > int.MaxValue)
                return Invalid("region needs nonnegative x/y and positive width/height within the client area.");
            args.Add($"--region={region.X},{region.Y},{region.Width},{region.Height}");
        }
        return At(hwnd, args, cancellationToken, json: true);
    }

    internal sealed record ClientRegion
    {
        public required int X { get; init; }
        public required int Y { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
    }

    [McpServerTool(Name = "click"), Description("Focus the target window and click once at client-area pixel coordinates (x, y). Rejects covered or out-of-bounds points. Button: left, right or middle.")]
    public Task<CallToolResult> Click(string hwnd, int x, int y, CancellationToken cancellationToken, string button = "left") =>
        x < 0 || y < 0 || button is not ("left" or "right" or "middle") ? Invalid("Expected nonnegative x/y and a left, right or middle button.") :
        Input(hwnd, ["mouse", "click", button, $"--wx={x}", $"--wy={y}", "--strict-point"], cancellationToken);

    [McpServerTool(Name = "press_key"), Description("Focus the target window and press a single named key, e.g. enter, tab, escape, a or f5.")]
    public Task<CallToolResult> PressKey(string hwnd, string key, CancellationToken cancellationToken) =>
        !KeyMap.Names.Contains(key, StringComparer.OrdinalIgnoreCase) ? Invalid("Expected a single KeyMouse key name, e.g. enter.") :
        Input(hwnd, ["key", "press", key], cancellationToken);

    [McpServerTool(Name = "type_text"), Description("Focus the target window and type literal Unicode text. A newline sends Enter. Maximum 512 UTF-16 code units per call.")]
    public Task<CallToolResult> TypeText(string hwnd, string text, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(text) || text.Length > 512 ? Invalid("text must contain 1 to 512 UTF-16 code units.") :
        Input(hwnd, ["key", "type", "--stdin"], cancellationToken, input: text);

    private Task<CallToolResult> Input(string hwnd, List<string> args, CancellationToken token, string? input = null)
    {
        args.Add("--focus-policy=gentle");
        return At(hwnd, args, token, json: false, input);
    }

    private Task<CallToolResult> At(string hwnd, List<string> args, CancellationToken token, bool json, string? input = null)
    {
        IntPtr handle;
        try { handle = WindowLocator.ParseHandle(hwnd ?? ""); }
        catch (ArgumentException) { return Invalid("Expected a window handle from list_windows."); }
        if (handle.ToInt64() <= 0) return Invalid("Expected a nonzero window handle from list_windows.");
        args.Add($"--hwnd=0x{handle.ToInt64():X}");
        return worker.Run(args, json, token, input);
    }

    private static Task<CallToolResult> Invalid(string message) => Task.FromResult(McpWorker.Result(2, message));
}
