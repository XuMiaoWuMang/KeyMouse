using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace KeyMouse.Runner;

internal static class RunnerPipe
{
    // Use the raw descriptor: PipeSecurity normalizes the SACL and drops mandatory-label ACEs.
    // Passing the whole descriptor to CreateNamedPipe makes the ACL atomic with publication.
    internal static byte[] AccessDescriptor(SecurityIdentifier user, bool elevated)
    {
        string label = elevated ? "S:(ML;;NW;;;HI)" : "";
        var acl = new RawSecurityDescriptor($"O:{user}D:P(D;;GA;;;NU)(A;;GA;;;{user}){label}");
        var bytes = new byte[acl.BinaryLength];
        acl.GetBinaryForm(bytes, 0);
        return bytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        internal int Length;
        internal IntPtr Descriptor;
        internal int InheritHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipeW(string name, uint openMode, uint pipeMode,
        uint maxInstances, uint outBuffer, uint inBuffer, uint timeout, ref SecurityAttributes security);

    internal static NamedPipeServerStream Listen(string name)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new UnauthorizedAccessException("Runner 无法确定当前用户 SID。");
        byte[] descriptor = AccessDescriptor(user, new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator));
        var pinned = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
        try
        {
            var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = pinned.AddrOfPinnedObject() };
            const uint duplex = 3, overlapped = 0x40000000, rejectRemoteClients = 8, unlimitedInstances = 255;
            var handle = CreateNamedPipeW(@"\\.\pipe\" + name, duplex | overlapped, rejectRemoteClients,
                unlimitedInstances, 0, 0, 0, ref security);
            try
            {
                if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastPInvokeError());
                return new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, handle);
            }
            catch { handle.Dispose(); throw; }
        }
        finally { pinned.Free(); }
    }
}
