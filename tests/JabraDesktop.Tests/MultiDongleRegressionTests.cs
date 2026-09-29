using JabraDesktop.App;
using JabraDesktop.App.ViewModels;
using JabraDesktop.Core;

namespace JabraDesktop.Tests;

public class MultiDongleRegressionTests
{
    [Fact] public void TopLevelTelemetryUpdatesWithoutShowingDongleBattery()
    {
        var texts=new LocalizationService(UiLanguage.English);
        var device=new DeviceInfo("dongle","Link",true,Role:DeviceRole.Dongle,
            Properties:new(false,null,true,"1.0.0"));
        var row=new DeviceListItemViewModel(device,texts);
        Assert.Equal("1.0.0",row.TelemetrySummary);
        var changes=new List<string?>(); row.PropertyChanged+=(_,e)=>changes.Add(e.PropertyName);
        row.Update(device with { Properties=new(false,null,true,"2.0.0") });
        Assert.Equal("2.0.0",row.TelemetrySummary);
        Assert.Contains(nameof(DeviceListItemViewModel.TelemetrySummary),changes);
        var headset=new DeviceListItemViewModel(new("headset","Headset",false,
            Properties:new(true,75,true,"3.0.0")),texts);
        Assert.Equal("75 % · 3.0.0",headset.TelemetrySummary);
    }
    [Fact] public async Task SavedDeviceSelectionSurvivesPeriodicRefresh()
    {
        var (_,session,vm)=await Setup();
        vm.Peers.Single().Select();
        Assert.NotNull(vm.SelectedPeer);
        await session.RefreshAllAsync();
        Assert.Same(vm.Dongles[0].Peers[0],vm.SelectedPeer);
        Assert.True(vm.IsEndpointSelected);
    }
    [Fact] public async Task SuccessfulExplicitRetryClearsItsFailureBanner()
    {
        var (backend,session,vm)=await Setup();
        backend.NextError=new IOException("USB failure");
        await session.RunAsync("a","peer",DeviceAction.Disconnect);
        Assert.True(vm.HasError);
        backend.NextError=null;
        await session.RunAsync("a","peer",DeviceAction.Disconnect);
        Assert.False(vm.HasError);
    }
    [Fact] public async Task DongleErrorDetailUsesSelectedLanguage()
    {
        var (backend,session,vm)=await Setup();
        backend.PeerErrors["a"]=new TimeoutException();
        await session.RefreshAllAsync();
        vm.Texts.SetLanguage(UiLanguage.English);
        Assert.Contains(vm.Texts[UiText.DeviceTimeout],vm.Dongles[0].ErrorText);
    }
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
    [Fact] public void DongleChildIsNotShownAsStandaloneWhilePairingListLoads()
    {
        var backend=new FakeBackend();
        var session=new DeviceSession(backend);
        var vm=new MainViewModel(session,action=>action());
        backend.EmitDevices(
            new DeviceInfo("link","Link 370",true,Role:DeviceRole.Dongle),
            new DeviceInfo("child","Evolve 75",false,Role:DeviceRole.Headset,ParentDongleId:"link"),
            new DeviceInfo("wired","USB Headset",false,Role:DeviceRole.Headset));
        Assert.Single(vm.Dongles);
        Assert.Empty(vm.Dongles[0].Peers);
        Assert.Equal("wired",Assert.Single(vm.StandaloneDevices).Device.Id);
    }
    [Fact] public async Task SelectedConnectedEndpointUpdatesInPlaceAndRemainsSelected()
    {
        var (backend,session,vm)=await Setup();
        var row=vm.Dongles[0].Peers[0]; row.Select();
        backend.PeersByDongle["a"]=[row.Peer with {Properties=new(true,64,true,"speaker-fw") }];
        await session.RefreshAllAsync();
        Assert.Same(row,vm.Dongles[0].Peers[0]);
        Assert.Same(row,vm.SelectedPeer);
        Assert.True(vm.SelectedPeer!.Connected);
        Assert.Contains("64 %",vm.SelectedPeer.TelemetrySummary);
    }
    [Fact] public async Task SidebarShowsOnlyConnectedPeersButPairingListRetainsAll()
    {
        var (backend,session,vm)=await Setup();
        backend.PeersByDongle["a"]=[
            new("peer","Speaker",LinkState.Connected),
            new("saved","Saved headset",LinkState.Disconnected),
            new("unknown","Unknown headset",LinkState.Unknown)
        ];
        await session.RefreshAllAsync();
        Assert.Equal(["peer"],vm.Dongles[0].Peers.Select(p=>p.Peer.Id));
        Assert.Equal(3,vm.Peers.Count);
        vm.Dongles[0].Peers[0].Select();
        backend.PeersByDongle["a"]=[
            new("peer","Speaker",LinkState.Disconnected),
            new("saved","Saved headset",LinkState.Connected),
            new("unknown","Unknown headset",LinkState.Unknown)
        ];
        await session.RefreshAllAsync();
        Assert.Equal(["saved"],vm.Dongles[0].Peers.Select(p=>p.Peer.Id));
        Assert.Null(vm.SelectedPeer);
        Assert.Equal(3,vm.Peers.Count);
        Assert.Contains(vm.Peers,p=>p.Peer.Id=="peer" && p.ActionLabel==vm.Texts[UiText.Connect]);
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
