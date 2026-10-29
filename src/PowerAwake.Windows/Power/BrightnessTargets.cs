using System.Management;

namespace PowerAwake.Windows.Power;

public enum DimmingMethod
{
    InternalPanel,
    DdcCi,
    Software
}

/// <summary>One independently dimmable display surface (laptop panel, DDC/CI monitor or software overlay).</summary>
internal interface IBrightnessTarget : IDisposable
{
    DimmingMethod Method { get; }

    /// <summary>Current brightness in percent (0-100), or null when it cannot be read.</summary>
    byte? Get();

    void Set(byte percent);
}

/// <summary>Built-in laptop panel through the WMI ACPI brightness interface.</summary>
internal sealed class WmiBrightnessTarget : IBrightnessTarget
{
    private readonly ManagementObject brightnessObject;
    private readonly ManagementObject methodsObject;
    private readonly ManagementBaseObject setParams;

    private WmiBrightnessTarget(ManagementObject brightnessObject, ManagementObject methodsObject, ManagementBaseObject setParams)
    {
        this.brightnessObject = brightnessObject;
        this.methodsObject = methodsObject;
        this.setParams = setParams;
    }

    public DimmingMethod Method => DimmingMethod.InternalPanel;

    public static WmiBrightnessTarget? TryCreate()
    {
        try
        {
            using var brightnessSearcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
            using var methodsSearcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            var brightness = brightnessSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
            var methods = methodsSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (brightness is null || methods is null)
            {
                brightness?.Dispose();
                methods?.Dispose();
                return null;
            }

            var parameters = methods.GetMethodParameters("WmiSetBrightness");
            parameters["Timeout"] = 0;
            return new WmiBrightnessTarget(brightness, methods, parameters);
        }
        catch
        {
            return null;
        }
    }

    public byte? Get()
    {
        try
        {
            brightnessObject.Get();
            return Convert.ToByte(brightnessObject["CurrentBrightness"]);
        }
        catch
        {
            return null;
        }
    }

    public void Set(byte percent)
    {
        try
        {
            setParams["Brightness"] = percent;
            using var _ = methodsObject.InvokeMethod("WmiSetBrightness", setParams, null);
        }
        catch
        {
            // Best effort: a failed write just skips this fade step.
        }
    }

    public void Dispose()
    {
        setParams.Dispose();
        brightnessObject.Dispose();
        methodsObject.Dispose();
    }
}

/// <summary>External monitor controlled through DDC/CI (dxva2 monitor configuration API).</summary>
internal sealed class DdcBrightnessTarget : IBrightnessTarget
{
    private readonly IntPtr handle;
    private readonly uint minimum;
    private readonly uint maximum;

    public DdcBrightnessTarget(IntPtr handle, uint minimum, uint maximum)
    {
        this.handle = handle;
        this.minimum = minimum;
        this.maximum = maximum;
    }

    public DimmingMethod Method => DimmingMethod.DdcCi;

    public static DdcBrightnessTarget? TryCreate(IntPtr handle)
    {
        try
        {
            if (DisplayNative.GetMonitorBrightness(handle, out var min, out _, out var max) && max > min)
            {
                return new DdcBrightnessTarget(handle, min, max);
            }
        }
        catch
        {
            // dxva2 refused the handle: treat as "no DDC/CI".
        }

        return null;
    }

    public byte? Get()
    {
        try
        {
            if (!DisplayNative.GetMonitorBrightness(handle, out var min, out var current, out var max) || max <= min)
            {
                return null;
            }

            return (byte)Math.Clamp((int)Math.Round((current - min) * 100.0 / (max - min)), 0, 100);
        }
        catch
        {
            return null;
        }
    }

    public void Set(byte percent)
    {
        try
        {
            var raw = minimum + (uint)Math.Round((maximum - minimum) * Math.Min(percent, (byte)100) / 100.0);
            DisplayNative.SetMonitorBrightness(handle, raw);
        }
        catch
        {
            // Best effort.
        }
    }

    public void Dispose()
    {
        try
        {
            DisplayNative.DestroyPhysicalMonitor(handle);
        }
        catch
        {
            // Handle already invalid.
        }
    }
}

/// <summary>
/// Fallback for displays without any hardware brightness control (many desktop monitors):
/// a click-through black layered window whose opacity emulates a lower backlight.
/// </summary>
internal sealed class OverlayBrightnessTarget : IBrightnessTarget
{
    // Never fully black out the screen, so the user can always see that the machine is awake.
    private const int MaxAlpha = 230;

    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new(false);
    private IntPtr window;
    private uint threadId;
    private byte alpha;

    public OverlayBrightnessTarget(DisplayNative.Rect bounds)
    {
        thread = new Thread(() => Run(bounds)) { IsBackground = true, Name = "PowerAwake overlay" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait(2000);
    }

    public DimmingMethod Method => DimmingMethod.Software;

    public bool IsCreated => window != IntPtr.Zero;

    private void Run(DisplayNative.Rect bounds)
    {
        threadId = DisplayNative.GetCurrentThreadId();
        window = DisplayNative.CreateWindowEx(
            DisplayNative.WsExTopmost | DisplayNative.WsExTransparent | DisplayNative.WsExToolWindow | DisplayNative.WsExLayered | DisplayNative.WsExNoActivate,
            "Static",
            null,
            DisplayNative.WsPopup | DisplayNative.SsBlackRect,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        if (window != IntPtr.Zero)
        {
            DisplayNative.SetLayeredWindowAttributes(window, 0, 0, DisplayNative.LwaAlpha);
        }

        ready.Set();
        if (window == IntPtr.Zero)
        {
            return;
        }

        while (DisplayNative.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            DisplayNative.DispatchMessage(ref message);
        }

        DisplayNative.DestroyWindow(window);
    }

    public byte? Get() => (byte)(100 - (int)Math.Round(alpha * 100.0 / MaxAlpha));

    public void Set(byte percent)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        alpha = (byte)Math.Round((100 - Math.Min(percent, (byte)100)) * MaxAlpha / 100.0);
        DisplayNative.SetLayeredWindowAttributes(window, 0, alpha, DisplayNative.LwaAlpha);
        if (alpha == 0)
        {
            DisplayNative.ShowWindow(window, DisplayNative.SwHide);
            return;
        }

        DisplayNative.ShowWindow(window, DisplayNative.SwShowNoActivate);
        DisplayNative.SetWindowPos(window, DisplayNative.HwndTopmost, 0, 0, 0, 0, DisplayNative.SwpNoMove | DisplayNative.SwpNoSize | DisplayNative.SwpNoActivate);
    }

    public void Dispose()
    {
        if (threadId != 0)
        {
            DisplayNative.PostThreadMessage(threadId, DisplayNative.WmQuit, UIntPtr.Zero, IntPtr.Zero);
            thread.Join(1000);
        }

        ready.Dispose();
    }
}
