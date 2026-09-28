using JabraDesktop.App;
using JabraDesktop.App.ViewModels;
using JabraDesktop.Core;

namespace JabraDesktop.Tests;

public class MultiDongleRegressionTests
{
    static async Task<(FakeBackend Backend, DeviceSession Session, MainViewModel Vm)> Setup()
    {
        var backend=new FakeBackend();
        var session=new DeviceSession(backend);
        var vm=new MainViewModel(session, action=>action());
        backend.PeersByDongle["a"]=[new("peer","Speaker",LinkState.Connected,new(true,65,true,"speaker-fw"))];
        backend.EmitDevices(new DeviceInfo("a","Link 370",true,Properties:new(false,null,true,"dongle-fw",true)));
        await session.RefreshAllAsync();
        return (backend,session,vm);
    }

    [Fact] public async Task PeerPropertyRefreshTargetsItsParentAndDoesNotRefreshDongleFirmware()
    {
        var (backend,session,vm)=await Setup();
        backend.PeersByDongle["a"]=[new("peer","Speaker",LinkState.Connected,new(true,65,true,"speaker-fw",true),"sdk-speaker")];
        await session.RefreshAllAsync(); vm.Dongles[0].Peers[0].Select();
        await vm.RefreshPropertiesCommand.ExecuteAsync(null);
        Assert.Contains(("a","peer"),backend.PeerPropertyRefreshCalls);
        Assert.Empty(backend.PropertyRefreshCalls);
    }
    [Fact] public async Task MappedSdkEndpointDoesNotAppearTwiceInNavigation()
    {
        var (backend,session,vm)=await Setup();
        backend.PeersByDongle["a"]=[new("peer","Speaker",LinkState.Connected,new(true,65,true,"fw"),"sdk-speaker")];
        backend.EmitDevices(new DeviceInfo("a","Link",true),new DeviceInfo("sdk-speaker","Speaker",false));
        await session.RefreshAllAsync();
        Assert.Single(vm.Dongles[0].Peers); Assert.Empty(vm.StandaloneDevices);
    }
    [Fact] public async Task SelectedEndpointUpdatesInPlaceAndRemainsSelected()
    {
        var (backend,session,vm)=await Setup();
        var row=vm.Dongles[0].Peers[0]; row.Select();
        backend.PeersByDongle["a"]=[row.Peer with {State=LinkState.Disconnected}];
        await session.RefreshAllAsync();
        Assert.Same(row,vm.Dongles[0].Peers[0]);
        Assert.Same(row,vm.SelectedPeer);
        Assert.False(vm.SelectedPeer!.Connected);
        Assert.Equal(vm.Texts[UiText.Connect],vm.SelectedPeer.ActionLabel);
    }
    [Fact] public async Task SelectingParentAfterChildReturnsToDongleView()
    {
        var (_,_,vm)=await Setup(); vm.Dongles[0].Peers[0].Select();
        vm.SelectedDevice=vm.Dongles[0].Device;
        Assert.Null(vm.SelectedPeer); Assert.True(vm.HasDongle);
    }
    [Fact] public async Task UnavailableEndpointFirmwareNeverUsesDongleFirmware()
    {
        var (backend,session,vm)=await Setup();
        backend.PeersByDongle["a"]=[new("peer","Speaker",LinkState.Connected,new(true,null,true))];
        await session.RefreshAllAsync(); vm.Dongles[0].Peers[0].Select();
        Assert.Equal(vm.Texts[UiText.FirmwareUnavailable],vm.FirmwareText);
    }
    [Fact] public async Task RemovingDongleClearsItsEndpointSelection()
    {
        var (backend,_,vm)=await Setup(); vm.Dongles[0].Peers[0].Select();
        backend.EmitDevices();
        Assert.Null(vm.SelectedPeer); Assert.Empty(vm.Dongles);
    }
    [Fact] public async Task GroupedPeerNotifiesWhenLanguageChanges()
    {
        var (_,_,vm)=await Setup(); var row=vm.Dongles[0].Peers[0];
        var changes=new List<string?>(); row.PropertyChanged+=(_,e)=>changes.Add(e.PropertyName);
        vm.Texts.SetLanguage(UiLanguage.English);
        Assert.Contains(nameof(PeerRow.Status),changes);
    }
    [Fact] public async Task ExplicitActionReportsFailureAndRefreshesWithoutThrowing()
    {
        var (backend,session,_)=await Setup(); backend.NextError=new IOException("USB");
        await session.RunAsync("a","peer",DeviceAction.Disconnect);
        Assert.NotNull(session.PeerSnapshots.Single().Error);
        Assert.False(session.IsBusy);
    }
    [Fact] public async Task ExplicitActionMarksOnlyItsDongleBusy()
    {
        var (backend,session,_)=await Setup(); backend.OperationCompletion=new();
        var action=session.RunAsync("a","peer",DeviceAction.Disconnect);
        Assert.True(session.IsBusy);
        backend.OperationCompletion.SetResult(); await action;
        Assert.False(session.IsBusy);
    }
    [Fact] public async Task RemovedAndReaddedDongleIgnoresOldInventoryRead()
    {
        var (backend,session,vm)=await Setup(); vm.Dispose(); backend.ReadCompletion=new();
        var read=session.RefreshAllAsync();
        backend.EmitDevices(); backend.EmitDevices(new DeviceInfo("a","Link",true));
        backend.ReadCompletion.SetResult(); await read;
        Assert.Empty(session.PeerSnapshots.Single().Peers);
    }
}
