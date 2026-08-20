using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PowerAwake.Windows.Power;

internal static class PowrProfNative
{
    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, out uint value);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, out uint value);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, uint value);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern uint PowerWriteDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, uint value);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr LocalFree(IntPtr memory);

    internal static void EnsureSuccess(uint result, string operation)
    {
        if (result != 0)
        {
            throw new Win32Exception((int)result, $"{operation}失败（Win32 错误 {result}）。");
        }
    }
}