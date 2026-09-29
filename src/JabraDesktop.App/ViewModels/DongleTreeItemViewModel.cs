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
    public string? ErrorText => HasError ? $"{texts[UiText.DongleRefreshError]} {texts.TranslateSessionError(Error)}" : null;
    public DongleTreeItemViewModel(DeviceListItemViewModel device,LocalizationService texts) { Device = device; this.texts=texts; }
    public void Update(DonglePeerSnapshot snapshot, Func<PeerInfo, PeerRow> createRow)
    {
        Error = snapshot.Error;
        var connected=snapshot.Peers.Where(p=>p.State==LinkState.Connected).ToArray();
        for(var i=Peers.Count-1;i>=0;i--) if(!connected.Any(p=>p.Id==Peers[i].Peer.Id)) Peers.RemoveAt(i);
        for(var target=0;target<connected.Length;target++)
        {
            var peer=connected[target];
            var existing=Peers.FirstOrDefault(p=>p.Peer.Id==peer.Id);
            if(existing is null) Peers.Insert(target,createRow(peer));
            else
            {
                existing.Update(peer);
                var index=Peers.IndexOf(existing);
                if(index!=target) Peers.Move(index,target);
            }
        }
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorText));
    }
    public void NotifyLocalizationChanged()=>OnPropertyChanged(nameof(ErrorText));
}
