using PowerAwake.Core.Models;

namespace PowerAwake.Core.Tests;

public class SettingsValidationTests
{
    [Fact]
    public void DefaultSettingsAreValid()
    {
        Assert.Empty(SettingsValidation.Validate(new AppSettings()));
    }

    [Fact]
    public void DisplayOffMustBeLaterThanDim()
    {
        var settings = new AppSettings { DisplayDimSeconds = 300, DisplayOffSeconds = 300 };

        var errors = SettingsValidation.Validate(settings);

        Assert.Contains("关闭屏幕时间必须晚于屏幕变暗时间。", errors);
    }

    [Fact]
    public void EitherDisplayTimeoutMayBeNever()
    {
        Assert.Empty(SettingsValidation.Validate(new AppSettings { DisplayDimSeconds = 0, DisplayOffSeconds = 60 }));
        Assert.Empty(SettingsValidation.Validate(new AppSettings { DisplayDimSeconds = 60, DisplayOffSeconds = 0 }));
    }

    [Fact]
    public void AwakeIntervalUsesInclusiveOneMinuteToSevenDaysRange()
    {
        Assert.True(SettingsValidation.IsValidAwakeInterval(SettingsValidation.MinimumAwakeIntervalSeconds));
        Assert.True(SettingsValidation.IsValidAwakeInterval(SettingsValidation.MaximumAwakeIntervalSeconds));
        Assert.False(SettingsValidation.IsValidAwakeInterval(59));
        Assert.False(SettingsValidation.IsValidAwakeInterval(SettingsValidation.MaximumAwakeIntervalSeconds + 1));
    }

    [Fact]
    public void BrightnessAboveOneHundredIsRejected()
    {
        var errors = SettingsValidation.Validate(new AppSettings { DimBrightnessPercent = 101 });

        Assert.Contains("变暗亮度必须在 0% 到 100% 之间。", errors);
    }

    [Fact]
    public void AtLeastOnePowerTargetMustBeSelected()
    {
        var errors = SettingsValidation.Validate(new AppSettings { ApplyToAc = false, ApplyToDc = false });

        Assert.Contains("请至少选择应用于 AC 或 DC。", errors);
    }
}