namespace PowerAwake.Core.Models;

public static class SettingsValidation
{
    public const uint MaximumAwakeIntervalSeconds = 7 * 24 * 60 * 60;
    public const uint MinimumAwakeIntervalSeconds = 60;

    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        var errors = new List<string>();

        if (!settings.ApplyToAc && !settings.ApplyToDc)
        {
            errors.Add("请至少选择应用于 AC 或 DC。" );
        }

        if (settings.DisplayDimSeconds != 0 && settings.DisplayOffSeconds != 0 &&
            settings.DisplayOffSeconds <= settings.DisplayDimSeconds)
        {
            errors.Add("关闭屏幕时间必须晚于屏幕变暗时间。");
        }

        if (settings.DimBrightnessPercent > 100)
        {
            errors.Add("变暗亮度必须在 0% 到 100% 之间。");
        }

        return errors;
    }

    public static bool IsValidAwakeInterval(uint seconds) =>
        seconds >= MinimumAwakeIntervalSeconds && seconds <= MaximumAwakeIntervalSeconds;
}