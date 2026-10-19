// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace CaptureImage.App;

/// <summary>
/// One running copy per user session and instance id. A second launch asks the first, over a
/// pipe only the same user can open, to show its window, then exits. The names differ from
/// v1, so a v1 copy that is still running doesn't swallow the request.
/// </summary>
internal sealed partial class SingleInstance : IDisposable
{
    private const byte ShowCommand = 0x01;
    private const int AsfwAny = -1;

    private readonly string _name;
    private Mutex? _mutex;
    private CancellationTokenSource? _listening;

    public SingleInstance(string instanceId)
    {
        using var identity = WindowsIdentity.GetCurrent();
        using var process = Process.GetCurrentProcess();
        var user = identity.User?.Value ?? Environment.UserName;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user)))[..12];
        _name = $"CaptureImage.v2.{instanceId}.{process.SessionId}.{hash}";
    }

    /// <summary>Raised on a background thread when another launch asks for the window.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>True when this is the first copy.</summary>
    public bool TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, @"Local\" + _name, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }
        _mutex = mutex;
        return true;
    }

    /// <summary>Ask the first copy to show its window (best effort).</summary>
    public void SignalExisting()
    {
        // The first copy is a background process; let it take the foreground for this request.
        AllowSetForegroundWindow(AsfwAny);
        try
        {
            using var client = new NamedPipeClientStream(".", _name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(TimeSpan.FromSeconds(2));
            client.WriteByte(ShowCommand);
            client.Flush();
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // The other copy is starting up or shutting down; nothing more to do.
        }
    }

    public void StartListening()
    {
        _listening = new CancellationTokenSource();
        var token = _listening.Token;
        _ = Task.Run(() => ListenAsync(token), token);
    }

    public void Dispose()
    {
        _listening?.Cancel();
        _listening?.Dispose();
        if (_mutex is not null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _name,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                if (await server.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) == 1 && buffer[0] == ShowCommand)
                {
                    ShowRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A client disconnected mid-message; wait a moment and listen again.
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // The BOOL result is ignored: when the call isn't allowed the window simply flashes instead.
    [LibraryImport("user32.dll")]
    private static partial void AllowSetForegroundWindow(int processId);
}
