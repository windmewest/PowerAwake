namespace PowerAwake.Core.Models;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public uint NormalSleepSeconds { get; init; } = 900;
    public uint NormalHibernateSeconds { get; init; }
    public uint DisplayDimSeconds { get; init; } = 120;
    public uint DisplayOffSeconds { get; init; } = 180;
    public byte DimBrightnessPercent { get; init; } = 50;
    public bool ApplyToAc { get; init; } = true;
    public bool ApplyToDc { get; init; } = true;
    public bool KeepScreenOn { get; init; }
    public string LanguageCode { get; init; } = "system";
    public bool StartWithWindows { get; init; } = true;
    public bool ShowStartupNotification { get; init; }
}