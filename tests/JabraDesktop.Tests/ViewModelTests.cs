using JabraDesktop.Core;
using JabraDesktop.App.ViewModels;
namespace JabraDesktop.Tests;
public class ViewModelTests
{
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
