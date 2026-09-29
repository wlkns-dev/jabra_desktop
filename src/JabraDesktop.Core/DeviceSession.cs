using System.Diagnostics;

namespace JabraDesktop.Core;

// All state changes are serialized; consumers receive immutable snapshots.
public sealed class DeviceSession : IAsyncDisposable
{
    readonly IDeviceBackend backend;
    readonly Action<string> diagnostic;
    readonly object gate = new();
    readonly HashSet<string> busy = [];
    readonly HashSet<string> foregroundBusy = [];
    readonly Dictionary<string,SemaphoreSlim> operationLocks = [];
    readonly Dictionary<string,DonglePeerSnapshot> peerSnapshots = [];
    readonly Dictionary<(string Id,long Attachment),Task> refreshes = [];
    readonly Dictionary<string,long> attachments=[];
    long nextAttachment;
    IReadOnlyList<DeviceInfo> devices = [];
    IReadOnlyList<PeerInfo> peers = [], results = [];
    string? selected, error;
    long generation;
    bool disposed, starting;
    CancellationTokenSource? scanCancel;
    public event Action? Changed;
    public DeviceSession(IDeviceBackend backend, Action<string>? diagnostic = null)
    {
        this.backend = backend;
        this.diagnostic = diagnostic ?? (message => Trace.WriteLine(message));
        backend.DevicesChanged += OnDevices;
        backend.Faulted += OnFault;
    }
    public IReadOnlyList<DeviceInfo> Devices { get { lock(gate) return devices; } }
    public IReadOnlyList<PeerInfo> Peers { get { lock(gate) return peers; } }
    public IReadOnlyList<PeerInfo> Results { get { lock(gate) return results; } }
    public IReadOnlyList<DonglePeerSnapshot> PeerSnapshots { get { lock(gate) return peerSnapshots.Values.ToArray(); } }
    public string? SelectedId { get { lock(gate) return selected; } }
    public bool IsBusy { get { lock(gate) return starting || selected is {} id && foregroundBusy.Contains(id); } }
    public bool IsDongleBusy(string id) { lock(gate) return foregroundBusy.Contains(id); }
    bool Attached(string id,long attachment)=>!disposed && attachments.GetValueOrDefault(id)==attachment;
    public string? Error { get { lock(gate) return error; } }
    void OnFault(string message) { lock(gate) { if(disposed) return; error = message; } Changed?.Invoke(); }
    void OnDevices(IReadOnlyList<DeviceInfo> update)
    {
        lock(gate)
        {
            if(disposed) return;
            var next=update.ToArray();
            if(devices.SequenceEqual(next)) return;
            devices=next;
            var dongleIds=devices.Where(d=>d.CanPair).Select(d=>d.Id).ToHashSet();
            foreach(var id in peerSnapshots.Keys.Where(id=>!dongleIds.Contains(id)).ToArray()) peerSnapshots.Remove(id);
            foreach(var id in attachments.Keys.Where(id=>!dongleIds.Contains(id)).ToArray()) attachments.Remove(id);
            foreach(var id in dongleIds)
            {
                peerSnapshots.TryAdd(id,new(id,[]));
                if(!attachments.ContainsKey(id)) attachments[id]=++nextAttachment;
            }
            if(selected != null && !devices.Any(d => d.Id == selected)) SelectLocked(null);
        }
        Changed?.Invoke();
    }
    void SelectLocked(string? id)
    {
        generation++; selected = id; peers = []; results = []; error = null;
        scanCancel?.Cancel();
    }
    public void Select(string? id)
    {
        lock(gate)
        {
            if(disposed || id == selected) return;
            SelectLocked(devices.Any(d => d.Id == id) ? id : null);
        }
        Changed?.Invoke();
    }
    public async Task StartAsync(CancellationToken token = default)
    {
        lock(gate) { if(disposed || starting) return; starting=true; error=null; }
        Changed?.Invoke();
        try { await backend.StartAsync(token); }
        catch(Exception e) { OnFault(FriendlyError(e)); }
        finally { lock(gate) starting=false; Changed?.Invoke(); }
    }
    public async Task RefreshAsync(CancellationToken token = default)
    {
        await WithOperation(async (id,epoch) =>
        {
            var update = await backend.GetPeersAsync(id,token);
            bool changed=false;
            lock(gate)
            {
                if(Current(id,epoch) && !peers.SequenceEqual(update)) { peers=update.ToArray(); changed=true; }
                if(Current(id,epoch)) peerSnapshots[id]=new(id,update.ToArray());
            }
            if(changed) Changed?.Invoke();
        }, operationName:"refresh", showActivity:false);
    }
    public Task RefreshAsync(string dongleId,CancellationToken token = default) => RefreshDongleAsync(dongleId,token);
    public Task RefreshAllAsync(CancellationToken token = default)
    {
        string[] ids;
        lock(gate) ids=devices.Where(d=>d.CanPair).Select(d=>d.Id).ToArray();
        return Task.WhenAll(ids.Select(id=>RefreshDongleAsync(id,token)));
    }
    Task RefreshDongleAsync(string id,CancellationToken token)
    {
        TaskCompletionSource completion;
        long attachment;
        lock(gate)
        {
            if(disposed || !devices.Any(d=>d.Id==id && d.CanPair)) return Task.CompletedTask;
            attachment=attachments[id];
            if(refreshes.TryGetValue((id,attachment),out var existing)) return existing;
            completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
            refreshes[(id,attachment)]=completion.Task;
        }
        _=RefreshDongleCoreAsync(id,attachment,token,completion);
        return completion.Task;
    }
    async Task RefreshDongleCoreAsync(string id,long attachment,CancellationToken token,TaskCompletionSource completion)
    {
        SemaphoreSlim operationLock;
        lock(gate)
        {
            if(!operationLocks.TryGetValue(id,out operationLock!)) operationLocks[id]=operationLock=new(1,1);
        }
        try
        {
            await operationLock.WaitAsync(token);
            IReadOnlyList<PeerInfo> update;
            try
            {
                lock(gate) if(!Attached(id,attachment)) return;
                update=await backend.GetPeersAsync(id,token);
            }
            finally { operationLock.Release(); }
            lock(gate)
            {
                if(Attached(id,attachment))
                {
                    peerSnapshots[id]=new(id,update.ToArray());
                    if(selected==id) peers=update.ToArray();
                }
            }
        }
        catch(Exception e)
        {
            lock(gate)
            {
                if(Attached(id,attachment) && peerSnapshots.TryGetValue(id,out var last))
                    peerSnapshots[id]=last with { Error=FriendlyError(e) };
            }
        }
        finally
        {
            lock(gate) refreshes.Remove((id,attachment));
            Changed?.Invoke();
            completion.TrySetResult();
        }
    }
    public Task RefreshDevicePropertiesAsync(string deviceId,CancellationToken token = default)
    {
        lock(gate)
        {
            if(disposed || !devices.Any(d=>d.Id==deviceId)) return Task.CompletedTask;
        }
        return backend.RefreshDevicePropertiesAsync(deviceId,token);
    }
    public async Task RefreshPeerPropertiesAsync(string dongleId,string peerId,CancellationToken token=default)
    {
        lock(gate) if(disposed || !peerSnapshots.TryGetValue(dongleId,out var snapshot) || !snapshot.Peers.Any(p=>p.Id==peerId)) return;
        await backend.RefreshPeerPropertiesAsync(dongleId,peerId,token);
        await RefreshAsync(dongleId,token);
    }
    bool Current(string id,long epoch) => !disposed && selected==id && generation==epoch;
    async Task WithOperation(Func<string,long,Task> operation, string operationName = "operation", bool clearError = false, bool showActivity = true,
        CancellationToken operationToken = default)
    {
        string id; long epoch; SemaphoreSlim operationLock;
        using var operationSpan = new Activity($"jabra.{operationName}").Start();
        var operationId = operationSpan.TraceId.ToString()[..8];
        lock(gate)
        {
            if(disposed) return;
            if(selected is not {} candidate || !devices.Any(d => d.Id==candidate && d.CanPair))
            {
                error="Bitte einen Bluetooth-Dongle mit unterstützter Geräteverwaltung auswählen.";
                id=""; epoch=0; operationLock=new SemaphoreSlim(1,1);
            }
            else
            {
                id=candidate; epoch=generation;
                if(!operationLocks.TryGetValue(id,out operationLock!)) operationLocks[id]=operationLock=new SemaphoreSlim(1,1);
            }
        }
        if(id=="") { Changed?.Invoke(); operationLock.Dispose(); return; }
        var waitStarted = Stopwatch.GetTimestamp();
        diagnostic($"op={operationId} operation={operationName} dongle={id} state=waiting-for-dongle-lock");
        try { await operationLock.WaitAsync(operationToken); }
        catch(OperationCanceledException) { return; }
        catch(ObjectDisposedException) { return; }
        diagnostic($"op={operationId} operation={operationName} dongle={id} state=lock-acquired wait_ms={Stopwatch.GetElapsedTime(waitStarted).TotalMilliseconds:F0}");
        var operationStarted = Stopwatch.GetTimestamp();
        bool activity=false;
        try
        {
            lock(gate)
            {
                if(!Current(id,epoch) || !devices.Any(d=>d.Id==id && d.CanPair)) return;
                busy.Add(id);
                if(showActivity) { foregroundBusy.Add(id); activity=true; }
                if(clearError) error=null;
            }
            if(activity) Changed?.Invoke();
            try { await operation(id,epoch); }
            catch(OperationCanceledException) { diagnostic($"op={operationId} operation={operationName} dongle={id} state=cancelled"); }
            catch(Exception e) { diagnostic($"op={operationId} operation={operationName} dongle={id} state=failed error={e.GetType().Name}"); lock(gate) if(Current(id,epoch)) error=FriendlyError(e); }
        }
        finally
        {
            lock(gate) { busy.Remove(id); foregroundBusy.Remove(id); }
            operationLock.Release();
            diagnostic($"op={operationId} operation={operationName} dongle={id} state=completed duration_ms={Stopwatch.GetElapsedTime(operationStarted).TotalMilliseconds:F0}");
            if(activity) Changed?.Invoke();
        }
    }
    public Task ScanAsync(CancellationToken token = default)
    {
        CancellationTokenSource cancel;
        lock(gate)
        {
            if(disposed) return Task.CompletedTask;
            scanCancel?.Cancel();
            cancel=CancellationTokenSource.CreateLinkedTokenSource(token);
            cancel.CancelAfter(TimeSpan.FromSeconds(30));
            scanCancel=cancel;
        }
        return RunScanAsync(cancel);
    }
    async Task RunScanAsync(CancellationTokenSource cancel)
    {
        try
        {
            await WithOperation(async (id,epoch) =>
            {
                lock(gate) { if(!Current(id,epoch)) return; results=[]; }
                await foreach(var peer in backend.ScanAsync(id,cancel.Token))
                {
                    lock(gate)
                    {
                        if(!Current(id,epoch)) break;
                        results=results.Where(p => p.Id!=peer.Id).Append(peer).ToArray();
                    }
                    Changed?.Invoke();
                }
            }, operationName:"scan", clearError: true, operationToken: cancel.Token);
        }
        finally
        {
            lock(gate) if(ReferenceEquals(scanCancel,cancel)) scanCancel=null;
            cancel.Dispose();
        }
    }
    public void CancelScan() { lock(gate) scanCancel?.Cancel(); }
    public Task RunAsync(string peerId, DeviceAction action, CancellationToken token = default) => WithOperation(async (id,epoch) =>
    {
        lock(gate)
        {
            var source=action==DeviceAction.Pair ? results : peers;
            if(!source.Any(p => p.Id==peerId)) throw new InvalidOperationException("Gerät nicht mehr verfügbar. Bitte aktualisieren oder erneut suchen.");
        }
        Exception? failure=null;
        try { await backend.ExecuteAsync(id,peerId,action,token); }
        catch(Exception e) { failure=e; }
        // Keep the lease until the actual backend task finishes, including after cancellation.
        try
        {
            lock(gate) if(!Current(id,epoch)) return;
            var update=await backend.GetPeersAsync(id,CancellationToken.None);
            lock(gate) if(Current(id,epoch)) peers=update.ToArray();
        }
        catch(Exception e) { failure ??= e; }
        if(failure != null) throw failure;
    }, operationName:action.ToString().ToLowerInvariant(), clearError: true);
    public async Task RunAsync(string dongleId,string peerId,DeviceAction action,CancellationToken token = default)
    {
        SemaphoreSlim operationLock;
        long attachment;
        lock(gate)
        {
            if(disposed || !attachments.TryGetValue(dongleId,out attachment)) return;
            if(!operationLocks.TryGetValue(dongleId,out operationLock!)) operationLocks[dongleId]=operationLock=new(1,1);
        }
        diagnostic($"operation={action.ToString().ToLowerInvariant()} dongle={dongleId} state=waiting-for-dongle-lock");
        try { await operationLock.WaitAsync(token); }
        catch(OperationCanceledException) { return; }
        bool active=false;
        try
        {
            lock(gate)
            {
                if(!Attached(dongleId,attachment)) return;
                var source=action==DeviceAction.Pair && selected==dongleId ? results : peerSnapshots[dongleId].Peers;
                if(!source.Any(p=>p.Id==peerId))
                    throw new InvalidOperationException("Gerät nicht mehr verfügbar. Bitte aktualisieren oder erneut suchen.");
                foregroundBusy.Add(dongleId); active=true;
                peerSnapshots[dongleId]=peerSnapshots[dongleId] with { Error=null };
            }
            Changed?.Invoke();
            Exception? failure=null;
            try { await backend.ExecuteAsync(dongleId,peerId,action,token); }
            catch(Exception e) { failure=e; }
            try
            {
                lock(gate) if(!Attached(dongleId,attachment)) return;
                var update=await backend.GetPeersAsync(dongleId,CancellationToken.None);
                lock(gate) if(Attached(dongleId,attachment))
                {
                    peerSnapshots[dongleId]=new(dongleId,update.ToArray());
                    if(selected==dongleId) peers=update.ToArray();
                }
            }
            catch(Exception e) { failure ??=e; }
            if(failure is not null) throw failure;
        }
        catch(Exception e)
        {
            lock(gate) if(Attached(dongleId,attachment))
            {
                peerSnapshots[dongleId]=peerSnapshots[dongleId] with { Error=FriendlyError(e) };
            }
        }
        finally
        {
            lock(gate) if(active) foregroundBusy.Remove(dongleId);
            operationLock.Release();
            Changed?.Invoke();
        }
    }
    public async Task<bool> RenamePeerAsync(string dongleId,string peerId,string newName,CancellationToken token=default)
    {
        var bluetoothName=BluetoothNameRules.Normalize(newName);
        if(bluetoothName is null) return false;
        SemaphoreSlim operationLock;
        long attachment;
        lock(gate)
        {
            if(disposed || !attachments.TryGetValue(dongleId,out attachment)) return false;
            if(!operationLocks.TryGetValue(dongleId,out operationLock!)) operationLocks[dongleId]=operationLock=new(1,1);
        }
        try { await operationLock.WaitAsync(token); }
        catch(OperationCanceledException) { return false; }
        bool active=false;
        try
        {
            lock(gate)
            {
                if(!Attached(dongleId,attachment)
                    || !peerSnapshots.TryGetValue(dongleId,out var snapshot)
                    || !snapshot.Peers.Any(p=>p.Id==peerId && p.State==LinkState.Connected && p.CanRenameBluetooth)) return false;
                foregroundBusy.Add(dongleId); active=true;
                peerSnapshots[dongleId]=snapshot with { Error=null };
            }
            Changed?.Invoke();
            await backend.RenamePeerAsync(dongleId,peerId,bluetoothName,token);
            var update=await backend.GetPeersAsync(dongleId,CancellationToken.None);
            lock(gate) if(Attached(dongleId,attachment))
            {
                peerSnapshots[dongleId]=new(dongleId,update.ToArray());
                if(selected==dongleId) peers=update.ToArray();
            }
            return true;
        }
        catch(Exception e)
        {
            lock(gate) if(Attached(dongleId,attachment) && peerSnapshots.TryGetValue(dongleId,out var last))
                peerSnapshots[dongleId]=last with { Error=FriendlyError(e) };
            return false;
        }
        finally
        {
            lock(gate) if(active) foregroundBusy.Remove(dongleId);
            operationLock.Release();
            Changed?.Invoke();
        }
    }
    public static string FriendlyError(Exception e) => e switch
    {
        UnauthorizedAccessException => "USB-Zugriff verweigert. Jabra-Zugriffsregel installieren und Dongle neu einstecken.",
        TimeoutException => "Das Gerät antwortet nicht rechtzeitig. Verbindungsstatus aktualisieren und erneut versuchen.",
        DllNotFoundException => "Eine benötigte Systembibliothek fehlt. Bitte die Einrichtung in der README prüfen.",
        _ => e.Message
    };
    public async ValueTask DisposeAsync()
    {
        lock(gate) { if(disposed) return; disposed=true; generation++; scanCancel?.Cancel(); }
        backend.DevicesChanged-=OnDevices; backend.Faulted-=OnFault;
        await backend.DisposeAsync();
    }
}
