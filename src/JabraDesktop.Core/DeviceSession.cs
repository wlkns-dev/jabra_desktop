namespace JabraDesktop.Core;

// All state changes are serialized; consumers receive immutable snapshots.
public sealed class DeviceSession : IAsyncDisposable
{
    readonly IDeviceBackend backend;
    readonly object gate = new();
    readonly HashSet<string> busy = [];
    readonly HashSet<string> foregroundBusy = [];
    readonly Dictionary<string,SemaphoreSlim> operationLocks = [];
    IReadOnlyList<DeviceInfo> devices = [];
    IReadOnlyList<PeerInfo> peers = [], results = [];
    string? selected, error;
    long generation;
    bool disposed, starting;
    CancellationTokenSource? scanCancel;
    public event Action? Changed;
    public DeviceSession(IDeviceBackend backend)
    {
        this.backend = backend;
        backend.DevicesChanged += OnDevices;
        backend.Faulted += OnFault;
    }
    public IReadOnlyList<DeviceInfo> Devices { get { lock(gate) return devices; } }
    public IReadOnlyList<PeerInfo> Peers { get { lock(gate) return peers; } }
    public IReadOnlyList<PeerInfo> Results { get { lock(gate) return results; } }
    public string? SelectedId { get { lock(gate) return selected; } }
    public bool IsBusy { get { lock(gate) return starting || selected is {} id && foregroundBusy.Contains(id); } }
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
            }
            if(changed) Changed?.Invoke();
        }, showActivity:false);
    }
    public Task RefreshDevicePropertiesAsync(string deviceId,CancellationToken token = default)
    {
        lock(gate)
        {
            if(disposed || !devices.Any(d=>d.Id==deviceId)) return Task.CompletedTask;
        }
        return backend.RefreshDevicePropertiesAsync(deviceId,token);
    }
    bool Current(string id,long epoch) => !disposed && selected==id && generation==epoch;
    async Task WithOperation(Func<string,long,Task> operation, bool clearError = false, bool showActivity = true,
        CancellationToken operationToken = default)
    {
        string id; long epoch; SemaphoreSlim operationLock;
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
        try { await operationLock.WaitAsync(operationToken); }
        catch(OperationCanceledException) { return; }
        catch(ObjectDisposedException) { return; }
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
            catch(OperationCanceledException) { }
            catch(Exception e) { lock(gate) if(Current(id,epoch)) error=FriendlyError(e); }
        }
        finally
        {
            lock(gate) { busy.Remove(id); foregroundBusy.Remove(id); }
            operationLock.Release();
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
            }, clearError: true, operationToken: cancel.Token);
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
    }, clearError: true);
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
