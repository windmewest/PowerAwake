using Microsoft.Win32;

namespace PowerAwake.App.Ui;

/// <summary>Light/dark palette following the Windows "app mode" setting (Windows 11 Fluent colours).</summary>
internal sealed class Palette
{
    public bool IsDark { get; init; }
    public Color WindowBack { get; init; }
    public Color CardBack { get; init; }
    public Color CardBorder { get; init; }
    public Color Divider { get; init; }
    public Color Text { get; init; }
    public Color SubText { get; init; }
    public Color Accent { get; init; }
    public Color AccentText { get; init; }
    public Color ControlBack { get; init; }
    public Color ControlHover { get; init; }
    public Color ControlBorder { get; init; }
    public Color MenuBack { get; init; }
    public Color MenuBorder { get; init; }
    public Color MenuHover { get; init; }
    public Color TrackOff { get; init; }
    public Color Danger { get; init; }
    public Color DangerBack { get; init; }

    public static readonly Palette Light = new()
    {
        IsDark = false,
        WindowBack = Color.FromArgb(243, 243, 243),
        CardBack = Color.FromArgb(255, 255, 255),
        CardBorder = Color.FromArgb(229, 229, 229),
        Divider = Color.FromArgb(235, 235, 235),
        Text = Color.FromArgb(27, 27, 27),
        SubText = Color.FromArgb(96, 96, 96),
        Accent = Color.FromArgb(0, 103, 192),
        AccentText = Color.White,
        ControlBack = Color.FromArgb(251, 251, 251),
        ControlHover = Color.FromArgb(246, 246, 246),
        ControlBorder = Color.FromArgb(214, 214, 214),
        MenuBack = Color.FromArgb(249, 249, 249),
        MenuBorder = Color.FromArgb(222, 222, 222),
        MenuHover = Color.FromArgb(234, 234, 234),
        TrackOff = Color.FromArgb(110, 110, 110),
        Danger = Color.FromArgb(196, 43, 28),
        DangerBack = Color.FromArgb(253, 231, 233)
    };

    public static readonly Palette Dark = new()
    {
        IsDark = true,
        WindowBack = Color.FromArgb(32, 32, 32),
        CardBack = Color.FromArgb(43, 43, 43),
        CardBorder = Color.FromArgb(58, 58, 58),
        Divider = Color.FromArgb(56, 56, 56),
        Text = Color.FromArgb(255, 255, 255),
        SubText = Color.FromArgb(180, 180, 180),
        Accent = Color.FromArgb(76, 194, 255),
        AccentText = Color.FromArgb(0, 0, 0),
        ControlBack = Color.FromArgb(52, 52, 52),
        ControlHover = Color.FromArgb(62, 62, 62),
        ControlBorder = Color.FromArgb(78, 78, 78),
        MenuBack = Color.FromArgb(44, 44, 44),
        MenuBorder = Color.FromArgb(70, 70, 70),
        MenuHover = Color.FromArgb(60, 60, 60),
        TrackOff = Color.FromArgb(190, 190, 190),
        Danger = Color.FromArgb(255, 153, 164),
        DangerBack = Color.FromArgb(68, 39, 38)
    };
}

internal static class Theme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Raised on the UI thread when the user switches Windows light/dark mode.</summary>
    public static event Action? Changed;

    public static bool AppsUseDark => IsDarkFlag("AppsUseLightTheme");

    public static bool TaskbarUsesDark => IsDarkFlag("SystemUsesLightTheme");

    public static Palette Current => AppsUseDark ? Palette.Dark : Palette.Light;

    public static void Watch()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
            {
                Changed?.Invoke();
            }
        };
    }

    // The registry value is 1 for light and 0 for dark; a missing key defaults to light.
    private static bool IsDarkFlag(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
