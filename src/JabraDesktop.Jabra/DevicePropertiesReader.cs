using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;
using Jabra.NET.Sdk.Properties;
using JabraDesktop.Core;

namespace JabraDesktop.Jabra;

internal interface IDevicePropertiesReader
{
    Task InitializeAsync(IApi api, CancellationToken token);
    Task<PropertyValue> GetAsync(IDevice device, string propertyName, CancellationToken token);
    Task<BluetoothNameStatus?> ReadBluetoothNameAsync(IDevice device,CancellationToken token) => Task.FromResult<BluetoothNameStatus?>(null);
    Task<string> SetBluetoothNameAsync(IDevice device,string name,CancellationToken token) =>
        throw new NotSupportedException("Bluetooth name change is not supported.");
}

internal sealed record BluetoothNameStatus(string Name,bool CanWrite);

internal sealed class JabraPropertiesReader : IDevicePropertiesReader
{
    IPropertyFactory? factory;
    readonly SemaphoreSlim initializationGate=new(1,1);

    public async Task InitializeAsync(IApi api,CancellationToken token)
    {
        if(factory!=null) return;
        await initializationGate.WaitAsync(token);
        try
        {
            if(factory!=null) return;
            token.ThrowIfCancellationRequested();
            var module=new PropertyModule(api);
            factory=await module.CreatePropertyFactory();
        }
        finally { initializationGate.Release(); }
    }

    public async Task<PropertyValue> GetAsync(IDevice device,string propertyName,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var currentFactory=factory ?? throw new InvalidOperationException("Jabra device properties are not initialized.");
        var properties=await currentFactory.CreateProperties(device,[propertyName]);
        return await properties[propertyName].Get();
    }

    public async Task<BluetoothNameStatus?> ReadBluetoothNameAsync(IDevice device,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var currentFactory=factory ?? throw new InvalidOperationException("Jabra device properties are not initialized.");
        var properties=await currentFactory.CreateProperties(device,["bluetoothName"]);
        var property=properties["bluetoothName"];
        if(!property.IsReadWrite) return null;
        var value=await property.Get();
        return value is StringPropertyValue && !string.IsNullOrWhiteSpace(value.AsString())
            ? new BluetoothNameStatus(value.AsString().Trim(),true) : null;
    }

    public async Task<string> SetBluetoothNameAsync(IDevice device,string name,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var currentFactory=factory ?? throw new InvalidOperationException("Jabra device properties are not initialized.");
        var properties=await currentFactory.CreateProperties(device,["bluetoothName"]);
        if(!properties["bluetoothName"].IsReadWrite)
            throw new NotSupportedException("Bluetooth name change is not supported by this device.");
        var transaction=properties.StartTransaction();
        transaction.Set("bluetoothName",new StringPropertyValue(name));
        await transaction.Commit();
        try
        {
            var readback=await properties["bluetoothName"].Get();
            if(readback is StringPropertyValue && readback.AsString().Trim()==name) return name;
        }
        catch { /* The write may briefly disconnect the device. Report that confirmation is missing. */ }
        throw new InvalidOperationException(DeviceSession.NameUnconfirmedError);
    }
}
