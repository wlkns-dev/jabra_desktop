namespace JabraDesktop.Core;
public interface IDeviceBackend : IAsyncDisposable
{
    event Action<IReadOnlyList<DeviceInfo>>? DevicesChanged;
    event Action<string>? Faulted;
    Task StartAsync(CancellationToken token);
    Task<IReadOnlyList<PeerInfo>> GetPeersAsync(string dongleId, CancellationToken token);
    Task RefreshDevicePropertiesAsync(string deviceId, CancellationToken token) => Task.CompletedTask;
    Task RefreshPeerPropertiesAsync(string dongleId,string peerId,CancellationToken token) => Task.CompletedTask;
    Task RenamePeerAsync(string dongleId,string peerId,string bluetoothName,CancellationToken token) =>
        throw new NotSupportedException("Bluetooth name change is not supported by this backend.");
    IAsyncEnumerable<PeerInfo> ScanAsync(string dongleId, CancellationToken token);
    Task ExecuteAsync(string dongleId, string peerId, DeviceAction action, CancellationToken token);
}
