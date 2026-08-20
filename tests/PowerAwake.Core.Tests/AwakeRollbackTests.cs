using PowerAwake.Core.Models;
using PowerAwake.Core.Services;

namespace PowerAwake.Core.Tests;

public sealed class AwakeRollbackTests
{
    [Fact]
    public void FailedAwakeWriteRollsBackAndReturnsToOff()
    {
        var scheme = Guid.NewGuid();
        var original = new PowerValues
        {
            SleepAc = 900, SleepDc = 900, HibernateAc = 0, HibernateDc = 0,
            DimAc = 120, DimDc = 120, DisplayOffAc = 180, DisplayOffDc = 180,
            DimBrightnessAc = 50, DimBrightnessDc = 50
        };
        var policy = new FailingWritePolicy(scheme, original);
        var path = Path.Combine(Path.GetTempPath(), $"powerawake-{Guid.NewGuid():N}", "recovery.json");
        var controller = new AwakeController(policy, new JsonFileStore<PowerSnapshot>(path));

        Assert.Throws<InvalidOperationException>(() => controller.StartIndefinitely(new AppSettings()));

        Assert.IsType<AwakeState.Off>(controller.State);
        Assert.Equal(original, policy.ReadValues(scheme));
        Assert.False(File.Exists(path));
    }

    private sealed class FailingWritePolicy : IPowerPolicy
    {
        private readonly Guid scheme;
        private PowerValues values;
        private int writeCount;

        public FailingWritePolicy(Guid scheme, PowerValues values)
        {
            this.scheme = scheme;
            this.values = values;
        }

        public Guid GetActiveScheme() => scheme;
        public PowerValues ReadValues(Guid schemeGuid) => values;

        public void WriteValues(Guid schemeGuid, PowerValues values, bool activate = true)
        {
            writeCount++;
            if (writeCount == 2)
            {
                throw new InvalidOperationException("模拟写入失败");
            }

            this.values = values;
        }
    }
}