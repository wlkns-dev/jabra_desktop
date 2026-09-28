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
    var snapshot=devices;
    var command=args.FirstOrDefault() ?? "list";
    var dongleIndex=0;
    for(var deviceIndex=0;deviceIndex<snapshot.Count;deviceIndex++)
    {
        var device=snapshot[deviceIndex];
        var identity=device.VendorId is > 0 && device.ProductId is > 0
            ? $"VID {device.VendorId.Value:X4} · PID {device.ProductId.Value:X4}"
            : "USB-ID nicht verfügbar";
        var management=device.CanPair ? $"Bluetooth-Verwaltung, Dongle-Index {dongleIndex++}" : "keine Dongle-Verwaltung";
        Console.WriteLine($"Gerät {deviceIndex}: {device.Name} · {device.Role} · {identity} · {management}");
    }
    if(command=="inventory")
    {
        // Read only: do not alter saved pairings or connections during diagnostics.
        for(var pass=0;pass<3;pass++)
        {
            foreach(var dongle in devices.Where(d=>d.CanPair).ToArray())
            {
                var inventory=await backend.GetPeersAsync(dongle.Id,CancellationToken.None);
                Console.WriteLine($"{dongle.Name}: firmware={dongle.Properties?.Firmware ?? "unavailable"}");
                foreach(var peer in inventory)
                    Console.WriteLine($"  {peer.Name}: {peer.State}, battery={peer.Properties?.BatteryPercent?.ToString() ?? "unavailable"}, firmware={peer.Properties?.Firmware ?? "unavailable"}, SDK device={peer.SourceDeviceId is not null}");
            }
            if(pass<2) await Task.Delay(5000);
        }
        return 0;
    }
    if(command=="list") return snapshot.Count>0 ? 0 : 2;
    // IDs are session-local. A CLI invocation selects a dongle by list index (default 0).
    var dongles=snapshot.Where(d=>d.CanPair).ToArray();
    var index=args.Length<=1 ? 0 : int.TryParse(args[1],out var n) ? n : Array.FindIndex(dongles,d=>d.Name==args[1]);
    if(args.Length>1 && !int.TryParse(args[1],out _) && dongles.Count(d=>d.Name==args[1])!=1) return 2;
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
        if(args.Length<3) return 2;
        var p=int.TryParse(args[2],out var peerIndex) ? peerIndex : peers.ToList().FindIndex(peer=>peer.Name==args[2]);
        if(p<0 || p>=peers.Count || (!int.TryParse(args[2],out _) && peers.Count(peer=>peer.Name==args[2])!=1)) return 2;
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
