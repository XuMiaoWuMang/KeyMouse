using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyMouse.Windowing;

internal static class NativeError
{
    // Only for APIs documented to set last-error on failure. Capture before formatting or logging.
    internal static CommandFailure Last(string api, int exitCode = 1)
    {
        int error = Marshal.GetLastPInvokeError();
        return new CommandFailure(exitCode, $"{api} 失败：Win32 {error}（{new Win32Exception(error).Message}）");
    }
}
