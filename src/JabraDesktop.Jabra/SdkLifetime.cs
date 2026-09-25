namespace JabraDesktop.Jabra;

/// <summary>Report slow SDK work without pretending a local timeout cancelled native work.</summary>
public static class SdkLifetime
{
    public static async Task AwaitCompletion(Task task,TimeSpan timeout,Action onTimeout)
    {
        try { await task.WaitAsync(timeout); }
        catch(TimeoutException) when(!task.IsCompleted)
        {
            onTimeout();
            await task; // Caller retains its device lease until the native task really settles.
        }
    }
}
public sealed class RetryableInitialization<T>
{
    readonly object gate=new();
    Task<T>? task;
    public Task<T> Run(Func<Task<T>> factory,bool retryCompleted=false)
    {
        lock(gate)
        {
            if(task==null || task.IsFaulted || task.IsCanceled || retryCompleted && task.IsCompleted)
            {
                try { task=factory(); }
                catch(Exception e) { task=Task.FromException<T>(e); }
            }
            return task;
        }
    }
}
/// <summary>Capture and deliver snapshots in one serialized order.</summary>
public sealed class SnapshotPublisher<T>
{
    readonly object gate=new();
    public void Publish(Func<T> snapshot,Action<T> consume)
    { lock(gate) consume(snapshot()); }
}
