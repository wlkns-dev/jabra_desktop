using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JabraDesktop.Core;
using JabraDesktop.App;

namespace JabraDesktop.App.ViewModels;

public sealed class DongleTreeItemViewModel : ObservableObject
{
    public DeviceListItemViewModel Device { get; }
    readonly LocalizationService texts;
    public ObservableCollection<PeerRow> Peers { get; } = [];
    public string? Error { get; private set; }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string? ErrorText => HasError ? $"{texts[UiText.DongleRefreshError]} {Error}" : null;
    public DongleTreeItemViewModel(DeviceListItemViewModel device,LocalizationService texts) { Device = device; this.texts=texts; }
    public void Update(DonglePeerSnapshot snapshot, Func<PeerInfo, PeerRow> createRow)
    {
        Error = snapshot.Error;
        if (!Peers.Select(p => p.Peer).SequenceEqual(snapshot.Peers))
        {
            Peers.Clear();
            foreach (var peer in snapshot.Peers) Peers.Add(createRow(peer));
        }
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorText));
    }
    public void NotifyLocalizationChanged()=>OnPropertyChanged(nameof(ErrorText));
}
