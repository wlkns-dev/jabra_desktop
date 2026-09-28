using JabraDesktop.Core;
using JabraDesktop.App.ViewModels;
namespace JabraDesktop.Tests;
public class ViewModelTests
{
    [Fact] public async Task DongleTreeContainsOnlyItsOwnPeers()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend); var vm=new MainViewModel(session,a=>a());
        backend.EmitDevices(new DeviceInfo("a","Link 370",true),new DeviceInfo("b","Link 380",true));
        backend.PeersByDongle["a"]=[new("pa","Speaker",LinkState.Connected)];
        backend.PeersByDongle["b"]=[new("pb","Headset",LinkState.Disconnected)];
        await session.RefreshAllAsync();
        Assert.Equal("pa",Assert.Single(vm.Dongles.Single(x=>x.Device.Device.Id=="a").Peers).Peer.Id);
        Assert.Equal("pb",Assert.Single(vm.Dongles.Single(x=>x.Device.Device.Id=="b").Peers).Peer.Id);
    }
    [Fact] public async Task SelectingPeerSelectsItsParentDongle()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend); var vm=new MainViewModel(session,a=>a());
        backend.EmitDevices(new DeviceInfo("dongle","Link",true));
        backend.PeersByDongle["dongle"]=[new("peer","Speaker",LinkState.Disconnected)];
        await session.RefreshAllAsync();
        vm.Dongles[0].Peers[0].Select();
        Assert.Equal("peer",vm.SelectedPeer?.Peer.Id);
        Assert.Equal("dongle",vm.SelectedDevice?.Device.Id);
        Assert.Equal("dongle",session.SelectedId);
    }
    [Fact] public async Task EndpointHidesPairingSearchAndOverview()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend); var vm=new MainViewModel(session,a=>a());
        backend.EmitDevices(new DeviceInfo("dongle","Link",true));
        backend.PeersByDongle["dongle"]=[new("peer","Speaker",LinkState.Connected)];
        await session.RefreshAllAsync();
        vm.Dongles[0].Peers[0].Select();
        Assert.False(vm.ShowSearch);
        Assert.False(vm.ShowDeviceOverview);
        Assert.False(vm.CanScan);
    }
    [Fact] public async Task DisconnectedEndpointOffersConnect()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend); var vm=new MainViewModel(session,a=>a());
        backend.EmitDevices(new DeviceInfo("dongle","Link",true));
        backend.PeersByDongle["dongle"]=[new("peer","Speaker",LinkState.Disconnected)];
        await session.RefreshAllAsync();
        var row=Assert.Single(vm.Dongles[0].Peers);
        Assert.Equal(vm.Texts[JabraDesktop.App.UiText.Connect],row.ActionLabel);
        await row.ActionCommand.ExecuteAsync(null);
        Assert.Contains(("dongle","peer",DeviceAction.Connect),backend.Calls);
    }
    [Fact] public void CapableDongleEnablesSearch()
    {
        var b=new FakeBackend(); var s=new DeviceSession(b); var vm=new MainViewModel(s,a=>a());
        Assert.False(vm.CanScan); b.EmitDevices(new DeviceInfo("a","Link",true)); Assert.True(vm.CanScan);
        b.EmitDevices(); Assert.False(vm.CanScan);
    }
    [Fact] public void UsbHeadsetNeverOffersSearch()
    {
        var b=new FakeBackend(); var vm=new MainViewModel(new(b),a=>a());
        b.EmitDevices(new DeviceInfo("usb","Headset",false)); Assert.False(vm.CanScan);
    }
    [Fact] public void DeviceRoleDoesNotGrantPairingCapability()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend); var vm=new MainViewModel(session,a=>a());
        backend.EmitDevices(new DeviceInfo("headset","Evolve 75",false,Role:DeviceRole.Headset,VendorId:2830,ProductId:1234));

        Assert.Equal("headset",session.SelectedId);
        Assert.Equal("Headset",Assert.Single(vm.Devices).RoleLabel);
        Assert.False(vm.CanScan);
    }
    [Fact] public void NewlyRecognizedDongleRefreshesPeers()
    {
        var backend=new FakeBackend { Peers=[new("peer","Evolve 75",LinkState.Disconnected)] };
        var session=new DeviceSession(backend); var vm=new MainViewModel(session,a=>a());
        backend.EmitDevices(new DeviceInfo("dongle","Link 380",false,Role:DeviceRole.Dongle));
        Assert.Empty(vm.Peers);

        backend.EmitDevices(new DeviceInfo("dongle","Link 380",true,Role:DeviceRole.Dongle));

        Assert.Equal("Evolve 75",Assert.Single(vm.Peers).Name);
    }
    [Fact] public void DuplicateDeviceRowsRemainSelectableWhenNamesAndIdsMatch()
    {
        var backend=new FakeBackend(); var session=new DeviceSession(backend); var vm=new MainViewModel(session,a=>a());
        backend.EmitDevices(
            new DeviceInfo("one","Link 380",true,Role:DeviceRole.Dongle,VendorId:2830,ProductId:9415),
            new DeviceInfo("two","Link 380",true,Role:DeviceRole.Dongle,VendorId:2830,ProductId:9415));

        Assert.Equal(2,vm.Devices.Count);
        vm.SelectedDevice=vm.Devices.Single(d=>d.Device.Id=="two");
        Assert.Equal("two",session.SelectedId);
    }
    [Fact] public async Task StartFailureIsVisible()
    {
        var b=new FakeBackend{NextError=new UnauthorizedAccessException()}; var vm=new MainViewModel(new(b),a=>a());
        await vm.StartAsync(); Assert.Contains("USB",vm.Error);
    }
    [Fact] public async Task RefreshPropertiesTargetsSelectedDeviceAndTracksPendingState()
    {
        var backend=new FakeBackend { PropertyRefreshCompletion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm=new MainViewModel(new DeviceSession(backend),a=>a());
        backend.EmitDevices(new DeviceInfo("headset","Evolve 75 SE",false,Role:DeviceRole.Headset,VendorId:2830,ProductId:9474,CanRefreshProperties:true));

        var refresh=vm.RefreshPropertiesCommand.ExecuteAsync(null);

        Assert.True(vm.CanRefreshProperties);
        Assert.True(vm.IsRefreshingProperties);
        Assert.Equal(["headset"],backend.PropertyRefreshCalls);
        backend.PropertyRefreshCompletion.SetResult();
        await refresh;
        Assert.False(vm.IsRefreshingProperties);
    }
    [Fact] public void UnsupportedDeviceCannotRefreshProperties()
    {
        var backend=new FakeBackend(); var vm=new MainViewModel(new DeviceSession(backend),a=>a());
        backend.EmitDevices(new DeviceInfo("link370","Link 370",false,Role:DeviceRole.Dongle,VendorId:2830,ProductId:9415));

        Assert.False(vm.CanRefreshProperties);
        Assert.False(vm.RefreshPropertiesCommand.CanExecute(null));
    }
    [Fact] public async Task PropertyRefreshDoesNotBlockScanOrPairCommands()
    {
        var backend=new FakeBackend { PropertyRefreshCompletion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), Peers=[new("peer","Headset",LinkState.Disconnected)] };
        var vm=new MainViewModel(new DeviceSession(backend),a=>a());
        backend.EmitDevices(new DeviceInfo("dongle","Link 380",true,Role:DeviceRole.Dongle,CanRefreshProperties:true));
        var peer=Assert.Single(vm.Peers);

        var refresh=vm.RefreshPropertiesCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
        Assert.True(vm.CanScan);
        Assert.True(peer.ActionCommand.CanExecute(null));
        backend.PropertyRefreshCompletion.SetResult();
        await refresh;
    }
    [Fact] public void PropertyRefreshLabelsUpdateWithLanguage()
    {
        var texts=new JabraDesktop.App.LocalizationService(JabraDesktop.App.UiLanguage.German);
        var backend=new FakeBackend(); var vm=new MainViewModel(new DeviceSession(backend),a=>a(),texts);
        backend.EmitDevices(new DeviceInfo("headset","Evolve 75 SE",false,CanRefreshProperties:true));
        var changes=new List<string?>(); vm.PropertyChanged+=(_,e)=>changes.Add(e.PropertyName);

        Assert.Equal("Gerätestatus aktualisieren",vm.RefreshPropertiesText);
        texts.SetLanguage(JabraDesktop.App.UiLanguage.English);

        Assert.Equal("Refresh device status",vm.RefreshPropertiesText);
        Assert.Contains(nameof(MainViewModel.RefreshPropertiesText),changes);
    }
}
public class DisplayTests
{
    [Fact] public void UnknownTelemetryStaysUnknown()
    {
        var b=new FakeBackend(); var vm=new MainViewModel(new(b),a=>a());
        b.EmitDevices(new DeviceInfo("a","Jabra",true));
        Assert.Equal("Nicht verfügbar",vm.BatteryText); Assert.Equal("Nicht verfügbar",vm.FirmwareText);
    }
    [Fact] public void UnicodeNamesArePreserved()
    {
        var name=new string('界',200)+" 🎧"; var b=new FakeBackend(); var vm=new MainViewModel(new(b),a=>a());
        b.EmitDevices(new DeviceInfo("a",name,true)); Assert.Equal(name,vm.DeviceTitle);
    }
    [Fact] public async Task EmptySearchKeepsResultsSectionVisible()
    {
        var b=new FakeBackend(); var vm=new MainViewModel(new(b),a=>a());
        b.EmitDevices(new DeviceInfo("a","Jabra",true)); b.Scan.Writer.Complete();
        await vm.ScanCommand.ExecuteAsync(null); Assert.True(vm.ShowSearch);
    }
}
