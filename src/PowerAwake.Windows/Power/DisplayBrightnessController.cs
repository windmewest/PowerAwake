namespace PowerAwake.Windows.Power;

/// <summary>What dimming methods the current machine offers.</summary>
public sealed record DisplayCapabilities(int InternalPanels, int DdcMonitors, int SoftwareMonitors)
{
    public bool HasHardwareControl => InternalPanels + DdcMonitors > 0;

    public bool IsAnySupported => InternalPanels + DdcMonitors + SoftwareMonitors > 0;
}

/// <summary>
/// Discovers every dimmable display and exposes it as an <see cref="IBrightnessTarget"/>.
/// Laptops use the WMI panel interface, external monitors use DDC/CI, and monitors that expose
/// neither (typical for desktops) can optionally fall back to a software overlay.
/// The WMI objects are resolved once and reused: re-querying them on every brightness step is
/// the dominant cost of a fade (each call is a COM round-trip).
/// </summary>
internal static class DisplayBrightnessController
{
    public static IReadOnlyList<IBrightnessTarget> Discover(bool allowSoftware)
    {
        var targets = new List<IBrightnessTarget>();
        var internalPanel = WmiBrightnessTarget.TryCreate();
        if (internalPanel is not null)
        {
            targets.Add(internalPanel);
        }

        foreach (var monitor in EnumerateMonitors())
        {
            var ddc = GetDdcTargets(monitor.Handle);
            if (ddc.Count > 0)
            {
                targets.AddRange(ddc);
            }
            else if (allowSoftware && internalPanel is null)
            {
                // With a WMI panel present an undimmable monitor is most likely the panel itself;
                // overlaying it would double-dim it, so software fallback is desktop-only.
                var overlay = new OverlayBrightnessTarget(monitor.Bounds);
                if (overlay.IsCreated)
                {
                    targets.Add(overlay);
                }
                else
                {
                    overlay.Dispose();
                }
            }
        }

        return targets;
    }

    public static DisplayCapabilities Describe(IReadOnlyList<IBrightnessTarget> targets) => new(
        targets.Count(t => t.Method == DimmingMethod.InternalPanel),
        targets.Count(t => t.Method == DimmingMethod.DdcCi),
        targets.Count(t => t.Method == DimmingMethod.Software));

    private static List<(IntPtr Handle, DisplayNative.Rect Bounds)> EnumerateMonitors()
    {
        var monitors = new List<(IntPtr, DisplayNative.Rect)>();
        try
        {
            DisplayNative.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref DisplayNative.Rect rect, IntPtr data) =>
            {
                monitors.Add((monitor, rect));
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // No monitor enumeration: no targets.
        }

        return monitors;
    }

    private static List<DdcBrightnessTarget> GetDdcTargets(IntPtr monitor)
    {
        var result = new List<DdcBrightnessTarget>();
        try
        {
            if (!DisplayNative.GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0)
            {
                return result;
            }

            var physical = new DisplayNative.PhysicalMonitor[count];
            if (!DisplayNative.GetPhysicalMonitorsFromHMONITOR(monitor, count, physical))
            {
                return result;
            }

            foreach (var item in physical)
            {
                var target = DdcBrightnessTarget.TryCreate(item.Handle);
                if (target is not null)
                {
                    result.Add(target);
                }
                else
                {
                    DisplayNative.DestroyPhysicalMonitor(item.Handle);
                }
            }
        }
        catch
        {
            // Treat as "no DDC/CI" for this monitor.
        }

        return result;
    }
}
