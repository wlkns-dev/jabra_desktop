using JabraDesktop.Core;

namespace JabraDesktop.Tests;

public class BluetoothRenameTests
{
    [Fact] public async Task UnconfirmedWriteReportsErrorAndRefreshesActualPeerState()
    {
        var backend=new FakeBackend { RenameErrorAfterWrite=new InvalidOperationException("device-name-unconfirmed") };
        var session=new DeviceSession(backend);
        backend.PeersByDongle["a"]=[new("peer-a","Old name",LinkState.Connected,CanRenameBluetooth:true)];
        backend.PeersByDongle["b"]=[new("peer-b","Other device",LinkState.Connected)];
        backend.EmitDevices(new DeviceInfo("a","Link 370",true),new DeviceInfo("b","Link 380",true));
        await session.RefreshAllAsync();

        Assert.False(await session.RenamePeerAsync("a","peer-a","New name"));
        Assert.Equal("New name",session.PeerSnapshots.Single(s=>s.DongleId=="a").Peers.Single().Name);
        Assert.Equal("device-name-unconfirmed",session.PeerSnapshots.Single(s=>s.DongleId=="a").Error);
        Assert.Null(session.PeerSnapshots.Single(s=>s.DongleId=="b").Error);
    }
    [Fact] public async Task RenameTargetsConnectedPeerOnItsOwnDongle()
    {
        var backend=new FakeBackend();
        var session=new DeviceSession(backend);
        backend.PeersByDongle["a"]=[new("peer-a","Evolve 75",LinkState.Connected,CanRenameBluetooth:true)];
        backend.PeersByDongle["b"]=[new("peer-b","Evolve 75",LinkState.Connected,CanRenameBluetooth:true)];
        backend.EmitDevices(new DeviceInfo("a","Link 370",true),new DeviceInfo("b","Link 380",true));
        await session.RefreshAllAsync();

        Assert.True(await session.RenamePeerAsync("b","peer-b","  Office headset  "));
        Assert.Equal([("b","peer-b","Office headset")],backend.RenameCalls);
        Assert.Equal("Office headset",session.PeerSnapshots.Single(s=>s.DongleId=="b").Peers.Single().Name);
        Assert.Equal("Evolve 75",session.PeerSnapshots.Single(s=>s.DongleId=="a").Peers.Single().Name);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("Line\nBreak")]
    public async Task InvalidNameNeverReachesDevice(string name)
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        backend.PeersByDongle["a"]=[new("peer","Evolve 75",LinkState.Connected,CanRenameBluetooth:true)];
        backend.EmitDevices(new DeviceInfo("a","Link",true)); await session.RefreshAllAsync();

        Assert.False(await session.RenamePeerAsync("a","peer",name));
        Assert.Empty(backend.RenameCalls);
    }

    [Fact] public async Task DisconnectedOrUnsupportedPeerCannotBeRenamed()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend);
        backend.PeersByDongle["a"]=[new("disconnected","Evolve 75",LinkState.Disconnected,CanRenameBluetooth:true),
            new("unsupported","Other",LinkState.Connected)];
        backend.EmitDevices(new DeviceInfo("a","Link",true)); await session.RefreshAllAsync();

        Assert.False(await session.RenamePeerAsync("a","disconnected","New name"));
        Assert.False(await session.RenamePeerAsync("a","unsupported","New name"));
        Assert.Empty(backend.RenameCalls);
    }
}
