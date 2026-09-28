using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JabraDesktop.Core;

namespace JabraDesktop.App.ViewModels;

public sealed class DongleTreeItemViewModel : ObservableObject
{
    public DeviceListItemViewModel Device { get; }
    public ObservableCollection<PeerRow> Peers { get; } = [];
    public string? Error { get; private set; }
    public DongleTreeItemViewModel(DeviceListItemViewModel device) => Device = device;
    public void Update(DonglePeerSnapshot snapshot, Func<PeerInfo, PeerRow> createRow)
    {
        Error = snapshot.Error;
        if (!Peers.Select(p => p.Peer).SequenceEqual(snapshot.Peers))
        {
            Peers.Clear();
            foreach (var peer in snapshot.Peers) Peers.Add(createRow(peer));
        }
        OnPropertyChanged(nameof(Error));
    }
}
