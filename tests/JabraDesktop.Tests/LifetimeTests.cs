using JabraDesktop.Jabra;
using JabraDesktop.Core;
namespace JabraDesktop.Tests;
public class LifetimeTests
{
    [Fact] public async Task TimeoutReportsButRetainsLeaseUntilSdkSettles()
    {
        var sdk=new TaskCompletionSource(); var reported=new TaskCompletionSource();
        var operation=SdkLifetime.AwaitCompletion(sdk.Task,TimeSpan.FromMilliseconds(10),()=>reported.TrySetResult());
        await Task.WhenAny(reported.Task,operation).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(reported.Task.IsCompletedSuccessfully); Assert.False(operation.IsCompleted);
        sdk.SetResult(); await operation;
    }
    [Fact] public async Task FailedInitializationCanBeRetried()
    {
        var init=new RetryableInitialization<int>();
        await Assert.ThrowsAsync<IOException>(()=>init.Run(()=>Task.FromException<int>(new IOException())));
        Assert.Equal(42,await init.Run(()=>Task.FromResult(42)));
    }
    [Fact] public async Task ConcurrentRetrySharesUnfinishedInitialization()
    {
        var init=new RetryableInitialization<int>(); var pending=new TaskCompletionSource<int>();
        var first=init.Run(()=>pending.Task); var second=init.Run(()=>Task.FromResult(99));
        Assert.Same(first,second); pending.SetResult(42); Assert.Equal(42,await second);
    }
    [Fact] public async Task SnapshotsCannotOvertakeEachOther()
    {
        var publisher=new SnapshotPublisher<int>(); var firstEntered=new TaskCompletionSource();
        using var release=new ManualResetEventSlim(); var seen=new List<int>();
        var first=Task.Run(()=>publisher.Publish(()=>1,n=> { firstEntered.SetResult(); release.Wait(TimeSpan.FromSeconds(3)); lock(seen) seen.Add(n); }));
        await firstEntered.Task;
        var second=Task.Run(()=>publisher.Publish(()=>2,n=> { lock(seen) seen.Add(n); }));
        await Task.WhenAny(second,Task.Delay(50)); release.Set(); await Task.WhenAll(first,second);
        Assert.Equal(new[]{1,2},seen);
    }
    [Fact] public async Task PollingDoesNotDismissActionFailure()
    {
        var b=new FakeBackend{Peers=[new("p","Headset",LinkState.Disconnected)]};
        var s=new DeviceSession(b); b.EmitDevices(new DeviceInfo("d","Dongle",true)); s.Select("d");
        await s.RefreshAsync(); b.NextError=new IOException("Aktion fehlgeschlagen");
        await s.RunAsync("p",DeviceAction.Connect); b.NextError=null;
        await s.RefreshAsync(); Assert.Equal("Aktion fehlgeschlagen",s.Error);
    }
}
