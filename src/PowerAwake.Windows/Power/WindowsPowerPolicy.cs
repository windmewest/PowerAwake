using System.Runtime.InteropServices;
using PowerAwake.Core.Models;
using PowerAwake.Core.Services;

namespace PowerAwake.Windows.Power;

public sealed class WindowsPowerPolicy : IPowerPolicy
{
    private static readonly Guid SubgroupProcessor = new("238c9fa8-0aad-41ed-83f4-97be242c8f20");
    private static readonly Guid SubgroupDisplay = new("7516b95f-f776-4464-8c53-06167f40cc99");
    private static readonly Guid Sleep = new("29f6c1db-86da-48c5-9fdb-f2b67b1f44da");
    private static readonly Guid Hibernate = new("9d7815a6-7ee4-497e-8888-515a05f02364");
    private static readonly Guid DisplayDim = new("17aaa29b-8b43-4b94-aafe-35f64daaf1ee");
    private static readonly Guid DisplayOff = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
    private static readonly Guid DimBrightness = new("f1fbfde2-a960-4165-9f88-50667911ce96");

    public Guid GetActiveScheme()
    {
        PowrProfNative.EnsureSuccess(PowrProfNative.PowerGetActiveScheme(IntPtr.Zero, out var pointer), "读取活动电源计划");
        try
        {
            return pointer == IntPtr.Zero ? throw new InvalidOperationException("Windows 未返回活动电源计划。") : Marshal.PtrToStructure<Guid>(pointer);
        }
        finally
        {
            PowrProfNative.LocalFree(pointer);
        }
    }

    public PowerValues ReadValues(Guid schemeGuid) => new()
    {
        SleepAc = Read(schemeGuid, SubgroupProcessor, Sleep, false),
        SleepDc = Read(schemeGuid, SubgroupProcessor, Sleep, true),
        HibernateAc = Read(schemeGuid, SubgroupProcessor, Hibernate, false),
        HibernateDc = Read(schemeGuid, SubgroupProcessor, Hibernate, true),
        DimAc = Read(schemeGuid, SubgroupDisplay, DisplayDim, false),
        DimDc = Read(schemeGuid, SubgroupDisplay, DisplayDim, true),
        DisplayOffAc = Read(schemeGuid, SubgroupDisplay, DisplayOff, false),
        DisplayOffDc = Read(schemeGuid, SubgroupDisplay, DisplayOff, true),
        DimBrightnessAc = Read(schemeGuid, SubgroupDisplay, DimBrightness, false),
        DimBrightnessDc = Read(schemeGuid, SubgroupDisplay, DimBrightness, true)
    };

    public void WriteValues(Guid schemeGuid, PowerValues values, bool activate = true)
    {
        Write(schemeGuid, SubgroupProcessor, Sleep, values.SleepAc, false);
        Write(schemeGuid, SubgroupProcessor, Sleep, values.SleepDc, true);
        Write(schemeGuid, SubgroupProcessor, Hibernate, values.HibernateAc, false);
        Write(schemeGuid, SubgroupProcessor, Hibernate, values.HibernateDc, true);
        Write(schemeGuid, SubgroupDisplay, DisplayDim, values.DimAc, false);
        Write(schemeGuid, SubgroupDisplay, DisplayDim, values.DimDc, true);
        Write(schemeGuid, SubgroupDisplay, DisplayOff, values.DisplayOffAc, false);
        Write(schemeGuid, SubgroupDisplay, DisplayOff, values.DisplayOffDc, true);
        Write(schemeGuid, SubgroupDisplay, DimBrightness, values.DimBrightnessAc, false);
        Write(schemeGuid, SubgroupDisplay, DimBrightness, values.DimBrightnessDc, true);
        if (activate)
        {
            PowrProfNative.EnsureSuccess(PowrProfNative.PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid), "重新激活电源计划");
        }
    }

    private static uint Read(Guid scheme, Guid subgroup, Guid setting, bool dc)
    {
        var result = dc
            ? PowrProfNative.PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, out var value)
            : PowrProfNative.PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, out value);
        PowrProfNative.EnsureSuccess(result, "读取电源设置");
        return value;
    }

    private static void Write(Guid scheme, Guid subgroup, Guid setting, uint value, bool dc)
    {
        var result = dc
            ? PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, value)
            : PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, value);
        PowrProfNative.EnsureSuccess(result, "写入电源设置");
    }
}