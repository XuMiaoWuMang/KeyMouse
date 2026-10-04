using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Frame = KeyMouse.NativeCapture.Frame;

namespace KeyMouse;

/// <summary>
/// 运行证据：把每一步"当时看到的东西"存成 PNG，落在流程文件旁边的 <c>&lt;流程&gt;.shots/</c> 里。
///
/// 为什么必须由引擎做，而不是界面截屏：证据的价值全在"它真的是那一步看到的东西"。
/// 界面不合成、不占位——**抓不到就返回 null**，宁可这一步没有截图，也不给一张假的。
///
/// 命名是 <c>0001-click-text.png</c>：每次运行覆盖同一组文件，所以行上显示的一定是**最近一次**运行的样子，
/// 而且目录不会随着运行次数无限长大。
/// </summary>
internal static class RunShots
{
    /// <summary>给一步留一张证据；拿不到就返回 null。</summary>
    internal static string? Capture(FlowStep step, string flowPath, string? evidenceFor, int stepNo, bool allowRestore)
    {
        try
        {
            WindowInfo? window = ResolveWindow(step.Target, allowRestore);
            if (window is null) return null;

            // 让窗口自己画自己（PrintWindow），而不是抓屏幕：
            // 抓屏幕会拍到"当时最上层的那个窗口"——实测拍到的是编辑器自己的标题栏，
            // 因为目标被盖住时屏幕上根本不是它。执行器读的确实是屏幕，但证据要回答的是
            // "这一步操作的那个窗口当时长什么样"，被遮住时也必须拍得到。
            NativeWindow.GetClientRect(window.Handle, out NativeWindow.RECT client);
            Frame? frame = NativeCapture.TryCapture(
                window.Handle, client.Right - client.Left, client.Bottom - client.Top);

            if (frame is null) return null;

            string directory = DirectoryOf(evidenceFor ?? flowPath);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"{stepNo:d4}-{step.Type}.png");
            Save(frame, path);
            return path;
        }
        catch (Exception)
        {
            // 截图失败不该让这一步失败：它是证据，不是功能。
            return null;
        }
    }

    /// <summary>证据目录：与**用户的那份流程文件**同名，后缀 .shots（编辑器跑内存快照时也落在这里）。</summary>
    internal static string DirectoryOf(string flowPath)
    {
        string full = Path.GetFullPath(flowPath);
        string folder = Path.GetDirectoryName(full) ?? ".";
        return Path.Combine(folder, Path.GetFileNameWithoutExtension(full) + ".shots");
    }

    private static WindowInfo? ResolveWindow(FlowTarget? target, bool allowRestore)
    {
        if (target is null) return null;

        // 复用执行器那份选择器构造：截出来的窗口必须和这一步真正操作的是同一个。
        WindowResolution resolution = WindowResolver.Resolve(FlowRunner.SelectorOf(target), allowRestore);
        return resolution.Usable.Count > 0 ? resolution.Usable[0] : null;
    }

    private static void Save(Frame frame, string path)
    {
        using var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(frame.Bgra, 0, data.Scan0, frame.Bgra.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        bitmap.Save(path, ImageFormat.Png);
    }
}
