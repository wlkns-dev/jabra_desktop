namespace JabraDesktop.Core;
public enum LinkState { Unknown, Disconnected, Connected }
public enum DeviceAction { Pair, Connect, Disconnect, Unpair }
public record DeviceInfo(string Id, string Name, bool CanPair, int? BatteryPercent = null, string? Firmware = null);
public record PeerInfo(string Id, string Name, LinkState State);
