using System.Runtime.InteropServices;

namespace PowerAwake.App.Ui;

internal static class NativeUi
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;
    private const int RoundCorner = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    /// <summary>Dark/light title bar (and Windows 11 caption colours) for a top-level window.</summary>
    public static void ApplyWindowTheme(IntPtr handle, Palette palette)
    {
        var dark = palette.IsDark ? 1 : 0;
        Set(handle, DwmUseImmersiveDarkMode, dark);
        Set(handle, DwmCaptionColor, ToColorRef(palette.WindowBack));
        Set(handle, DwmTextColor, ToColorRef(palette.Text));
    }

    /// <summary>Asks Windows 11 to round the corners of a popup (menus); a no-op on older systems.</summary>
    public static void RoundCorners(IntPtr handle) => Set(handle, DwmWindowCornerPreference, RoundCorner);

    /// <summary>Switches native scrollbars/edit chrome to the dark variant.</summary>
    public static void ApplyControlTheme(IntPtr handle, Palette palette)
    {
        try
        {
            SetWindowTheme(handle, palette.IsDark ? "DarkMode_Explorer" : "Explorer", null);
        }
        catch
        {
            // Theming is cosmetic only.
        }
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    private static void Set(IntPtr handle, int attribute, int value)
    {
        try
        {
            DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch
        {
            // Attribute unsupported on this Windows build.
        }
    }
}
