using JabraDesktop.Core;
namespace JabraDesktop.Tests;
public class SessionTests
{
    static (FakeBackend b, DeviceSession s) Setup()
    {
        var b = new FakeBackend { Peers = [new("p", "Evolve", LinkState.Disconnected)] };
        var s = new DeviceSession(b);
        b.EmitDevices(new DeviceInfo("a", "Link", true), new("b", "Link", true));
        s.Select("a");
        return (b,s);
    }
    [Fact] public void SameNamesRemainSeparate()
    { var (b,s)=Setup(); Assert.Equal(2,s.Devices.Count); s.Select("b"); Assert.Equal("b",s.SelectedId); }
    [Fact] public async Task ScanDeduplicatesById()
    {
        var (b,s)=Setup(); b.Scan.Writer.TryWrite(new("p","Name",LinkState.Unknown));
        b.Scan.Writer.TryWrite(new("p","Better",LinkState.Unknown)); b.Scan.Writer.Complete();
        await s.ScanAsync(); Assert.Single(s.Results); Assert.Equal("Better",s.Results[0].Name); Assert.False(s.IsBusy);
    }
    [Fact] public async Task RemovalInvalidatesPendingRead()
    {
        var (b,s)=Setup(); b.ReadCompletion=new(); var task=s.RefreshAsync();
        b.EmitDevices(); b.EmitDevices(new DeviceInfo("a","Link",true)); s.Select("a");
        b.ReadCompletion.SetResult(); await task; Assert.Empty(s.Peers);
    }
    [Fact] public async Task RemovalCancelsScanAndClearsResults()
    {
        var (b,s)=Setup(); var task=s.ScanAsync(); Assert.True(s.IsBusy);
        b.EmitDevices(); await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(s.IsBusy); Assert.Empty(s.Results); Assert.Null(s.SelectedId);
    }
    [Fact] public async Task FailedOperationDoesNotClaimConnection()
    {
        var (b,s)=Setup(); await s.RefreshAsync(); b.NextError=new IOException("USB");
        await s.RunAsync("p",DeviceAction.Connect); Assert.NotNull(s.Error);
        Assert.Equal(LinkState.Disconnected,Assert.Single(s.Peers).State); Assert.False(s.IsBusy);
    }
    [Fact] public async Task PendingOperationBlocksSecondEvenAfterSelectionChange()
    {
        var (b,s)=Setup(); await s.RefreshAsync(); b.OperationCompletion=new();
        var first=s.RunAsync("p",DeviceAction.Connect); s.Select("b"); s.Select("a");
        var second=s.RunAsync("p",DeviceAction.Disconnect);
        Assert.False(second.IsCompleted); Assert.Single(b.Calls);
        b.OperationCompletion.SetResult(); await Task.WhenAll(first,second);
        Assert.Single(b.Calls);
    }
    [Fact] public async Task SelectedDongleReceivesAction()
    {
        var (b,s)=Setup(); s.Select("b"); await s.RefreshAsync();
        await s.RunAsync("p",DeviceAction.Connect); Assert.Equal("b",Assert.Single(b.Calls).Dongle);
    }
    [Fact] public async Task UnknownPeerNeverReachesHardware()
    { var(b,s)=Setup(); await s.RunAsync("missing",DeviceAction.Connect); Assert.Empty(b.Calls); Assert.NotNull(s.Error); }
    [Fact] public async Task StartupErrorIsVisibleAndRetryWorks()
    {
        var (b,s)=Setup(); b.NextError=new UnauthorizedAccessException(); await s.StartAsync();
        Assert.NotNull(s.Error); b.NextError=null; await s.StartAsync(); Assert.Null(s.Error);
    }
    [Fact] public async Task NonDongleCannotScan()
    {
        var(b,s)=Setup(); b.EmitDevices(new DeviceInfo("usb","USB Headset",false)); s.Select("usb");
        await s.ScanAsync(); Assert.NotNull(s.Error); Assert.False(s.IsBusy);
    }
    [Fact] public async Task CancellationDoesNotReleaseUnfinishedSdkOperation()
    {
        var(b,s)=Setup(); await s.RefreshAsync(); b.OperationCompletion=new();
        using var c=new CancellationTokenSource(); var t=s.RunAsync("p",DeviceAction.Connect,c.Token);
        c.Cancel(); Assert.True(s.IsBusy); b.OperationCompletion.SetResult(); await t; Assert.False(s.IsBusy);
    }
}
