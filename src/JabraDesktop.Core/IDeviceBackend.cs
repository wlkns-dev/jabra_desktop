namespace JabraDesktop.Core;
public interface IDeviceBackend : IAsyncDisposable
{
    event Action<IReadOnlyList<DeviceInfo>>? DevicesChanged;
    event Action<string>? Faulted;
    Task StartAsync(CancellationToken token);
    Task<IReadOnlyList<PeerInfo>> GetPeersAsync(string dongleId, CancellationToken token);
    Task RefreshDevicePropertiesAsync(string deviceId, CancellationToken token) => Task.CompletedTask;
    IAsyncEnumerable<PeerInfo> ScanAsync(string dongleId, CancellationToken token);
    Task ExecuteAsync(string dongleId, string peerId, DeviceAction action, CancellationToken token);
}
