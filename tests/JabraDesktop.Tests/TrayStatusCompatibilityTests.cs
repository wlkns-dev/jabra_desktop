using System.Reflection;
using JabraDesktop.App;

namespace JabraDesktop.Tests;

public class TrayStatusCompatibilityTests
{
    [Fact]
    public void StatusNotifierStatusIsActiveRatherThanTooltipText()
    {
        var helper = typeof(JabraDesktop.App.App).Assembly.GetType("JabraDesktop.App.TrayStatusCompatibility")!;
        var method = helper.GetMethod("SetStatusActive", BindingFlags.Static | BindingFlags.NonPublic)!;
        var item = new FakeStatusNotifierItem { Status = "Jabra Desktop" };

        var result = (bool)method.Invoke(null, [item])!;

        Assert.True(result);
        Assert.Equal("Active", item.Status);
        Assert.Equal(1, item.InvalidationCount);
    }

    sealed class FakeStatusNotifierItem
    {
        public string Status { get; set; } = "";
        public int InvalidationCount { get; private set; }
        public void InvalidateAll() => InvalidationCount++;
    }
}
