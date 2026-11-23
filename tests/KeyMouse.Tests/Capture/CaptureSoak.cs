using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyMouse.Tests;

/// <summary>Opt-in leak probe. PrintWindow reads only our hidden control, never the user's screen.</summary>
internal static class CaptureSoak
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetGuiResources(IntPtr process, uint flags);

    internal static int Run()
    {
        if (OcrModels.Locate() is null)
        {
            Console.Error.WriteLine("capture-soak requires installed det.onnx and rec.onnx models");
            return 2;
        }
        using var panel = new System.Windows.Forms.Panel { Size = new System.Drawing.Size(64, 64) };
        var window = new WindowInfo { Handle = panel.Handle, Title = "capture-soak" };
        using var process = Process.GetCurrentProcess();
        string flow = Path.GetTempFileName();
        try
        {
            // Replace the leaf dispatch, not the runner: each invocation loads, plans and runs a new
            // flow in this process. Its leaf takes the real PrintWindow -> crop -> cached OCR path.
            File.WriteAllText(flow, """{"format":"keymouse-flow","version":1,"steps":[{"type":"key","combo":"a"}]}""");
            int reads = 0;
            int ReadFixture(FlowStep _, bool restore)
            {
                Probe.ReadOnce(window, false, (0, 0, 32, 32), capture: "print", engine: "builtin");
                reads++;
                return 0;
            }
            using var quiet = CommandOutput.Redirect(TextWriter.Null, TextWriter.Null);
            int baselineGdi = 0;
            for (int i = -49; i <= 1000; i++)
            {
                if (FlowRunner.Run([flow, "--dry-run"], new(), ReadFixture) != 0) return 1;
                if (i < 0 || i % 250 != 0) continue;
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                process.Refresh();
                int gdi = GetGuiResources(process.Handle, 0);
                if (gdi == 0 && Marshal.GetLastPInvokeError() != 0)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
                Console.WriteLine($"{i,4} runs: private={process.PrivateMemorySize64 / 1024} KiB, handles={process.HandleCount}, GDI={gdi}");
                if (i == 0) baselineGdi = gdi;
                if (gdi > baselineGdi + 2) return 1;
            }
            return reads == 1050 ? 0 : 1;
        }
        finally { File.Delete(flow); }
    }
}
