using System.Runtime.CompilerServices;
using System.Threading.Channels;
using JabraDesktop.Core;
namespace JabraDesktop.Tests;
public sealed class FakeBackend : IDeviceBackend
{
    public event Action<IReadOnlyList<DeviceInfo>>? DevicesChanged;
    public event Action<string>? Faulted;
    public Channel<PeerInfo> Scan { get; } = Channel.CreateUnbounded<PeerInfo>();
    public IReadOnlyList<PeerInfo> Peers { get; set; } = [];
    public Exception? NextError { get; set; }
    public TaskCompletionSource? OperationCompletion { get; set; }
    public TaskCompletionSource? ReadCompletion { get; set; }
    public List<(string Dongle, string Peer, DeviceAction Action)> Calls { get; } = [];
    public void EmitDevices(params DeviceInfo[] devices) => DevicesChanged?.Invoke(devices);
    public void Fail(string text) => Faulted?.Invoke(text);
    public Task StartAsync(CancellationToken token) => NextError is {} e ? Task.FromException(e) : Task.CompletedTask;
    public async Task<IReadOnlyList<PeerInfo>> GetPeersAsync(string id, CancellationToken token)
    { if (ReadCompletion is {} pending) await pending.Task; return Peers; }
    public async IAsyncEnumerable<PeerInfo> ScanAsync(string id, [EnumeratorCancellation] CancellationToken token)
    { await foreach(var p in Scan.Reader.ReadAllAsync(token)) yield return p; }
    public async Task ExecuteAsync(string id,string peer,DeviceAction action,CancellationToken token)
    {
        Calls.Add((id,peer,action));
        if(OperationCompletion is {} completion) await completion.Task;
        if(NextError is {} e) throw e;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
