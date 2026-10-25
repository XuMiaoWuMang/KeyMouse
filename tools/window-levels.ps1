# 列出所有可见窗口以及它们所在进程的权限级别。
# 用途：确认"点了没记录"的那个窗口，是不是跑在更高完整性级别（提权）的进程里。
#
#   .\tools\window-levels.ps1              # 全部可见窗口
#   .\tools\window-levels.ps1 -Filter bili # 只看标题含 bili 的

param([string]$Filter = '')

# 中文列名在 GBK 控制台里会变问号，统一按 UTF-8 输出
$OutputEncoding = [Console]::OutputEncoding = [Text.Encoding]::UTF8

Add-Type @"
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices;
public class KLevel {
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("advapi32.dll", SetLastError = true)] public static extern bool OpenProcessToken(IntPtr p, int access, out IntPtr token);
  [DllImport("advapi32.dll", SetLastError = true)] public static extern bool GetTokenInformation(IntPtr token, int cls, ref int info, int len, out int ret);
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(int access, bool inherit, int pid);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);

  public static List<string[]> Windows() {
    var rows = new List<string[]>();
    EnumWindows((h, p) => {
      if (!IsWindowVisible(h)) return true;
      var t = new StringBuilder(512);
      GetWindowTextW(h, t, 512);
      if (t.Length == 0) return true;
      int pid;
      GetWindowThreadProcessId(h, out pid);
      rows.Add(new string[] { t.ToString(), pid.ToString() });
      return true;
    }, IntPtr.Zero);
    return rows;
  }

  // 0 = 普通用户，1 = 提权（管理员），2 = 读不到（多半是更强的隔离）
  public static int Level(int pid) {
    IntPtr proc = OpenProcess(0x1000, false, pid);
    if (proc == IntPtr.Zero) return 2;
    try {
      IntPtr token;
      if (!OpenProcessToken(proc, 0x0008, out token)) return 2;
      try {
        int info = 0, ret;
        if (!GetTokenInformation(token, 20 /*TokenElevation*/, ref info, 4, out ret)) return 2;
        return info != 0 ? 1 : 0;
      } finally { CloseHandle(token); }
    } finally { CloseHandle(proc); }
  }
}
"@

$rows = [KLevel]::Windows() | Where-Object {
  $Filter.Length -eq 0 -or $_[0].IndexOf($Filter, [StringComparison]::OrdinalIgnoreCase) -ge 0
}

'{0,-16} {1,-8} {2}' -f '进程', '权限', '标题'
foreach ($row in $rows) {
  $pid2 = [int]$row[1]
  $proc = Get-Process -Id $pid2 -ErrorAction SilentlyContinue
  $name = if ($proc) { $proc.ProcessName } else { '(已退出)' }
  $level = [KLevel]::Level($pid2)
  $label = switch ($level) { 0 { '普通' } 1 { '已提权' } default { '读不到' } }
  $title = if ($row[0].Length -gt 54) { $row[0].Substring(0, 54) } else { $row[0] }
  '{0,-16} {1,-8} {2}' -f $name, $label, $title
}
