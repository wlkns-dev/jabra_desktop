using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;
using Jabra.NET.Sdk.DevicePairing;
using JabraDesktop.Core;
namespace JabraDesktop.Jabra;

public sealed class JabraBackend : IDeviceBackend
{
    static string CurrentOperation => Activity.Current?.TraceId.ToString()[..8] ?? "none";
    sealed class DeviceEntry(IDevice source)
    {
        public IDevice Source { get; } = source;
        public string Id { get; } = Guid.NewGuid().ToString("N")[..10];
        public IBluetoothDongle? Dongle { get; set; }
        public RetryableInitialization<IBluetoothDongle?> Capability { get; } = new();
        public ConcurrentDictionary<string,IBluetoothAddress> Addresses { get; } = new();
        public string[] ChildAddresses { get; set; } = [];
        public Task? ChildDiscovery { get; set; }
        public DateTimeOffset LastChildDiscovery { get; set; }
        public Task? PropertyRefresh { get; set; }
        public DateTimeOffset LastPropertyRefresh { get; set; }
        public DeviceProperties Properties { get; set; } = new();
        public bool CanRefreshProperties { get; set; }
        public Task<BluetoothNameStatus?>? BluetoothNameLookup { get; set; }
        public bool CanRenameBluetooth { get; set; }
        public string? BluetoothName { get; set; }
        public ConcurrentDictionary<DevicePropertyValueKind, DevicePropertyReadRunner> PropertyReadRunners { get; } = new();
        public DeviceInfo BaseInfo => new(Id,DeviceMapper.DisplayName(Source.Name),Dongle != null,
            Role:DeviceMapper.Role(Source.Type),
            VendorId:DeviceMapper.OptionalUsbId(Source.VendorId),
            ProductId:DeviceMapper.OptionalUsbId(Source.ProductId), Properties:Properties);
        public DeviceInfo Info => BaseInfo with { CanRefreshProperties=CanRefreshProperties,
            BatteryPercent=Properties.BatteryPercent, Firmware=Properties.Firmware };
    }
    readonly object gate=new();
    readonly Dictionary<string,DeviceEntry> entries=[];
    readonly CompositeDisposable subscriptions=new();
    IManualApi? api;
    BluetoothModule? module;
    readonly IDevicePropertiesReader propertiesReader;
    bool propertiesInitialized;
    bool disposed;
    readonly RetryableInitialization<bool> initialization=new();
    readonly SnapshotPublisher<DeviceInfo[]> publisher=new();
    public event Action<IReadOnlyList<DeviceInfo>>? DevicesChanged;
    public event Action<string>? Faulted;
    public JabraBackend() : this(new JabraPropertiesReader()) { }
    internal JabraBackend(IDevicePropertiesReader propertiesReader) => this.propertiesReader=propertiesReader;
    public async Task StartAsync(CancellationToken token)
    {
        if(disposed) throw new ObjectDisposedException(nameof(JabraBackend));
        token.ThrowIfCancellationRequested();
        if(api == null)
        {
            api=Init.InitManualSdk(new Config(Environment.GetEnvironmentVariable("JABRA_PARTNER_KEY") ?? "", "JabraDesktopLinux", "Jabra Desktop"));
            module=new BluetoothModule(api);
            subscriptions.Add(api.DeviceAdded.Subscribe(d => { _ = AddAsync(d); }, e => Faulted?.Invoke(DeviceSession.FriendlyError(e))));
            subscriptions.Add(api.DeviceRemoved.Subscribe(Remove));
            subscriptions.Add(api.LogEvents.Where(e => e.Level==LogLevel.Error).Subscribe(e =>
            {
                // Native logs may contain identifiers; only expose classified messages.
                var message=e.ToString();
                Faulted?.Invoke(message.Contains("Permission denied",StringComparison.OrdinalIgnoreCase)
                    ? "USB-Zugriff verweigert. Jabra-Zugriffsregel installieren und Dongle neu einstecken."
                    : "Jabra-Gerätekommunikation meldet einen Fehler. Bitte Verbindung und USB-Berechtigungen prüfen.");
            }));
        }
        var start=initialization.Run(async ()=> { await api.Start(); return true; });
        await SdkLifetime.AwaitCompletion(start,TimeSpan.FromSeconds(15),()=>SlowOperation("sdk-start"),"sdk-start");
        var propertyModuleReady=await DevicePropertiesInitialization.TryInitializeAsync(propertiesReader,api,token);
        lock(gate) propertiesInitialized=propertyModuleReady;
        DeviceEntry[] retry;
        lock(gate) retry=entries.Values.Where(e=>e.Dongle==null).ToArray();
        foreach(var entry in retry) await DiscoverAsync(entry,true);
        DeviceEntry[] propertyEntries;
        lock(gate)
        {
            foreach(var entry in entries.Values) UpdatePropertyCapability(entry);
            propertyEntries=entries.Values.ToArray();
        }
        Publish();
        foreach(var entry in propertyEntries)
            if(entry.CanRefreshProperties) _=RefreshPropertiesAsync(entry,CancellationToken.None);
    }
    async Task AddAsync(IDevice source)
    {
        var entry=new DeviceEntry(source);
        var key=source.Id.Id.ToString()!;
        lock(gate) { if(disposed) return; entries[key]=entry; }
        Publish();
        await DiscoverAsync(entry,false);
        bool refreshProperties;
        lock(gate)
        {
            if(disposed || !entries.Values.Contains(entry)) return;
            UpdatePropertyCapability(entry);
            refreshProperties=entry.CanRefreshProperties;
        }
        Publish();
        if(entry.Dongle is null) _=DiscoverChildAsync(entry);
        if(refreshProperties) await RefreshPropertiesAsync(entry,CancellationToken.None);
    }
    void UpdatePropertyCapability(DeviceEntry entry)
    {
        var capabilities = DevicePropertyCapabilities.For(entry.BaseInfo);
        entry.Properties = new DeviceProperties(
            BatteryApplicable: capabilities.Any(c => c.ValueKind == DevicePropertyValueKind.BatteryPercent),
            BatteryPercent: entry.Properties.BatteryPercent,
            FirmwareApplicable: capabilities.Any(c => c.ValueKind == DevicePropertyValueKind.Firmware),
            Firmware: entry.Properties.Firmware,
            CanRefresh: propertiesInitialized && capabilities.Count > 0,
            PartNumber: entry.Properties.PartNumber,
            AudioName: entry.Properties.AudioName,
            MobilePhoneKnown: entry.Properties.MobilePhoneKnown,
            MobilePhone: entry.Properties.MobilePhone);
        entry.CanRefreshProperties = entry.Properties.CanRefresh;
    }
    void SlowOperation(string operation)
    {
        Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-operation-slow operation={operation}");
        Faulted?.Invoke("Jabra antwortet verzögert. Der laufende Vorgang bleibt gesperrt, bis die Gerätekommunikation beendet ist.");
    }
    async Task DiscoverAsync(DeviceEntry entry,bool retry)
    {
        try
        {
            var task=entry.Capability.Run(async ()=>
            {
                try { return await module!.CreateBluetoothDongle(entry.Source); }
                catch(InvalidBluetoothDeviceException) { return null; }
            }, retryCompleted: retry);
            await SdkLifetime.AwaitCompletion(task,TimeSpan.FromSeconds(10),()=>SlowOperation($"discover dongle={entry.Id}"),$"discover dongle={entry.Id}");
            lock(gate)
            {
                if(disposed || !entries.Values.Contains(entry)) return;
                entry.Dongle=task.Result;
            }
            Publish();
        }
        catch(Exception e) { if(!disposed) Faulted?.Invoke(DeviceSession.FriendlyError(e)); }
    }
    void Remove(IDevice source)
    {
        lock(gate) entries.Remove(source.Id.Id.ToString()!);
        Publish();
    }
    void Publish()
    {
        publisher.Publish(() =>
        {
            lock(gate)
            {
                if(disposed) return [];
                var current=entries.Values.ToArray();
                return current.Select(entry =>
                {
                    var parent=entry.Dongle is null
                        ? current.FirstOrDefault(candidate=>!ReferenceEquals(candidate,entry)
                            && (candidate.Dongle is not null || DeviceMapper.Role(candidate.Source.Type)==DeviceRole.Dongle)
                            && IsChildOf(entry,candidate))
                        : null;
                    return entry.Info with { ParentDongleId=parent?.Id };
                }).ToArray();
            }
        }, snapshot => { if(!disposed) DevicesChanged?.Invoke(snapshot); });
    }
    DeviceEntry Find(string id)
    {
        lock(gate) return entries.Values.FirstOrDefault(e=>e.Id==id && e.Dongle!=null)
            ?? throw new InvalidOperationException("Dongle nicht verfügbar oder Bluetooth-Verwaltung nicht unterstützt.");
    }
    public async Task RefreshDevicePropertiesAsync(string deviceId,CancellationToken token)
    {
        DeviceEntry? entry;
        lock(gate) entry=entries.Values.FirstOrDefault(e=>e.Id==deviceId && e.CanRefreshProperties);
        if(entry==null) return;
        await RefreshPropertiesAsync(entry,token,force:true);
    }
    Task DiscoverChildAsync(DeviceEntry entry)
    {
        lock(gate)
        {
            if(entry.ChildDiscovery is {IsCompleted:false} || DateTimeOffset.UtcNow-entry.LastChildDiscovery < TimeSpan.FromSeconds(30)) return entry.ChildDiscovery ?? Task.CompletedTask;
            entry.LastChildDiscovery=DateTimeOffset.UtcNow;
            return entry.ChildDiscovery=Task.Run(async ()=>
            {
                try
                {
                    var child=await module!.TryCreateBluetoothChildDevice(entry.Source);
                    lock(gate) if(!disposed && entries.Values.Contains(entry))
                        entry.ChildAddresses=child?.AllAddresses.Select(a=>a.AsHexString()).ToArray() ?? [];
                }
                catch(Exception) { /* Optional child metadata cannot fail pairing inventory. */ }
            });
        }
    }
    static bool IsChildOf(DeviceEntry child,DeviceEntry parent) => child.Source.CurrentConnections.Any(connection =>
    {
        var ancestor=connection.ParentConnection;
        for(var depth=0;ancestor is not null && depth<8;depth++,ancestor=ancestor.ParentConnection)
            if(ancestor.Device.Id.Id.Equals(parent.Source.Id.Id)) return true;
        return false;
    });
    DeviceEntry? ResolvePeer(DeviceEntry parent,IBluetoothAddress address)
    {
        var key=address.AsHexString();
        return entries.Values.FirstOrDefault(e=>e.ChildAddresses.Contains(key) && IsChildOf(e,parent));
    }
    Task RefreshPropertiesAsync(DeviceEntry entry,CancellationToken token,bool force=false)
    {
        Task refresh;
        lock(gate)
        {
            if(entry.PropertyRefresh is {IsCompleted:false}) refresh=entry.PropertyRefresh;
            else if(!force && DateTimeOffset.UtcNow-entry.LastPropertyRefresh<TimeSpan.FromSeconds(30)) return Task.CompletedTask;
            else
            {
                entry.LastPropertyRefresh=DateTimeOffset.UtcNow;
                refresh=entry.PropertyRefresh=Task.Run(async ()=>
                {
                    var reads=DevicePropertyCapabilities.For(entry.BaseInfo).Select(async capability=>
                    {
                        var runner=entry.PropertyReadRunners.GetOrAdd(capability.ValueKind,_=>new DevicePropertyReadRunner());
                        var value=await runner.ReadAsync(()=>propertiesReader.GetAsync(entry.Source,capability.PropertyName,CancellationToken.None),CancellationToken.None);
                        lock(gate)
                        {
                            if(disposed || !entries.Values.Contains(entry)) return;
                            entry.Properties=DevicePropertyValueUpdate.Apply(entry.Properties,capability,value);
                        }
                        Publish();
                    });
                    await Task.WhenAll(reads.Append(RefreshBluetoothNameAsync(entry)));
                });
            }
        }
        return refresh.WaitAsync(token);
    }
    async Task RefreshBluetoothNameAsync(DeviceEntry entry)
    {
        if(entry.Dongle is not null || DeviceMapper.Role(entry.Source.Type) is not (DeviceRole.Headset or DeviceRole.Other)) return;
        Task<BluetoothNameStatus?> lookup;
        lock(gate)
        {
            if(entry.BluetoothNameLookup is not {IsCompleted:false})
                entry.BluetoothNameLookup=Task.Run(()=>propertiesReader.ReadBluetoothNameAsync(entry.Source,CancellationToken.None));
            lookup=entry.BluetoothNameLookup;
        }
        try
        {
            var result=await lookup.WaitAsync(TimeSpan.FromSeconds(3));
            lock(gate) if(!disposed && entries.Values.Contains(entry) && result?.CanWrite==true)
            {
                entry.CanRenameBluetooth=true;
                entry.BluetoothName=result.Name;
            }
        }
        catch { /* Unsupported or slow name lookup does not block telemetry or pairing. */ }
    }
    public async Task RefreshPeerPropertiesAsync(string dongleId,string peerId,CancellationToken token)
    {
        var parent=Find(dongleId);
        if(!parent.Addresses.TryGetValue(peerId,out var address)) return;
        DeviceEntry[] candidates;
        lock(gate) candidates=entries.Values.Where(e=>e.Dongle is null && IsChildOf(e,parent)).ToArray();
        try { await Task.WhenAll(candidates.Select(DiscoverChildAsync)).WaitAsync(TimeSpan.FromSeconds(3),token); }
        catch(TimeoutException) { return; }
        DeviceEntry? child;
        lock(gate) child=ResolvePeer(parent,address);
        if(child?.CanRefreshProperties==true) await RefreshPropertiesAsync(child,token,force:true);
    }
    static string Remember(DeviceEntry entry,IBluetoothAddress address)
    {
        // Address never leaves the adapter. Public peer IDs are opaque and session-local.
        foreach(var known in entry.Addresses)
            if(known.Value.AsHexString()==address.AsHexString()) return known.Key;
        var id=Guid.NewGuid().ToString("N")[..10];
        entry.Addresses[id]=address;
        return id;
    }
    public async Task<IReadOnlyList<PeerInfo>> GetPeersAsync(string dongleId,CancellationToken token)
    {
        var entry=Find(dongleId);
        token.ThrowIfCancellationRequested();
        Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-call-start operation=get-pairing-list dongle={entry.Id}");
        var request=entry.Dongle!.GetPairingList();
        await SdkLifetime.AwaitCompletion(request,TimeSpan.FromSeconds(10),()=>SlowOperation($"get-pairing-list dongle={entry.Id}"),$"get-pairing-list dongle={entry.Id}");
        var peers=await request;
        Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-call-complete operation=get-pairing-list dongle={entry.Id} count={peers.Count}");
        var result = new List<PeerInfo>(peers.Count);
        foreach (var peer in peers)
        {
            var peerId = Remember(entry, peer.BluetoothAddress);
            var info = new DeviceInfo(peerId, DeviceMapper.DisplayName(peer.BluetoothName), false,
                Role: DeviceRole.Other);
            var capabilities = DevicePropertyCapabilities.For(info);
            var properties = new DeviceProperties(
                BatteryApplicable: capabilities.Any(c => c.ValueKind == DevicePropertyValueKind.BatteryPercent),
                FirmwareApplicable: capabilities.Any(c => c.ValueKind == DevicePropertyValueKind.Firmware),
                CanRefresh: false);
            DeviceEntry? child;
            DeviceEntry[] candidates;
            lock(gate)
            {
                child=ResolvePeer(entry,peer.BluetoothAddress);
                candidates=entries.Values.Where(e=>e.Dongle is null && IsChildOf(e,entry)).ToArray();
                if(child is not null) properties=child.Properties;
            }
            foreach(var candidate in candidates) _=DiscoverChildAsync(candidate);
            if(child?.CanRefreshProperties==true && DeviceMapper.State(peer.ConnectionStatus)==LinkState.Connected)
                _=RefreshPropertiesAsync(child,CancellationToken.None);
            var state=DeviceMapper.State(peer.ConnectionStatus);
            result.Add(new PeerInfo(peerId,child?.BluetoothName ?? DeviceMapper.DisplayName(peer.BluetoothName),
                state,properties,child?.Id,state==LinkState.Connected && child?.CanRenameBluetooth==true));
        }
        return result;
    }
    public async Task RenamePeerAsync(string dongleId,string peerId,string bluetoothName,CancellationToken token)
    {
        var parent=Find(dongleId);
        if(!parent.Addresses.TryGetValue(peerId,out var address))
            throw new InvalidOperationException("Gerät nicht mehr bekannt. Bitte aktualisieren.");
        DeviceEntry? child;
        lock(gate) child=ResolvePeer(parent,address);
        if(child?.CanRenameBluetooth!=true)
            throw new NotSupportedException("Bluetooth-Name kann für dieses Gerät nicht geändert werden.");
        var confirmed=await propertiesReader.SetBluetoothNameAsync(child.Source,bluetoothName,token);
        if(confirmed!=bluetoothName) throw new InvalidOperationException(DeviceSession.NameUnconfirmedError);
        lock(gate) if(!disposed && entries.Values.Contains(child))
            child.BluetoothName=confirmed;
    }
    public async IAsyncEnumerable<PeerInfo> ScanAsync(string dongleId,[EnumeratorCancellation] CancellationToken token)
    {
        var entry=Find(dongleId);
        Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-call-start operation=scan-subscribe dongle={entry.Id}");
        var channel=Channel.CreateUnbounded<PeerInfo>();
        using var subscription=entry.Dongle!.ScanForDevicesInPairingMode(TimeSpan.FromSeconds(30)).Subscribe(
            p=>channel.Writer.TryWrite(new(Remember(entry,p.BluetoothAddress),DeviceMapper.DisplayName(p.BluetoothName),LinkState.Unknown)),
            e=>channel.Writer.TryComplete(e),()=>channel.Writer.TryComplete());
        try { await foreach(var peer in channel.Reader.ReadAllAsync(token)) yield return peer; }
        finally
        {
            Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-call-start operation=stop-scan dongle={entry.Id}");
            await SdkLifetime.AwaitCompletion(entry.Dongle.StopDeviceScanning(),TimeSpan.FromSeconds(5),()=>SlowOperation($"stop-scan dongle={entry.Id}"),$"stop-scan dongle={entry.Id}");
            Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-call-complete operation=stop-scan dongle={entry.Id}");
        }
    }
    public async Task ExecuteAsync(string dongleId,string peerId,DeviceAction action,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var entry=Find(dongleId);
        if(!entry.Addresses.TryGetValue(peerId,out var address)) throw new InvalidOperationException("Gerät nicht mehr bekannt. Bitte erneut suchen.");
        var dongle=entry.Dongle!;
        // Await the underlying operation itself: cancellation must not permit overlapping commands.
        switch(action)
        {
            case DeviceAction.Pair: await AwaitSdk(dongle.PairAndConnectTo(address,TimeSpan.FromSeconds(30)),"pair-connect",entry.Id); break;
            case DeviceAction.Connect: await AwaitSdk(dongle.ConnectTo(address,TimeSpan.FromSeconds(15)),"connect",entry.Id); break;
            case DeviceAction.Disconnect: await AwaitSdk(dongle.DisconnectFrom(address,TimeSpan.FromSeconds(15)),"disconnect",entry.Id); break;
            case DeviceAction.Unpair:
                Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-call-start operation=is-connected dongle={entry.Id}");
                var connected=await dongle.IsConnectedTo(address);
                Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-call-complete operation=is-connected dongle={entry.Id}");
                if(connected) await AwaitSdk(dongle.DisconnectFrom(address,TimeSpan.FromSeconds(15)),"disconnect",entry.Id);
                await AwaitSdk(dongle.Unpair(address),"unpair",entry.Id); break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
    static async Task AwaitSdk(Task task,string operation,string dongleId)
    {
        Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-call-start operation={operation} dongle={dongleId}");
        await task;
        Trace.WriteLine($"{DateTimeOffset.Now:O} op={CurrentOperation} event=sdk-call-complete operation={operation} dongle={dongleId}");
    }
    public ValueTask DisposeAsync()
    {
        lock(gate) { if(disposed) return ValueTask.CompletedTask; disposed=true; entries.Clear(); }
        subscriptions.Dispose();
        if(api is IDisposable disposable) disposable.Dispose();
        return ValueTask.CompletedTask;
    }
}
