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
    DeviceInfo? selectedDevice;
    bool syncing,disposed,isScanning,hasSearched;
    public ObservableCollection<DeviceInfo> Devices { get; }=[];
    public ObservableCollection<PeerRow> Peers { get; }=[];
    public ObservableCollection<PeerRow> Results { get; }=[];
    public LocalizationService Texts => texts;
    public Func<string,Task<bool>> ConfirmUnpair { get; set; } = _ => Task.FromResult(false);
    public DeviceInfo? SelectedDevice
    {
        get=>selectedDevice;
        set
        {
            if(syncing || !SetProperty(ref selectedDevice,value)) return;
            session.Select(value?.Id);
            if(value?.CanPair==true) _=session.RefreshAsync();
            NotifyState();
        }
    }
    public bool CanScan => selectedDevice?.CanPair==true && !session.IsBusy;
    public bool IsBusy => session.IsBusy;
    public bool IsScanning { get=>isScanning; private set { SetProperty(ref isScanning,value); OnPropertyChanged(nameof(ShowSearch)); } }
    public bool ShowSearch => hasSearched || IsScanning || Results.Count>0;
    public string SearchSummary => IsScanning ? texts[UiText.SearchingPairingMode]
        : Results.Count == 0 ? texts[UiText.SearchNoDevices]
        : Results.Count == 1 ? texts.Format(UiText.SearchFoundOne, Results.Count)
        : texts.Format(UiText.SearchFoundMany, Results.Count);
    public bool HasDevice => selectedDevice!=null;
    public bool HasDongle => selectedDevice?.CanPair==true;
    public bool NoPeers => HasDongle && Peers.Count==0;
    public string DeviceTitle => selectedDevice?.Name ?? texts[UiText.DeviceTitleFallback];
    public string DeviceSubtitle => selectedDevice == null ? texts[UiText.PlugInDongle]
        : HasDongle ? texts[UiText.BluetoothDongleSubtitle] : texts[UiText.UsbDeviceSubtitle];
    public string StatusText => IsScanning ? texts[UiText.SearchingPairingMode] : IsBusy ? texts[UiText.DeviceActionRunning]
        : HasDongle ? texts[UiText.ReadyToConnect] : texts[UiText.DeviceOverview];
    public string BatteryText => selectedDevice?.BatteryPercent is { } n ? $"{n} %" : texts[UiText.BatteryUnavailable];
    public string FirmwareText => selectedDevice?.Firmware ?? texts[UiText.FirmwareUnavailable];
    public string? Error => texts.TranslateSessionError(session.Error);
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelScanCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
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
            var snapshot=session.Devices;
            if(!Devices.SequenceEqual(snapshot)) { Devices.Clear(); foreach(var d in snapshot) Devices.Add(d); }
            var candidate=snapshot.FirstOrDefault(d=>d.Id==session.SelectedId);
            if(candidate==null && snapshot.Count>0)
            {
                candidate=snapshot.FirstOrDefault(d=>d.CanPair) ?? snapshot[0];
                session.Select(candidate.Id);
            }
            selectedDevice=old!=null && Equals(old,candidate) ? old : candidate;
            refresh=selectedDevice?.CanPair==true && (old?.Id!=selectedDevice.Id || old.CanPair==false);
            if(!Equals(old,selectedDevice)) OnPropertyChanged(nameof(SelectedDevice));
            ReplaceRows(Peers,session.Peers,false);
            ReplaceRows(Results,session.Results,true);
            NotifyState();
        }
        finally { syncing=false; }
        if(refresh) _=session.RefreshAsync();
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
        foreach(var name in new[]{nameof(CanScan),nameof(IsBusy),nameof(HasDevice),nameof(HasDongle),nameof(NoPeers),nameof(DeviceTitle),nameof(DeviceSubtitle),nameof(StatusText),nameof(BatteryText),nameof(FirmwareText),nameof(Error),nameof(HasError),nameof(ShowSearch),nameof(SearchSummary)}) OnPropertyChanged(name);
        ScanCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged();
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
