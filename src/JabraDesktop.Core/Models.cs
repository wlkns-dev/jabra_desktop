namespace JabraDesktop.Core;
public enum LinkState { Unknown, Disconnected, Connected }
public enum DeviceAction { Pair, Connect, Disconnect, Unpair }
public enum DeviceRole { Unknown, Headset, Dongle, Other }
public record DeviceInfo(string Id, string Name, bool CanPair, int? BatteryPercent = null, string? Firmware = null,
    DeviceRole Role = DeviceRole.Unknown, int? VendorId = null, int? ProductId = null);
public record PeerInfo(string Id, string Name, LinkState State);
