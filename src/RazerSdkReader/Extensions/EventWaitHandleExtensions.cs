using System.Threading;
using System.Threading.Tasks;

namespace RazerSdkReader.Extensions;

internal static class EventWaitHandleExtensions
{
    public static ValueTask<bool> WaitOneAsync(this EventWaitHandle handle, int timeoutMs, CancellationToken cancellationToken = default)
    {
        // Check if already signaled without blocking.
        if (handle.WaitOne(0))
            return ValueTask.FromResult(true);

        // Register async wait with the caller's timeout so the ReadLoop gets a
        // guaranteed polling fallback even when the event is never signaled (e.g.
        // legacy Chroma SDK games that write directly to shared memory without
        // signaling the update event handle).
        var tcs = new TaskCompletionSource<bool>();
        var threadPoolRegistration = ThreadPool.RegisterWaitForSingleObject(
            handle,
            static (state, timedOut) => ((TaskCompletionSource<bool>)state!).TrySetResult(!timedOut),
            tcs,
            timeoutMs,
            true);
        cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
        tcs.Task.ContinueWith(_ => threadPoolRegistration.Unregister(null), TaskScheduler.Default);
        return new ValueTask<bool>(tcs.Task);
    }
}