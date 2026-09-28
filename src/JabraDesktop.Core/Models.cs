namespace JabraDesktop.Core;
public enum LinkState { Unknown, Disconnected, Connected }
public enum DeviceAction { Pair, Connect, Disconnect, Unpair }
public enum DeviceRole { Unknown, Headset, Dongle, Other }
public record DeviceProperties(bool BatteryApplicable = false, int? BatteryPercent = null,
    bool FirmwareApplicable = false, string? Firmware = null, bool CanRefresh = false);
public record DeviceInfo(string Id, string Name, bool CanPair, int? BatteryPercent = null, string? Firmware = null,
    DeviceRole Role = DeviceRole.Unknown, int? VendorId = null, int? ProductId = null, bool CanRefreshProperties = false,
    DeviceProperties? Properties = null, string? ParentDongleId = null);
public record PeerInfo(string Id, string Name, LinkState State, DeviceProperties? Properties = null,
    string? SourceDeviceId = null, bool CanRenameBluetooth = false);
public record DonglePeerSnapshot(string DongleId, IReadOnlyList<PeerInfo> Peers, string? Error = null);
