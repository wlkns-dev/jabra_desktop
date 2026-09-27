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
    bool syncing,disposed,isScanning,hasSearched,isRefreshingProperties;
    public ObservableCollection<DeviceListItemViewModel> Devices { get; }=[];
    public ObservableCollection<PeerRow> Peers { get; }=[];
    public ObservableCollection<PeerRow> Results { get; }=[];
    public LocalizationService Texts => texts;
    public Func<string,Task<bool>> ConfirmUnpair { get; set; } = _ => Task.FromResult(false);
    public DeviceListItemViewModel? SelectedDevice
    {
        get=>selectedDevice;
        set
        {
            if(syncing || !SetProperty(ref selectedDevice,value)) return;
            session.Select(value?.Device.Id);
            if(value?.Device.CanPair==true) _=session.RefreshAsync();
            NotifyState();
        }
    }
    public bool CanScan => selectedDevice?.Device.CanPair==true && !session.IsBusy;
    public bool CanRefreshProperties => selectedDevice?.Device.CanRefreshProperties==true;
    public bool IsRefreshingProperties { get=>isRefreshingProperties; private set { if(SetProperty(ref isRefreshingProperties,value)) OnPropertyChanged(nameof(RefreshPropertiesText)); } }
    public string RefreshPropertiesText => texts[IsRefreshingProperties ? UiText.RefreshingDeviceStatus : UiText.RefreshDeviceStatus];
    public bool IsBusy => session.IsBusy;
    public bool IsScanning { get=>isScanning; private set { SetProperty(ref isScanning,value); OnPropertyChanged(nameof(ShowSearch)); } }
    public bool ShowSearch => hasSearched || IsScanning || Results.Count>0;
    public string SearchSummary => IsScanning ? texts[UiText.SearchingPairingMode]
        : Results.Count == 0 ? texts[UiText.SearchNoDevices]
        : Results.Count == 1 ? texts.Format(UiText.SearchFoundOne, Results.Count)
        : texts.Format(UiText.SearchFoundMany, Results.Count);
    public bool HasDevice => selectedDevice!=null;
    public bool HasDongle => selectedDevice?.Device.CanPair==true;
    public bool NoPeers => HasDongle && Peers.Count==0;
    public string DeviceTitle => selectedDevice?.Name ?? texts[UiText.DeviceTitleFallback];
    public string DeviceSubtitle => selectedDevice == null ? texts[UiText.PlugInDongle]
        : $"{selectedDevice.RoleLabel} · {texts[HasDongle ? UiText.BluetoothDongleSubtitle : UiText.UsbDeviceSubtitle]}";
    public string StatusText => IsScanning ? texts[UiText.SearchingPairingMode] : IsBusy ? texts[UiText.DeviceActionRunning]
        : HasDongle ? texts[UiText.ReadyToConnect] : texts[UiText.DeviceOverview];
    public string BatteryText => selectedDevice?.Device.BatteryPercent is { } n ? $"{n} %" : texts[UiText.BatteryUnavailable];
    public string FirmwareText => selectedDevice?.Device.Firmware ?? texts[UiText.FirmwareUnavailable];
    public string? Error => texts.TranslateSessionError(session.Error);
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelScanCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand RefreshPropertiesCommand => refreshPropertiesCommand!;
    public IAsyncRelayCommand RetryCommand { get; }
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
        RefreshCommand=new AsyncRelayCommand(()=>session.RefreshAsync(),()=>CanScan);
        refreshPropertiesCommand=new AsyncRelayCommand(async () =>
        {
            var deviceId=selectedDevice?.Device.Id;
            if(deviceId is null) return;
            IsRefreshingProperties=true;
            refreshPropertiesCommand?.NotifyCanExecuteChanged();
            try { await session.RefreshDevicePropertiesAsync(deviceId); }
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
    public Task StartAsync()=>session.StartAsync();
    public Task RefreshAsync()=>session.RefreshAsync();
    void OnChanged()=>dispatch(()=> { if(!disposed) Sync(); });
    void OnLanguageChanged(object? sender, EventArgs e) => dispatch(() =>
    {
        if (disposed) return;
        NotifyState();
        foreach (var device in Devices) device.NotifyLocalizationChanged();
        foreach (var row in Peers.Concat(Results)) row.NotifyLocalizationChanged();
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
            NotifyState();
        }
        finally { syncing=false; }
        if(refresh) _=session.RefreshAsync();
    }
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
            if(existingIndex<0) Devices.Insert(target,new DeviceListItemViewModel(info,texts));
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
    }
    void NotifyState()
    {
        foreach(var name in new[]{nameof(CanScan),nameof(CanRefreshProperties),nameof(IsBusy),nameof(HasDevice),nameof(HasDongle),nameof(NoPeers),nameof(DeviceTitle),nameof(DeviceSubtitle),nameof(StatusText),nameof(BatteryText),nameof(FirmwareText),nameof(RefreshPropertiesText),nameof(Error),nameof(HasError),nameof(ShowSearch),nameof(SearchSummary)}) OnPropertyChanged(name);
        ScanCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); RefreshPropertiesCommand.NotifyCanExecuteChanged();
    }
    internal async Task ActAsync(PeerInfo peer,DeviceAction action)
    {
        if(action==DeviceAction.Unpair && !await ConfirmUnpair(peer.Name)) return;
        await session.RunAsync(peer.Id,action);
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
    public PeerInfo Peer { get; }
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
    public PeerRow(PeerInfo peer,bool found,MainViewModel owner)
    {
        Peer=peer; IsFound=found; this.owner=owner;
        ActionCommand=new AsyncRelayCommand(()=>owner.ActAsync(peer,found ? DeviceAction.Pair : Connected ? DeviceAction.Disconnect : DeviceAction.Connect),()=>owner.CanScan);
        UnpairCommand=new AsyncRelayCommand(()=>owner.ActAsync(peer,DeviceAction.Unpair),()=>owner.CanScan);
    }
    public void NotifyEnabled() { ActionCommand.NotifyCanExecuteChanged(); UnpairCommand.NotifyCanExecuteChanged(); }
    public void NotifyLocalizationChanged()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ActionLabel));
    }
}
