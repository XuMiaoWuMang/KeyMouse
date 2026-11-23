using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;

namespace KeyMouse.Windowing;

internal sealed record TokenCapabilities(
    [property: JsonPropertyName("uiAccess")] bool UiAccess,
    [property: JsonPropertyName("integrityLevel")] string IntegrityLevel,
    [property: JsonPropertyName("integrityRid")] int IntegrityRid,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("sessionId")] int SessionId,
    [property: JsonIgnore] string UserSid)
{
    internal static TokenCapabilities Read(int pid)
    {
        using Process process = Process.GetProcessById(pid);
        using SafeProcessHandle handle = OpenProcess(0x1000, false, pid);
        if (handle.IsInvalid || !OpenProcessToken(handle, 0x0008, out SafeAccessTokenHandle token))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        using (token)
        {
            int uiAccess = ReadInfo(token, 26, p => Marshal.ReadInt32(p));
            int rid = ReadInfo(token, 25, p =>
            {
                IntPtr sid = Marshal.ReadIntPtr(p);
                int count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            });
            string user = ReadInfo(token, 1, p => new SecurityIdentifier(Marshal.ReadIntPtr(p)).Value);
            string level = rid switch { < 0x1000 => "untrusted", < 0x2000 => "low", < 0x3000 => "medium", < 0x4000 => "high", _ => "system" };
            return new(uiAccess != 0, level, rid, pid, process.SessionId, user);
        }
    }

    private static T ReadInfo<T>(SafeAccessTokenHandle token, int kind, Func<IntPtr, T> read)
    {
        GetTokenInformation(token, kind, IntPtr.Zero, 0, out int length);
        if (length <= 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr data = Marshal.AllocHGlobal(length);
        try
        {
            if (!GetTokenInformation(token, kind, data, length, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return read(data);
        }
        finally { Marshal.FreeHGlobal(data); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int kind, IntPtr data, int size, out int required);
    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
}
