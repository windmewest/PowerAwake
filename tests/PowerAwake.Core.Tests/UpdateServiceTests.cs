using PowerAwake.App;

namespace PowerAwake.Core.Tests;

public sealed class UpdateServiceTests
{
    [Fact]
    public void CurrentVersion_CanBeReadWhenUpdateServiceInitializes()
    {
        var version = UpdateService.CurrentVersion;

        Assert.True(version.Major >= 1);
    }
}
