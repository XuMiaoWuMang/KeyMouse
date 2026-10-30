using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyMouse.Runtime;

/// <summary>
/// 让"正在跑的那一条"能被别的东西按停：命令行每次启动都在本进程里建一个手动复位的命名事件
/// <c>Local\KeyMouse-pause-&lt;pid&gt;</c>，谁把它置上，我们就停在下一步之前；清掉就继续。
///
/// 为什么是命名事件而不是管道：外壳要能暂停的是**它自己起的那个子进程**，不需要握手、
/// 不需要协议版本，知道 pid 就够了。命令行这边也只在最省事的地方接一下——FlowRunner 本来
/// 就在每一步之前问一次 <see cref="IExecutionControl.BetweenSteps"/>，那条缝正好。
///
/// 事件是"手动复位"的：置上以后一直有效，直到对方显式清掉。用自动复位事件会出现"停一步
/// 又跑一步"的抖动。
internal sealed class PauseControl : IExecutionControl, IDisposable
{
    private const uint EventModifyState = 0x0002;
    private const int Forever = -1;

    private readonly IntPtr _handle;
    private bool _announced;

    private PauseControl(IntPtr handle) => _handle = handle;

    /// <summary>命令行自己那一侧的固定名字：外壳按 pid 拼出来就能打开它。</summary>
    internal static string NameFor(int pid) => $@"Local\KeyMouse-pause-{pid}";

    /// <summary>
    /// 建好事件并挂到 <see cref="Execution.Control"/> 上。建不出来（极少数受限环境）就返回
    /// null —— 暂停只是锦上添花，不该因为它让整条流程跑不了。
    /// </summary>
    internal static PauseControl? Install(int pid)
    {
        IntPtr handle = CreateEventW(IntPtr.Zero, true, false, NameFor(pid));
        if (handle == IntPtr.Zero) return null;
        PauseControl control = new(handle);
        Execution.Control = control;
        return control;
    }

    public bool Cancelled => false;

    public void BetweenSteps(int index)
    {
        // 没被置上就直接走过去：这是几乎每一次调用的路径，不要在这里做别的事。
        if (WaitForSingleObject(_handle, 0) != 0) return;

        if (!_announced)
        {
            _announced = true;
            Console.Error.WriteLine("已暂停：下一步还没发出去。要停就现在按 Ctrl+Alt+Q（或连按两次 Esc）。");
        }

        while (WaitForSingleObject(_handle, 0) == 0) Thread.Sleep(50);
        _announced = false;
        Console.Error.WriteLine("继续。");
    }

    public void StepStarted(int index, string type) { }

    public void StepFinished(int index, int exitCode, long durationMs) { }

    public void RecordingState(string state) { }

    public void Dispose()
    {
        if (Execution.Control == this) Execution.Control = null;
        CloseHandle(_handle);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateEventW(IntPtr attributes, bool manualReset, bool initialState, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, int milliseconds);
}
