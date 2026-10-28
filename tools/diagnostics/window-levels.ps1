# 列出窗口所在进程的权限级别（排查"点了没记录"：目标是不是提权进程）。
#
#   .\tools\window-levels.ps1                 # 全部可见窗口
#   .\tools\window-levels.ps1 -Filter bili    # 标题或进程名含 bili 的
#   .\tools\window-levels.ps1 -Under          # 光标底下那个窗口（把鼠标停到目标上再跑）

param([string]$Filter = '', [switch]$Under)

# 中文列名在 GBK 控制台里会变问号，统一按 UTF-8 输出
$OutputEncoding = [Console]::OutputEncoding = [Text.Encoding]::UTF8

# 同一个 PowerShell 会话里重复运行会撞名（Add-Type 不能重定义类），
# 所以类名带版本号并先检查：改了下面的 C# 就把版本号 +1。
if (-not ('KLevelV2' -as [type])) {
    Add-Type @"
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices;
public class KLevelV2 {
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("advapi32.dll", SetLastError = true)] public static extern bool OpenProcessToken(IntPtr p, int access, out IntPtr token);
  [DllImport("advapi32.dll", SetLastError = true)] public static extern bool GetTokenInformation(IntPtr token, int cls, ref int info, int len, out int ret);
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(int access, bool inherit, int pid);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

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

  // 光标底下那个顶层窗口：标题、pid、类名
  public static string[] UnderCursor() {
    POINT p; GetCursorPos(out p);
    IntPtr h = GetAncestor(WindowFromPoint(p), 2 /*GA_ROOT*/);
    var t = new StringBuilder(512); GetWindowTextW(h, t, 512);
    var c = new StringBuilder(256); GetClassNameW(h, c, 256);
    int pid; GetWindowThreadProcessId(h, out pid);
    return new string[] { t.ToString(), pid.ToString(), c.ToString() };
  }

  // 0 = 普通，1 = 提权，2 = 读不到（隔离更强，或进程已退出）
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
}

function Level-Label([int]$pid2) {
    $proc = Get-Process -Id $pid2 -ErrorAction SilentlyContinue
    $name = if ($proc) { $proc.ProcessName } else { '(已退出)' }
    $level = if ($proc) { [KLevelV2]::Level($pid2) } else { 2 }
    $label = switch ($level) { 0 { '普通' } 1 { '已提权' } default { '读不到' } }
    return @($name, $label)
}

if ($Under) {
    $one = [KLevelV2]::UnderCursor()
    $info = Level-Label ([int]$one[1])
    '光标下窗口：'
    '  进程  ' + $info[0] + '（pid ' + $one[1] + '）'
    '  权限  ' + $info[1]
    '  类名  ' + $one[2]
    '  标题  ' + $one[0]
    return
}

$rows = [KLevelV2]::Windows() | Where-Object {
    if ($Filter.Length -eq 0) { return $true }
    if ($_[0].IndexOf($Filter, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
    $p = Get-Process -Id ([int]$_[1]) -ErrorAction SilentlyContinue
    return ($p -and $p.ProcessName.IndexOf($Filter, [StringComparison]::OrdinalIgnoreCase) -ge 0)
}

'{0,-18} {1,-8} {2}' -f '进程', '权限', '标题'
foreach ($row in $rows) {
    $info = Level-Label ([int]$row[1])
    $title = if ($row[0].Length -gt 52) { $row[0].Substring(0, 52) } else { $row[0] }
    '{0,-18} {1,-8} {2}' -f $info[0], $info[1], $title
}
