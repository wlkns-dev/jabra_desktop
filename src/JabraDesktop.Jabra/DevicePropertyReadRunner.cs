using Jabra.NET.Sdk.Properties;

namespace JabraDesktop.Jabra;

internal sealed class DevicePropertyReadRunner
{
    readonly object gate=new();
    readonly TimeSpan timeout;
    Task<PropertyValue>? activeRead;

    public DevicePropertyReadRunner() : this(TimeSpan.FromSeconds(10)) { }

    internal DevicePropertyReadRunner(TimeSpan timeout)
    {
        this.timeout=timeout;
    }

    public async Task<PropertyValue?> ReadAsync(Func<Task<PropertyValue>> read,CancellationToken token)
    {
        Task<PropertyValue> operation;
        lock(gate)
        {
            if(activeRead is { IsCompleted:false }) return null;
            try { operation=Task.Run(read,CancellationToken.None); }
            catch(Exception) { return null; }
            activeRead=operation;
            _=operation.ContinueWith(completed =>
            {
                _=completed.Exception;
                lock(gate) if(ReferenceEquals(activeRead,completed)) activeRead=null;
            },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        }

        try { return await operation.WaitAsync(timeout,token); }
        catch(Exception) { return null; }
    }
}
