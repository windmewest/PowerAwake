using PowerAwake.Core.Models;
using PowerAwake.Core.Services;

namespace PowerAwake.Core.Tests;

public sealed class AwakeControllerTests
{
    [Fact]
    public void ExternalSchemeRecoveryRestoresOldSchemeWithoutActivatingIt()
    {
        var oldScheme = Guid.NewGuid();
        var newScheme = Guid.NewGuid();
        var original = CreateValues(900);
        var policy = new FakePowerPolicy(oldScheme, original);
        policy.SetScheme(newScheme, CreateValues(1200));
        var recoveryPath = Path.Combine(Path.GetTempPath(), $"powerawake-{Guid.NewGuid():N}", "recovery.json");
        var controller = new AwakeController(policy, new JsonFileStore<PowerSnapshot>(recoveryPath));

        policy.SetActive(oldScheme);
        controller.StartIndefinitely(new AppSettings());
        policy.SetActive(newScheme);

        Assert.True(controller.DetectExternalSchemeChange());
        controller.RecoverExternalScheme();

        Assert.Equal(newScheme, policy.GetActiveScheme());
        Assert.Equal(original, policy.ReadValues(oldScheme));
        Assert.IsType<AwakeState.Off>(controller.State);
        Assert.False(File.Exists(recoveryPath));
    }

    [Fact]
    public void NormalSettingsCanTargetOnlyAcPower()
    {
        var scheme = Guid.NewGuid();
        var original = CreateValues(900) with { SleepDc = 1200, HibernateDc = 3600 };
        var policy = new FakePowerPolicy(scheme, original);
        var path = Path.Combine(Path.GetTempPath(), $"powerawake-{Guid.NewGuid():N}", "recovery.json");
        var controller = new AwakeController(policy, new JsonFileStore<PowerSnapshot>(path));

        controller.ApplyNormalSettings(new AppSettings { NormalSleepSeconds = 300, NormalHibernateSeconds = 600, ApplyToAc = true, ApplyToDc = false });

        var applied = policy.ReadValues(scheme);
        Assert.Equal(300u, applied.SleepAc);
        Assert.Equal(600u, applied.HibernateAc);
        Assert.Equal(original.SleepDc, applied.SleepDc);
        Assert.Equal(original.HibernateDc, applied.HibernateDc);
    }

    private static PowerValues CreateValues(uint timeout) => new()
    {
        SleepAc = timeout,
        SleepDc = timeout,
        HibernateAc = 0,
        HibernateDc = 0,
        DimAc = 120,
        DimDc = 120,
        DisplayOffAc = 180,
        DisplayOffDc = 180,
        DimBrightnessAc = 50,
        DimBrightnessDc = 50
    };

    private sealed class FakePowerPolicy : IPowerPolicy
    {
        private readonly Dictionary<Guid, PowerValues> schemes = new();
        private Guid activeScheme;

        public FakePowerPolicy(Guid scheme, PowerValues values)
        {
            activeScheme = scheme;
            schemes[scheme] = values;
        }

        public Guid GetActiveScheme() => activeScheme;

        public PowerValues ReadValues(Guid schemeGuid) => schemes[schemeGuid];

        public void WriteValues(Guid schemeGuid, PowerValues values, bool activate = true)
        {
            schemes[schemeGuid] = values;
            if (activate)
            {
                activeScheme = schemeGuid;
            }
        }

        public void SetScheme(Guid schemeGuid, PowerValues values) => schemes[schemeGuid] = values;

        public void SetActive(Guid schemeGuid) => activeScheme = schemeGuid;
    }
}