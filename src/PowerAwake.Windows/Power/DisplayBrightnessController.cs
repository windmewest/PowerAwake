using System.Management;

namespace PowerAwake.Windows.Power;

/// <summary>
/// Controls the built-in panel backlight via the WMI monitor-brightness ACPI interface.
/// Only laptops/monitors that expose WmiMonitorBrightness support this (external DDC/CI-only
/// monitors typically do not), so callers must check <see cref="IsAvailable"/>.
/// The WMI objects and method-parameter buffer are resolved once and reused: re-querying and
/// re-enumerating a ManagementObjectSearcher on every brightness step is the dominant cost of
/// a fade (each call is a COM round-trip), and doing that every step during a fade both wastes
/// CPU and makes the fade run noticeably longer than its configured duration.
/// </summary>
internal sealed class DisplayBrightnessController : IDisposable
{
    private readonly ManagementObject? brightnessObject;
    private readonly ManagementObject? methodsObject;
    private readonly ManagementBaseObject? setBrightnessParams;

    public DisplayBrightnessController()
    {
        try
        {
            using var brightnessSearcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
            using var methodsSearcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            brightnessObject = brightnessSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
            methodsObject = methodsSearcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (methodsObject is not null)
            {
                setBrightnessParams = methodsObject.GetMethodParameters("WmiSetBrightness");
                setBrightnessParams["Timeout"] = 0;
            }

            IsAvailable = brightnessObject is not null && methodsObject is not null;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public bool IsAvailable { get; }

    public byte? GetBrightness()
    {
        if (!IsAvailable || brightnessObject is null)
        {
            return null;
        }

        try
        {
            brightnessObject.Get();
            return Convert.ToByte(brightnessObject["CurrentBrightness"]);
        }
        catch
        {
            // Best effort: treat query failures as "unknown", callers skip the fade.
            return null;
        }
    }

    public void SetBrightness(byte percent)
    {
        if (!IsAvailable || methodsObject is null || setBrightnessParams is null)
        {
            return;
        }

        try
        {
            setBrightnessParams["Brightness"] = percent;
            using var _ = methodsObject.InvokeMethod("WmiSetBrightness", setBrightnessParams, null);
        }
        catch
        {
            // Best effort: a failed write just skips this fade step.
        }
    }

    public void Dispose()
    {
        setBrightnessParams?.Dispose();
        brightnessObject?.Dispose();
        methodsObject?.Dispose();
    }
}
