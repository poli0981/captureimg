using System.IO.Pipes;
using Serilog;

namespace CaptureImage.App.SingleInstance;

/// <summary>
/// Per-user single-instance gate. The first process to call <see cref="TryAcquire"/>
/// wins the named mutex and returns a releaser to be held for the lifetime of the
/// app. Any subsequent launch fails the acquire, fires an ACTIVATE message at the
/// existing instance over a named pipe, and exits — the user perceives a restored
/// window instead of a duplicate process.
///
/// Mutex + pipe names are scoped to the local user session so two different OS
/// users on the same machine each get their own primary instance.
/// </summary>
internal static class SingleInstanceGuard
{
    public const string PipeName = "CaptureImage.Activate";
    public const string ActivateMessage = "ACTIVATE";

    private static readonly string MutexName =
        $"Local\\CaptureImage.SingleInstance.{Environment.UserName}";

    /// <summary>
    /// Try to become the primary instance. Returns true and yields a disposable
    /// releaser when this process owns the mutex. Returns false if another instance
    /// already holds it — in that case an ACTIVATE message is fired over the
    /// named pipe (best effort) so the existing instance restores its window.
    /// </summary>
    public static bool TryAcquire(out IDisposable releaser)
    {
        var mutex = new Mutex(initiallyOwned: false, name: MutexName);
        bool acquired;
        try
        {
            // Short timeout — if another instance is alive it should already own the
            // mutex; if it's hung we don't want to block startup waiting for it.
            acquired = mutex.WaitOne(TimeSpan.FromMilliseconds(100), exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            // Previous instance exited without releasing (crash, kill, etc.). The
            // OS hands us ownership anyway — treat as acquired.
            Log.Warning("Single-instance mutex was abandoned by a previous process; taking ownership.");
            acquired = true;
        }

        if (acquired)
        {
            releaser = new MutexReleaser(mutex);
            return true;
        }

        // Best-effort: poke the existing instance to restore its window. Failure
        // here is non-fatal — even if the pipe send fails, the user can still
        // click the tray icon to restore the running instance.
        try
        {
            using var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: PipeName,
                direction: PipeDirection.Out);
            client.Connect(timeout: 1000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(ActivateMessage);
            Log.Information("Existing CaptureImage instance detected — sent ACTIVATE and exiting.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to ping existing instance over named pipe; user must restore manually.");
        }

        mutex.Dispose();
        releaser = NoopReleaser.Instance;
        return false;
    }

    private sealed class MutexReleaser : IDisposable
    {
        private Mutex? _mutex;

        public MutexReleaser(Mutex mutex) => _mutex = mutex;

        public void Dispose()
        {
            if (_mutex is null) return;
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { /* not owned — race on shutdown */ }
            _mutex.Dispose();
            _mutex = null;
        }
    }

    private sealed class NoopReleaser : IDisposable
    {
        public static readonly NoopReleaser Instance = new();
        public void Dispose() { }
    }
}
