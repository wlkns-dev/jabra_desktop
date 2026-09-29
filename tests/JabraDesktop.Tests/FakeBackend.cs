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
    public Dictionary<string, IReadOnlyList<PeerInfo>> PeersByDongle { get; } = [];
    public Dictionary<string, Exception> PeerErrors { get; } = [];
    public List<string> PeerReads { get; } = [];
    public Exception? NextError { get; set; }
    public TaskCompletionSource? OperationCompletion { get; set; }
    public TaskCompletionSource? ReadCompletion { get; set; }
    public TaskCompletionSource ReadStarted { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource? PropertyRefreshCompletion { get; set; }
    public List<(string Dongle,string Peer)> PeerPropertyRefreshCalls { get; }=[];
    public Task RefreshPeerPropertiesAsync(string dongleId,string peerId,CancellationToken token) { PeerPropertyRefreshCalls.Add((dongleId,peerId)); return Task.CompletedTask; }
    public List<string> PropertyRefreshCalls { get; }=[];
    public List<(string Dongle, string Peer, DeviceAction Action)> Calls { get; } = [];
    public List<(string Dongle,string Peer,string Name)> RenameCalls { get; } = [];
    public Exception? RenameErrorAfterWrite { get; set; }
    public int ScanStarted { get; private set; }
    public void EmitDevices(params DeviceInfo[] devices) => DevicesChanged?.Invoke(devices);
    public void Fail(string text) => Faulted?.Invoke(text);
    public Task StartAsync(CancellationToken token) => NextError is {} e ? Task.FromException(e) : Task.CompletedTask;
    public async Task<IReadOnlyList<PeerInfo>> GetPeersAsync(string id, CancellationToken token)
    { PeerReads.Add(id); ReadStarted.TrySetResult(); if (ReadCompletion is {} pending) await pending.Task; if(PeerErrors.TryGetValue(id,out var error)) throw error; return PeersByDongle.GetValueOrDefault(id, Peers); }
    public async Task RefreshDevicePropertiesAsync(string id,CancellationToken token)
    {
        PropertyRefreshCalls.Add(id);
        if(PropertyRefreshCompletion is {} pending) await pending.Task.WaitAsync(token);
    }
    public async IAsyncEnumerable<PeerInfo> ScanAsync(string id, [EnumeratorCancellation] CancellationToken token)
    { ScanStarted++; await foreach(var p in Scan.Reader.ReadAllAsync(token)) yield return p; }
    public async Task ExecuteAsync(string id,string peer,DeviceAction action,CancellationToken token)
    {
        Calls.Add((id,peer,action));
        if(OperationCompletion is {} completion) await completion.Task;
        if(NextError is {} e) throw e;
    }
    public Task RenamePeerAsync(string dongleId,string peerId,string bluetoothName,CancellationToken token)
    {
        RenameCalls.Add((dongleId,peerId,bluetoothName));
        if(PeersByDongle.TryGetValue(dongleId,out var peers))
            PeersByDongle[dongleId]=peers.Select(p=>p.Id==peerId ? p with { Name=bluetoothName } : p).ToArray();
        if(RenameErrorAfterWrite is {} error) throw error;
        return Task.CompletedTask;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
