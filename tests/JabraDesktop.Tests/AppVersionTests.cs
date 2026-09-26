using JabraDesktop.App;

namespace JabraDesktop.Tests;

public sealed class AppVersionTests
{
    [Fact]
    public void DisplayUsesAssemblyVersion()
    {
        Assert.Equal(typeof(JabraDesktop.App.App).Assembly.GetName().Version?.ToString(3), AppVersion.Display);
    }
}
