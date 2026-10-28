using System.Runtime.InteropServices;
using System.Text;
using Windows.Storage.Pickers;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 选文件。默认走系统的现代选择器；**失败就回退到经典对话框**。
///
/// 为什么要回退：WinUI 的 FileOpenPicker/FileSavePicker 在**提权运行**的进程里会抛
/// COMException 0x80004005（E_FAIL）——这是系统出于安全的设计。而录制管理员权限的窗口时，
/// 编辑器本身也必须提权，于是"打开文件"就废了。经典对话框（comdlg32）在提权进程里照样能用。
///
/// 结构体里的字符串一律用 IntPtr：带 string/StringBuilder 字段的结构体
/// Marshal.SizeOf 会直接抛"cannot be marshaled as an unmanaged structure"（踩过一次）。
/// </summary>
internal static class Picker
{
    private const int OfnReadOnly = 0x00000001;
    private const int OfnOverwritePrompt = 0x00000002;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;

    /// <summary>用经典对话框代替现代选择器（调试用：设置 KEYMOUSE_PICKER=legacy）。</summary>
    private static bool ForceLegacy =>
        string.Equals(Environment.GetEnvironmentVariable("KEYMOUSE_PICKER"), "legacy",
            StringComparison.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
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
        if (!ForceLegacy)
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
                // 提权 / 打包身份缺失时现代选择器不可用，回退。
            }
        }

        return Legacy(owner, save: false, null);
    }

    /// <summary>选保存位置；取消返回 null。</summary>
    internal static async Task<string?> SaveFlowAsync(IntPtr owner, string suggestedName)
    {
        if (!ForceLegacy)
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
            }
        }

        return Legacy(owner, save: true, suggestedName);
    }

    private static string? Legacy(IntPtr owner, bool save, string? suggestedName)
    {
        const int capacity = 1024;

        IntPtr filter = IntPtr.Zero, title = IntPtr.Zero, ext = IntPtr.Zero, file = IntPtr.Zero;
        try
        {
            filter = Marshal.StringToHGlobalUni("流程文件 (*.json)\0*.json\0所有文件 (*.*)\0*.*\0");
            title = Marshal.StringToHGlobalUni(save ? "保存流程" : "打开流程");
            ext = Marshal.StringToHGlobalUni("json");
            file = Marshal.AllocHGlobal(capacity * sizeof(char));
            for (int i = 0; i < capacity; i++) Marshal.WriteInt16(file, i * sizeof(char), 0);
            if (!string.IsNullOrEmpty(suggestedName))
            {
                char[] seed = suggestedName.ToCharArray();
                for (int i = 0; i < Math.Min(seed.Length, capacity - 1); i++)
                    Marshal.WriteInt16(file, i * sizeof(char), (short)seed[i]);
            }

            var dialog = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = owner,
                lpstrFilter = filter,
                lpstrFile = file,
                nMaxFile = capacity,
                lpstrTitle = title,
                lpstrDefExt = ext,
                Flags = OfnNoChangeDir | OfnPathMustExist |
                        (save ? OfnOverwritePrompt : OfnFileMustExist | OfnReadOnly),
            };

            bool ok = save ? GetSaveFileNameW(ref dialog) : GetOpenFileNameW(ref dialog);
            if (!ok) return null;

            string? picked = Marshal.PtrToStringUni(dialog.lpstrFile);
            return string.IsNullOrWhiteSpace(picked) ? null : picked;
        }
        finally
        {
            foreach (IntPtr block in new[] { filter, title, ext, file })
                if (block != IntPtr.Zero) Marshal.FreeHGlobal(block);
        }
    }
}
