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
        public DeviceProperties Properties { get; set; } = new();
        public bool CanRefreshProperties { get; set; }
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
            CanRefresh: propertiesInitialized && capabilities.Count > 0);
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
            lock(gate) return disposed ? [] : entries.Values.Select(e=>e.Info).ToArray();
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
        await RefreshPropertiesAsync(entry,token);
    }
    async Task RefreshPropertiesAsync(DeviceEntry entry,CancellationToken token)
    {
        foreach (var capability in DevicePropertyCapabilities.For(entry.BaseInfo))
        {
            var runner = entry.PropertyReadRunners.GetOrAdd(capability.ValueKind, _ => new DevicePropertyReadRunner());
            var value=await runner.ReadAsync(
                ()=>propertiesReader.GetAsync(entry.Source,capability.PropertyName,CancellationToken.None),token);
            lock(gate)
            {
                if(disposed || !entries.Values.Contains(entry)) return;
                entry.Properties = DevicePropertyValueUpdate.Apply(entry.Properties, capability, value);
            }
            Publish();
        }
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
            // The SDK exposes paired peers as IBluetoothChildDevice, not IDevice. Its
            // property API only accepts IDevice, so keep applicable values explicitly unavailable.
            result.Add(new PeerInfo(peerId, DeviceMapper.DisplayName(peer.BluetoothName),
                DeviceMapper.State(peer.ConnectionStatus), properties));
        }
        return result;
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
