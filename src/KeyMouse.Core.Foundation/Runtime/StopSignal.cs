using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyMouse.Runtime;

/// <summary>
/// 让"正在录的那一条"能被别的东西按停：录制进程启动时建一个手动复位的命名事件
/// <c>Local\KeyMouse-record-stop-&lt;pid&gt;</c>，谁把它置上，这一轮就收尾、把已经录到的写盘。
///
/// 为什么不用"注入 Ctrl+Alt+Q"：那条路要求注入的键落在前台窗口上、而且钩子看得见注入事件。
/// 录制浮条自己就是前台窗口（WebView2），实测时灵时不灵——用户点「停止」，录制却还在跑。
/// 命名事件不依赖焦点、不依赖注入，只要知道 pid。
///
/// 与 <see cref="PauseControl"/> 是一对：那个是"置上就停一步、清掉就继续"，这个是单向的
/// "置上就收尾"，所以用它的人只管 Set，不管 Reset。
internal sealed class StopSignal : IDisposable
{
    private const int Forever = -1;

    private readonly IntPtr _handle;

    private StopSignal(IntPtr handle) => _handle = handle;

    /// <summary>命令行那一侧的固定名字：外壳按子进程 pid 拼出来就能打开它。</summary>
    internal static string NameFor(int pid) => $@"Local\KeyMouse-record-stop-{pid}";

    /// <summary>
    /// 建好事件。建不出来（极少数受限环境）就返回 null——停止信号是锦上添花，
    /// 不该因为它让整轮录制起不来；键盘上的 Ctrl+Alt+Q 仍然能用。
    /// </summary>
    internal static StopSignal? Install()
    {
        IntPtr handle = CreateEventW(IntPtr.Zero, true, false, NameFor(Environment.ProcessId));
        return handle == IntPtr.Zero ? null : new StopSignal(handle);
    }

    /// <summary>被置上了没有。每次问都只花一次 <c>WaitForSingleObject(…, 0)</c>。</summary>
    internal bool IsSet => WaitForSingleObject(_handle, 0) == 0;

    public void Dispose() => CloseHandle(_handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateEventW(IntPtr attributes, bool manualReset, bool initialState, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, int milliseconds);
}
