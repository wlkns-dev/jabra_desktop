using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JabraDesktop.Core;

namespace JabraDesktop.App.ViewModels;

public sealed class DeviceListItemViewModel : ObservableObject
{
    readonly LocalizationService texts;
    readonly Action select;
    public IRelayCommand SelectCommand { get; }
    public DeviceInfo Device { get; private set; }
    public string Name => Device.Name;
    public string TelemetrySummary => string.Join(" · ",new[] {
        Device.Properties?.BatteryApplicable==true && Device.Properties.BatteryPercent is {} n ? $"{n} %" : null,
        Device.Properties?.Firmware ?? Device.Firmware
    }.Where(value=>value is not null));
    public bool HasTelemetry => TelemetrySummary.Length>0;
    public string StatusSummary => Device.CanPair ? texts[UiText.BluetoothDongleSubtitle] : texts[UiText.UsbDeviceSubtitle];
    public string RoleLabel => texts[Device.Role switch
    {
        DeviceRole.Dongle => UiText.DeviceRoleDongle,
        DeviceRole.Headset => UiText.DeviceRoleHeadset,
        DeviceRole.Other => UiText.DeviceRoleOther,
        _ => UiText.DeviceRoleUnknown
    }];
    public bool HasIdentifier => Device.VendorId is > 0 && Device.ProductId is > 0;
    public string IdentifierText => HasIdentifier
        ? $"VID {Device.VendorId!.Value:X4} · PID {Device.ProductId!.Value:X4}"
        : string.Empty;

    public DeviceListItemViewModel(DeviceInfo device, LocalizationService texts, Action? select = null)
    {
        Device = device;
        this.texts = texts;
        this.select=select ?? (()=>{});
        SelectCommand=new RelayCommand(()=>this.select());
    }

    public void Update(DeviceInfo device)
    {
        var old = Device;
        if (old == device) return;
        Device = device;
        OnPropertyChanged(nameof(Device));
        OnPropertyChanged(nameof(TelemetrySummary));
        OnPropertyChanged(nameof(HasTelemetry));
        if (old.Name != device.Name) OnPropertyChanged(nameof(Name));
        if (old.Role != device.Role) OnPropertyChanged(nameof(RoleLabel));
        if (old.VendorId != device.VendorId || old.ProductId != device.ProductId)
        {
            OnPropertyChanged(nameof(HasIdentifier));
            OnPropertyChanged(nameof(IdentifierText));
        }
    }

    public void NotifyLocalizationChanged() => OnPropertyChanged(nameof(RoleLabel));
}
