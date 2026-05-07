using System.IO.Pipes;
using System.Runtime.InteropServices;
using CaptureImage.Core.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Serilog;
using WinRT.Interop;

namespace CaptureImage.App.SingleInstance;

/// <summary>
/// Background listener for the single-instance ACTIVATE pipe. When a secondary launch
/// fires <see cref="SingleInstanceGuard.ActivateMessage"/>, this brings the main window
/// back to the foreground — same restore sequence the tray icon uses
/// (<c>AppWindow.Show</c> + <c>ShowWindow(SW_RESTORE)</c> + <c>SetForegroundWindow</c>).
/// </summary>
internal sealed class ActivationListener : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private readonly Window _mainWindow;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private bool _disposed;

    private ActivationListener(Window mainWindow, IUIThreadDispatcher dispatcher)
    {
        _mainWindow = mainWindow;
        _dispatcher = dispatcher;
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>Start listening; returns the listener so it can be disposed on shutdown.</summary>
    public static ActivationListener Start(Window mainWindow, IUIThreadDispatcher dispatcher)
        => new(mainWindow, dispatcher);

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    pipeName: SingleInstanceGuard.PipeName,
                    direction: PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.Equals(line, SingleInstanceGuard.ActivateMessage, StringComparison.Ordinal))
                {
                    _dispatcher.Post(RestoreWindow);
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown — exit cleanly.
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ActivationListener pipe iteration failed; will retry.");
                // Avoid a hot loop if the pipe is in a bad state — yield briefly.
                try { await Task.Delay(500, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private void RestoreWindow()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(_mainWindow);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            // Mirror TrayIconHost.ShowMainWindow: AppWindow.Show un-hides if minimised
            // to tray, ShowWindow(SW_RESTORE) un-minimises a taskbar-minimised window,
            // and SetForegroundWindow forces Z-order to top even if another app has focus.
            appWindow.Show();
            ShowWindow(hwnd, SW_RESTORE);
            _mainWindow.Activate();
            SetForegroundWindow(hwnd);
            Log.Debug("Main window restored from secondary launch ACTIVATE.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to restore main window on ACTIVATE.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); }
        catch { /* swallow */ }
        try { _loop.Wait(TimeSpan.FromSeconds(1)); }
        catch { /* shutdown — best effort */ }
        _cts.Dispose();
    }
}
