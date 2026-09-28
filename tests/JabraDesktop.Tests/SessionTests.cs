using JabraDesktop.Core;
namespace JabraDesktop.Tests;
public class SessionTests
{
    [Fact] public async Task RefreshAllRetainsDistinctPeerGroupsForTwoDongles()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        backend.EmitDevices(new("a","Link 370",true),new("b","Link 380",true));
        backend.PeersByDongle["a"]=[new("p1","Speaker",LinkState.Connected)];
        backend.PeersByDongle["b"]=[new("p2","Headset",LinkState.Disconnected)];
        await session.RefreshAllAsync();
        Assert.Equal(new[]{"p1"},session.PeerSnapshots.Single(x=>x.DongleId=="a").Peers.Select(x=>x.Id));
        Assert.Equal(new[]{"p2"},session.PeerSnapshots.Single(x=>x.DongleId=="b").Peers.Select(x=>x.Id));
    }
    [Fact] public async Task SamePeerOnTwoDonglesRemainsDistinct()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        backend.EmitDevices(new("a","Link",true),new("b","Link",true));
        backend.PeersByDongle["a"]=[new("same","Device A",LinkState.Connected)];
        backend.PeersByDongle["b"]=[new("same","Device B",LinkState.Disconnected)];
        await session.RefreshAllAsync();
        Assert.Equal(2,session.PeerSnapshots.Count);
        Assert.All(session.PeerSnapshots,x=>Assert.Equal("same",Assert.Single(x.Peers).Id));
    }
    [Fact] public async Task ConcurrentRefreshAllCoalescesPerDongle()
    {
        var backend=new FakeBackend { ReadCompletion=new() }; var session=new DeviceSession(backend);
        backend.EmitDevices(new("a","Link",true),new("b","Link",true));
        var first=session.RefreshAllAsync();
        await backend.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second=session.RefreshAllAsync();
        backend.ReadCompletion.SetResult();
        await Task.WhenAll(first,second);
        Assert.Equal(1,backend.PeerReads.Count(x=>x=="a"));
        Assert.Equal(1,backend.PeerReads.Count(x=>x=="b"));
    }
    [Fact] public async Task FailedDongleRefreshKeepsLastSnapshotAndOtherDongleUsable()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        backend.EmitDevices(new("a","Link",true),new("b","Link",true));
        backend.PeersByDongle["a"]=[new("pa","Speaker",LinkState.Connected)];
        backend.PeersByDongle["b"]=[new("pb","Headset",LinkState.Connected)];
        await session.RefreshAllAsync();
        backend.PeerErrors["a"]=new IOException("failure");
        backend.PeersByDongle["b"]=[new("pb2","Headset 2",LinkState.Disconnected)];
        await session.RefreshAllAsync();
        Assert.Equal("pa",Assert.Single(session.PeerSnapshots.Single(x=>x.DongleId=="a").Peers).Id);
        Assert.NotNull(session.PeerSnapshots.Single(x=>x.DongleId=="a").Error);
        Assert.Equal("pb2",Assert.Single(session.PeerSnapshots.Single(x=>x.DongleId=="b").Peers).Id);
    }
    [Fact] public async Task RemovedDongleDropsOnlyItsSnapshot()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        backend.EmitDevices(new("a","Link",true),new("b","Link",true));
        await session.RefreshAllAsync();
        backend.EmitDevices(new DeviceInfo("b","Link",true));
        Assert.Single(session.PeerSnapshots);
        Assert.Equal("b",session.PeerSnapshots[0].DongleId);
    }
    [Fact] public async Task PeerActionTargetsExplicitParentDongle()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        backend.EmitDevices(new("a","Link",true),new("b","Link",true));
        await session.RunAsync("b","peer-b",DeviceAction.Disconnect);
        Assert.Contains(("b","peer-b",DeviceAction.Disconnect),backend.Calls);
    }
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
    [Fact] public void SameDeviceModelAndUsbIdsRemainIndependentlySelectable()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        var first=new DeviceInfo("first","Link 380",true,Role:DeviceRole.Dongle,VendorId:2830,ProductId:9415);
        var second=new DeviceInfo("second","Link 380",true,Role:DeviceRole.Dongle,VendorId:2830,ProductId:9415);
        backend.EmitDevices(first,second);
        Assert.Equal(2,session.Devices.Count);
        session.Select("second");
        Assert.Equal("second",session.SelectedId);
    }
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
    [Fact] public async Task CancelScanWhileQueuedBehindRefreshPreventsStartingIt()
    {
        var (b,s)=Setup(); b.ReadCompletion=new();
        var refresh=s.RefreshAsync();
        await b.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var scan=s.ScanAsync();

        s.CancelScan();
        await scan.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0,b.ScanStarted);
        b.ReadCompletion.SetResult();
        await refresh;

        Assert.Equal(0,b.ScanStarted);
        Assert.False(s.IsBusy);
    }
    [Fact] public async Task CancelActiveScanReleasesDeviceSession()
    {
        var (b,s)=Setup();
        var scan=s.ScanAsync();
        Assert.Equal(1,b.ScanStarted);

        s.CancelScan();
        await scan.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(s.IsBusy);
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
    [Fact] public async Task QueuedDisconnectEmitsDiagnosticShowingWhatItWaitsFor()
    {
        var backend=new FakeBackend();
        var diagnostics=new List<string>();
        var session=new DeviceSession(backend,diagnostics.Add);
        backend.EmitDevices(new DeviceInfo("a","Link",true));
        session.Select("a");
        backend.ReadCompletion=new();

        var refresh=session.RefreshAsync();
        await backend.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var disconnect=session.RunAsync("p",DeviceAction.Disconnect);

        Assert.Contains(diagnostics,message=>message.Contains("operation=disconnect",StringComparison.Ordinal) && message.Contains("waiting-for-dongle-lock",StringComparison.Ordinal));
        backend.ReadCompletion.SetResult();
        await Task.WhenAll(refresh,disconnect);
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
    [Fact] public async Task RefreshPropertiesCanTargetAHeadsetWithoutPairing()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        backend.EmitDevices(new DeviceInfo("headset","Evolve 75 SE",false,CanRefreshProperties:true));

        await session.RefreshDevicePropertiesAsync("headset");

        Assert.Equal(["headset"],backend.PropertyRefreshCalls);
    }
    [Fact] public async Task RefreshPropertiesForRemovedDeviceDoesNothing()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        backend.EmitDevices(new DeviceInfo("headset","Evolve 75 SE",false,CanRefreshProperties:true));
        backend.EmitDevices();

        await session.RefreshDevicePropertiesAsync("headset");

        Assert.Empty(backend.PropertyRefreshCalls);
    }
    [Fact] public async Task PropertyRefreshDoesNotSerializeBluetoothActions()
    {
        var (backend,session)=Setup();
        backend.PropertyRefreshCompletion=new();

        var propertyRefresh=session.RefreshDevicePropertiesAsync("a");
        await session.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(propertyRefresh.IsCompleted);
        backend.PropertyRefreshCompletion.SetResult();
        await propertyRefresh;
    }
}
