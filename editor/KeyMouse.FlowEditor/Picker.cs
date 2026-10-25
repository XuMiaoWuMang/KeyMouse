using System.Runtime.InteropServices;
using System.Text;
using Windows.Storage.Pickers;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 选文件。默认走系统的现代选择器；**失败就回退到经典对话框**。
///
/// 为什么要回退：WinUI 的 FileOpenPicker/FileSavePicker 在**提权运行**的进程里会抛
/// COMException 0x80004005（E_FAIL）——这是系统出于安全的设计，不是我们的 bug。
/// 而录制管理员权限的窗口时，编辑器本身也必须提权，于是"打开文件"就废了（实测崩溃日志）。
/// 经典对话框（comdlg32）在提权进程里照样能用，所以拿它兜底。
/// </summary>
internal static class Picker
{
    private const int OfnReadOnly = 0x00000001;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnOverwritePrompt = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string? lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public StringBuilder? lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName dialog);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileNameW(ref OpenFileName dialog);

    /// <summary>选一个流程文件；取消返回 null。</summary>
    internal static async Task<string?> OpenFlowAsync(IntPtr owner)
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".json");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, owner);
            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (Exception)
        {
            // 提权 / 打包身份缺失时现代选择器不可用，回退。没有更好的办法，也不该挡住用户。
            return Legacy(owner, save: false, null);
        }
    }

    /// <summary>选保存位置；取消返回 null。</summary>
    internal static async Task<string?> SaveFlowAsync(IntPtr owner, string suggestedName)
    {
        try
        {
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("流程文件", new List<string> { ".json" });
            picker.SuggestedFileName = suggestedName;
            WinRT.Interop.InitializeWithWindow.Initialize(picker, owner);
            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
        catch (Exception)
        {
            return Legacy(owner, save: true, suggestedName);
        }
    }

    private static string? Legacy(IntPtr owner, bool save, string? suggestedName)
    {
        var buffer = new StringBuilder(suggestedName ?? "", 1024);
        var dialog = new OpenFileName
        {
            lStructSize = Marshal.SizeOf<OpenFileName>(),
            hwndOwner = owner,
            lpstrFilter = "流程文件 (*.json)\0*.json\0所有文件 (*.*)\0*.*\0\0",
            lpstrFile = buffer,
            nMaxFile = buffer.Capacity,
            lpstrTitle = save ? "保存流程" : "打开流程",
            lpstrDefExt = "json",
            Flags = OfnNoChangeDir | OfnPathMustExist |
                    (save ? OfnOverwritePrompt : OfnFileMustExist | OfnReadOnly),
        };

        bool ok = save ? GetSaveFileNameW(ref dialog) : GetOpenFileNameW(ref dialog);
        return ok ? buffer.ToString() : null;
    }
}
