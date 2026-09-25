using JabraDesktop.Core;
using JabraDesktop.Jabra;

await using var backend=new JabraBackend();
IReadOnlyList<DeviceInfo> devices=[];
backend.DevicesChanged += d => devices=d;
backend.Faulted += message => Console.Error.WriteLine(message);
try
{
    await backend.StartAsync(CancellationToken.None);
    await Task.Delay(3500);
    var command=args.FirstOrDefault() ?? "list";
    foreach(var d in devices) Console.WriteLine($"{d.Id}  {d.Name}  {(d.CanPair ? "Bluetooth-Verwaltung" : "Gerät")}");
    if(command=="list") return devices.Count>0 ? 0 : 2;
    // IDs are session-local. A CLI invocation selects a dongle by list index (default 0).
    var dongles=devices.Where(d=>d.CanPair).ToArray();
    var index=args.Length>1 && int.TryParse(args[1],out var n) ? n : 0;
    if(index<0 || index>=dongles.Length) return 2;
    var id=dongles[index].Id;
    if(command=="scan")
    {
        await foreach(var p in backend.ScanAsync(id,CancellationToken.None)) Console.WriteLine($"Gefunden: {p.Name}");
        return 0;
    }
    if(command=="pair")
    {
        if(args.Length!=3) { Console.Error.WriteLine("pair Dongle-Index exakter-Gerätename"); return 2; }
        var found=new Dictionary<string,PeerInfo>();
        await foreach(var peer in backend.ScanAsync(id,CancellationToken.None))
        {
            found[peer.Id]=peer;
            Console.WriteLine($"Gefunden: {peer.Name}");
        }
        var targets=found.Values.Where(p=>p.Name==args[2]).ToArray();
        if(targets.Length!=1) { Console.Error.WriteLine("Kein eindeutiger Treffer. Bitte Pairing-Modus und Gerätenamen prüfen."); return 2; }
        await backend.ExecuteAsync(id,targets[0].Id,DeviceAction.Pair,CancellationToken.None);
        foreach(var peer in await backend.GetPeersAsync(id,CancellationToken.None)) Console.WriteLine($"{peer.Name}: {peer.State}");
        return 0;
    }
    var peers=await backend.GetPeersAsync(id,CancellationToken.None);
    for(var i=0;i<peers.Count;i++) Console.WriteLine($"{i}: {peers[i].Name} ({peers[i].State})");
    if(command=="peers") return 0;
    if(command is "connect" or "disconnect" or "unpair")
    {
        if(args.Length<3 || !int.TryParse(args[2],out var p) || p<0 || p>=peers.Count) return 2;
        if(command=="unpair" && !args.Contains("--confirm")) { Console.Error.WriteLine("Entkoppeln benötigt --confirm."); return 2; }
        var action=command switch { "connect"=>DeviceAction.Connect,"disconnect"=>DeviceAction.Disconnect,_=>DeviceAction.Unpair };
        await backend.ExecuteAsync(id,peers[p].Id,action,CancellationToken.None);
        foreach(var peer in await backend.GetPeersAsync(id,CancellationToken.None)) Console.WriteLine($"{peer.Name}: {peer.State}");
        return 0;
    }
    Console.Error.WriteLine("Befehle: list | peers [Dongle-Index] | scan [Dongle-Index] | connect/disconnect/unpair Dongle-Index Peer-Index [--confirm]");
    return 2;
}
catch(Exception e) { Console.Error.WriteLine(DeviceSession.FriendlyError(e)); return 1; }
