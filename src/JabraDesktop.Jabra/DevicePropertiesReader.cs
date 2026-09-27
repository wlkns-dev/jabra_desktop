using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;
using Jabra.NET.Sdk.Properties;

namespace JabraDesktop.Jabra;

internal interface IDevicePropertiesReader
{
    Task InitializeAsync(IApi api, CancellationToken token);
    Task<PropertyValue> GetAsync(IDevice device, string propertyName, CancellationToken token);
}

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
}
