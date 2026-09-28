using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JabraDesktop.App;
using JabraDesktop.Core;
namespace JabraDesktop.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    readonly DeviceSession session;
    readonly Action<Action> dispatch;
    readonly LocalizationService texts;
    IAsyncRelayCommand? refreshPropertiesCommand;
    DeviceListItemViewModel? selectedDevice;
    PeerRow? selectedPeer;
    bool syncing,disposed,isScanning,hasSearched,isRefreshingProperties;
    public ObservableCollection<DeviceListItemViewModel> Devices { get; }=[];
    public ObservableCollection<PeerRow> Peers { get; }=[];
    public ObservableCollection<PeerRow> Results { get; }=[];
    public ObservableCollection<DongleTreeItemViewModel> Dongles { get; }=[];
    public ObservableCollection<DeviceListItemViewModel> StandaloneDevices { get; }=[];
    public LocalizationService Texts => texts;
    internal string? SessionSelectedId => session.SelectedId;
    public Func<string,Task<bool>> ConfirmUnpair { get; set; } = _ => Task.FromResult(false);
    public DeviceListItemViewModel? SelectedDevice
    {
        get=>selectedDevice;
        set
        {
            if(syncing) return;
            var changed=SetProperty(ref selectedDevice,value);
            if(!changed && selectedPeer is null) return;
            if(selectedPeer is not null) { selectedPeer=null; OnPropertyChanged(nameof(SelectedPeer)); }
            session.Select(value?.Device.Id);
            if(value?.Device.CanPair==true) _=session.RefreshAsync();
            NotifyState();
        }
    }
    public PeerRow? SelectedPeer
    {
        get=>selectedPeer;
        set
        {
            if(value is not null)
                value=Dongles.FirstOrDefault(d=>d.Device.Device.Id==value.DongleId)?.Peers.FirstOrDefault(p=>p.Peer.Id==value.Peer.Id);
            if(!SetProperty(ref selectedPeer,value)) return;
            if(value is not null)
            {
                session.CancelScan();
                var parent=Devices.FirstOrDefault(d=>d.Device.Id==value.DongleId);
                if(parent is not null)
                {
                    selectedDevice=parent;
                    session.Select(parent.Device.Id);
                    OnPropertyChanged(nameof(SelectedDevice));
                }
            }
            NotifyState();
        }
    }
    public bool CanScan => !IsScanning && SelectedPeer is null && selectedDevice?.Device.CanPair==true && !session.IsBusy;
    public bool CanRefreshProperties => SelectedPeer is {} peer ? peer.Peer.Properties?.CanRefresh==true : selectedDevice?.Device.CanRefreshProperties==true;
    public bool IsRefreshingProperties { get=>isRefreshingProperties; private set { if(SetProperty(ref isRefreshingProperties,value)) OnPropertyChanged(nameof(RefreshPropertiesText)); } }
    public string RefreshPropertiesText => texts[IsRefreshingProperties ? UiText.RefreshingDeviceStatus : UiText.RefreshDeviceStatus];
    public bool IsBusy => session.IsBusy;
    public bool IsScanning { get=>isScanning; private set { SetProperty(ref isScanning,value); OnPropertyChanged(nameof(ShowSearch)); } }
    public bool ShowSearch => HasDongle && (hasSearched || IsScanning || Results.Count>0);
    public bool ShowEndpointStatus=>HasDevice && !HasDongle;
    public bool HasProperties=>HasDevice && (ShowBattery || ShowFirmware);
    public bool ShowDeviceOverview => HasDongle;
    public string SearchSummary => IsScanning ? texts[UiText.SearchingPairingMode]
        : Results.Count == 0 ? texts[UiText.SearchNoDevices]
        : Results.Count == 1 ? texts.Format(UiText.SearchFoundOne, Results.Count)
        : texts.Format(UiText.SearchFoundMany, Results.Count);
    public bool HasDevice => selectedDevice!=null;
    public bool HasStandaloneDevices => StandaloneDevices.Count>0;
    public bool HasDongle => SelectedPeer is null && selectedDevice?.Device.CanPair==true;
    public bool IsEndpointSelected => SelectedPeer is not null;
    public bool NoPeers => HasDongle && Peers.Count==0;
    public string DeviceTitle => SelectedPeer?.Name ?? selectedDevice?.Name ?? texts[UiText.DeviceTitleFallback];
    public string DeviceSubtitle => SelectedPeer is {} peer ? texts.Format(UiText.ViaDongle, selectedDevice?.Name ?? peer.DongleId) : selectedDevice == null ? texts[UiText.PlugInDongle]
        : $"{selectedDevice.RoleLabel} · {texts[HasDongle ? UiText.BluetoothDongleSubtitle : UiText.UsbDeviceSubtitle]}";
    public string StatusText => SelectedPeer is {} peer ? peer.Status : IsScanning ? texts[UiText.SearchingPairingMode] : IsBusy ? texts[UiText.DeviceActionRunning]
        : HasDongle ? texts[UiText.ReadyToConnect] : texts[UiText.DeviceOverview];
    DeviceProperties? SelectedProperties => SelectedPeer is {} peer ? peer.Peer.Properties : selectedDevice?.Device.Properties;
    public string BatteryText => (SelectedPeer is not null ? SelectedProperties?.BatteryPercent : SelectedProperties?.BatteryPercent ?? selectedDevice?.Device.BatteryPercent) is {} n ? $"{n} %" : texts[UiText.BatteryUnavailable];
    public string FirmwareText => (SelectedPeer is not null ? SelectedProperties?.Firmware : SelectedProperties?.Firmware ?? selectedDevice?.Device.Firmware) ?? texts[UiText.FirmwareUnavailable];
    public bool ShowBattery => SelectedProperties?.BatteryApplicable ?? (SelectedPeer is null && selectedDevice?.Device.Role is DeviceRole.Headset or DeviceRole.Other);
    public bool ShowFirmware => SelectedProperties?.FirmwareApplicable ?? (HasDevice || IsEndpointSelected);
    public string? Error => texts.TranslateSessionError(session.PeerSnapshots.FirstOrDefault(s=>s.DongleId==selectedDevice?.Device.Id)?.Error ?? session.Error);
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelScanCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand RefreshPropertiesCommand => refreshPropertiesCommand!;
    public IAsyncRelayCommand RetryCommand { get; }
    public IRelayCommand<DeviceListItemViewModel> SelectDeviceCommand { get; }
    public MainViewModel(DeviceSession session, Action<Action> dispatch, LocalizationService? texts = null)
    {
        this.session = session;
        this.dispatch = dispatch;
        this.texts = texts ?? new LocalizationService(UiLanguage.German);
        this.texts.LanguageChanged += OnLanguageChanged;
        ScanCommand=new AsyncRelayCommand(async ()=>
        {
            hasSearched=true; IsScanning=true;
            try { await session.ScanAsync(); }
            finally { IsScanning=false; OnPropertyChanged(nameof(SearchSummary)); }
        },()=>CanScan);
        CancelScanCommand=new RelayCommand(session.CancelScan);
        SelectDeviceCommand=new RelayCommand<DeviceListItemViewModel>(device=>SelectedDevice=device);
        RefreshCommand=new AsyncRelayCommand(()=>session.RefreshAllAsync(),()=>CanScan);
        refreshPropertiesCommand=new AsyncRelayCommand(async () =>
        {
            var peer=SelectedPeer;
            var deviceId=selectedDevice?.Device.Id;
            if(deviceId is null) return;
            IsRefreshingProperties=true;
            refreshPropertiesCommand?.NotifyCanExecuteChanged();
            try
            {
                if(peer is not null) await session.RefreshPeerPropertiesAsync(peer.DongleId,peer.Peer.Id);
                else await session.RefreshDevicePropertiesAsync(deviceId);
            }
            finally
            {
                IsRefreshingProperties=false;
                refreshPropertiesCommand?.NotifyCanExecuteChanged();
            }
        },()=>CanRefreshProperties && !IsRefreshingProperties);
        RetryCommand=new AsyncRelayCommand(StartAsync);
        session.Changed+=OnChanged;
        Sync();
    }
    public async Task StartAsync()
    {
        await session.StartAsync();
        await session.RefreshAllAsync();
    }
    public Task RefreshAsync()=>session.RefreshAllAsync();
    void OnChanged()=>dispatch(()=> { if(!disposed) Sync(); });
    void OnLanguageChanged(object? sender, EventArgs e) => dispatch(() =>
    {
        if (disposed) return;
        NotifyState();
        foreach (var device in Devices) device.NotifyLocalizationChanged();
        foreach (var dongle in Dongles) dongle.NotifyLocalizationChanged();
        foreach (var row in Peers.Concat(Results).Concat(Dongles.SelectMany(d=>d.Peers))) row.NotifyLocalizationChanged();
    });
    void Sync()
    {
        if(syncing) return;
        syncing=true;
        bool refresh=false;
        try
        {
            var old=selectedDevice;
            var oldDeviceId=old?.Device.Id;
            var oldCanPair=old?.Device.CanPair;
            var snapshot=session.Devices;
            ReplaceDevices(snapshot);
            var candidate=Devices.FirstOrDefault(d=>d.Device.Id==session.SelectedId);
            if(candidate==null && snapshot.Count>0)
            {
                candidate=Devices.FirstOrDefault(d=>d.Device.CanPair) ?? Devices[0];
                session.Select(candidate.Device.Id);
            }
            selectedDevice=candidate;
            refresh=selectedDevice?.Device.CanPair==true && (oldDeviceId!=selectedDevice.Device.Id || oldCanPair==false);
            if(!ReferenceEquals(old,selectedDevice)) OnPropertyChanged(nameof(SelectedDevice));
            ReplaceRows(Peers,session.Peers,false);
            ReplaceRows(Results,session.Results,true);
            ReplaceHierarchy(snapshot,session.PeerSnapshots);
            if(selectedPeer is {} selection && !Dongles.Any(d=>d.Device.Device.Id==selection.DongleId && d.Peers.Contains(selection)))
            {
                selectedPeer=null;
                OnPropertyChanged(nameof(SelectedPeer));
            }
            NotifyState();
        }
        finally { syncing=false; }
        if(refresh) _=session.RefreshAsync();
    }
    void ReplaceHierarchy(IReadOnlyList<DeviceInfo> devices,IReadOnlyList<DonglePeerSnapshot> snapshots)
    {
        var dongleInfos=devices.Where(d=>d.CanPair).ToArray();
        for(var i=Dongles.Count-1;i>=0;i--) if(!dongleInfos.Any(d=>d.Id==Dongles[i].Device.Device.Id)) Dongles.RemoveAt(i);
        for(var i=0;i<dongleInfos.Length;i++)
        {
            var info=dongleInfos[i];
            var item=Devices.First(d=>d.Device.Id==info.Id);
            var index=IndexOfDongle(info.Id);
            if(index<0) Dongles.Insert(i,new(item,texts));
            else if(index!=i) Dongles.Move(index,i);
            var group=Dongles[i];
            var peerSnapshot=snapshots.FirstOrDefault(s=>s.DongleId==info.Id) ?? new(info.Id,[]);
            group.Update(peerSnapshot,p=>new PeerRow(p,false,this,info.Id));
        }
        var nestedIds=snapshots.SelectMany(s=>s.Peers).Select(p=>p.SourceDeviceId).OfType<string>().ToHashSet();
        var standalone=devices.Where(d=>!d.CanPair && !nestedIds.Contains(d.Id)).ToArray();
        for(var i=StandaloneDevices.Count-1;i>=0;i--) if(!standalone.Any(d=>d.Id==StandaloneDevices[i].Device.Id)) StandaloneDevices.RemoveAt(i);
        foreach(var info in standalone)
        {
            var item=Devices.First(d=>d.Device.Id==info.Id);
            if(!StandaloneDevices.Contains(item)) StandaloneDevices.Add(item);
        }
        OnPropertyChanged(nameof(HasStandaloneDevices));
    }
    int IndexOfDongle(string id) { for(var i=0;i<Dongles.Count;i++) if(Dongles[i].Device.Device.Id==id) return i; return -1; }
    void ReplaceDevices(IReadOnlyList<DeviceInfo> source)
    {
        var liveIds=source.Select(d=>d.Id).ToHashSet(StringComparer.Ordinal);
        for(var i=Devices.Count-1;i>=0;i--)
            if(!liveIds.Contains(Devices[i].Device.Id)) Devices.RemoveAt(i);
        for(var target=0;target<source.Count;target++)
        {
            var info=source[target];
            var existingIndex=-1;
            for(var i=0;i<Devices.Count;i++)
                if(Devices[i].Device.Id==info.Id) { existingIndex=i; break; }
            if(existingIndex<0) Devices.Insert(target,new DeviceListItemViewModel(info,texts,()=>SelectedDevice=Devices.FirstOrDefault(d=>d.Device.Id==info.Id)));
            else
            {
                Devices[existingIndex].Update(info);
                if(existingIndex!=target) Devices.Move(existingIndex,target);
            }
        }
    }
    void ReplaceRows(ObservableCollection<PeerRow> rows,IReadOnlyList<PeerInfo> source,bool found)
    {
        if(!rows.Select(r=>r.Peer).SequenceEqual(source))
        {
            rows.Clear(); foreach(var p in source) rows.Add(new PeerRow(p,found,this));
        }
        foreach(var row in rows) row.NotifyEnabled();
        foreach(var group in Dongles) foreach(var row in group.Peers) row.NotifyEnabled();
    }
    void NotifyState()
    {
        foreach(var name in new[]{nameof(CanScan),nameof(CanRefreshProperties),nameof(IsBusy),nameof(HasDevice),nameof(HasDongle),nameof(IsEndpointSelected),nameof(ShowDeviceOverview),nameof(ShowBattery),nameof(ShowFirmware),nameof(NoPeers),nameof(DeviceTitle),nameof(DeviceSubtitle),nameof(StatusText),nameof(BatteryText),nameof(FirmwareText),nameof(RefreshPropertiesText),nameof(Error),nameof(HasError),nameof(ShowSearch),nameof(SearchSummary),nameof(HasStandaloneDevices),nameof(HasProperties),nameof(ShowEndpointStatus)}) OnPropertyChanged(name);
        ScanCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); RefreshPropertiesCommand.NotifyCanExecuteChanged();
        foreach(var row in Peers.Concat(Results).Concat(Dongles.SelectMany(d=>d.Peers))) row.NotifyEnabled();
    }
    internal bool CanAct(string dongleId)=>session.Devices.Any(d=>d.Id==dongleId && d.CanPair) && !session.IsDongleBusy(dongleId);
    internal async Task ActAsync(PeerInfo peer,DeviceAction action,string dongleId)
    {
        if(action==DeviceAction.Unpair && !await ConfirmUnpair(peer.Name)) return;
        await session.RunAsync(dongleId,peer.Id,action);
    }
    public void Dispose()
    {
        disposed = true;
        session.Changed -= OnChanged;
        texts.LanguageChanged -= OnLanguageChanged;
    }
}
public sealed class PeerRow : ObservableObject
{
    readonly MainViewModel owner;
    public PeerInfo Peer { get; private set; }
    public string DongleId { get; }
    public bool IsSelected => ReferenceEquals(owner.SelectedPeer,this);
    public LocalizationService Texts => owner.Texts;
    public string Name=>Peer.Name;
    public bool IsFound { get; }
    public bool IsSaved=>!IsFound;
    public bool Connected=>Peer.State==LinkState.Connected;
    public string Status=>IsFound ? owner.Texts[UiText.FoundForPairing] : Peer.State switch
    { LinkState.Connected=>owner.Texts[UiText.Connected],LinkState.Disconnected=>owner.Texts[UiText.Disconnected],_=>owner.Texts[UiText.UnknownStatus] };
    public string ActionLabel=>IsFound ? owner.Texts[UiText.Pair] : Connected ? owner.Texts[UiText.Disconnect] : owner.Texts[UiText.Connect];
    public IAsyncRelayCommand ActionCommand { get; }
    public IAsyncRelayCommand UnpairCommand { get; }
    public IRelayCommand SelectCommand { get; }
    public PeerRow(PeerInfo peer,bool found,MainViewModel owner,string dongleId="")
    {
        Peer=peer; IsFound=found; this.owner=owner; DongleId=dongleId;
        var target=string.IsNullOrEmpty(dongleId) ? owner.SessionSelectedId : dongleId;
        DongleId=target ?? string.Empty;
        ActionCommand=new AsyncRelayCommand(()=>owner.ActAsync(Peer,found ? DeviceAction.Pair : Connected ? DeviceAction.Disconnect : DeviceAction.Connect,DongleId),()=>found ? owner.CanScan : owner.CanAct(DongleId));
        UnpairCommand=new AsyncRelayCommand(()=>owner.ActAsync(Peer,DeviceAction.Unpair,DongleId),()=>!found && owner.CanAct(DongleId));
        SelectCommand=new RelayCommand(Select);
    }
    public void Select() { if(!IsFound) owner.SelectedPeer=this; }
    public void Update(PeerInfo peer)
    {
        if(Peer==peer) return;
        Peer=peer;
        foreach(var property in new[]{nameof(Peer),nameof(Name),nameof(Connected),nameof(Status),nameof(ActionLabel),nameof(TelemetrySummary),nameof(HasTelemetry)}) OnPropertyChanged(property);
        NotifyEnabled();
    }
    public bool HasTelemetry=>Peer.Properties?.BatteryPercent is not null || Peer.Properties?.Firmware is not null;
    public string TelemetrySummary => string.Join(" · ", new[]{Peer.Properties?.BatteryPercent is {} n ? $"{n} %" : null, Peer.Properties?.Firmware}.Where(s=>s is not null));
    public void NotifyEnabled() { OnPropertyChanged(nameof(IsSelected)); ActionCommand.NotifyCanExecuteChanged(); UnpairCommand.NotifyCanExecuteChanged(); }
    public void NotifyLocalizationChanged()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ActionLabel));
    }
}
