using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Hotkeys;

/// <summary>
/// Windows implementation of <see cref="IHotkeyConflictSniffer"/>. Uses
/// <c>RegisterHotKey</c> with <c>HWND=NULL</c> — per Win32 docs that posts
/// <c>WM_HOTKEY</c> to the calling thread's queue, which is fine here because
/// we release the registration immediately.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="HotkeyModifiers"/> flag values deliberately match the Win32
/// <c>MOD_*</c> constants so no re-mapping is needed. Bindings flagged by
/// <see cref="Validation.ReservedHotkeys"/> often still register successfully
/// here (Windows gets to eat the keypress first), so both checks are needed.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed partial class HotkeyConflictSniffer : IHotkeyConflictSniffer
{
    // Start well clear of the 0x0000-0xBFFF range used by application-defined
    // hotkeys so accidental collision with another part of the process is unlikely.
    private static int _nextId = 0x7100;

    private readonly ILogger<HotkeyConflictSniffer> _logger;

    public HotkeyConflictSniffer(ILogger<HotkeyConflictSniffer> logger)
    {
        _logger = logger;
    }

    public bool IsConflicted(HotkeyBinding binding)
    {
        if (!binding.IsValid())
        {
            // Invalid bindings can't conflict with anything meaningful.
            return false;
        }

        var id = Interlocked.Increment(ref _nextId);
        var mods = (uint)binding.Modifiers;

        if (RegisterHotKey(IntPtr.Zero, id, mods, binding.VirtualKey))
        {
            UnregisterHotKey(IntPtr.Zero, id);
            return false;
        }

        var err = Marshal.GetLastWin32Error();
        _logger.LogDebug(
            "RegisterHotKey sniff failed for {Binding}; treating as conflicted. ({ErrCode}: {ErrText})",
            binding,
            err,
            DescribeWin32Error(err));
        return true;
    }

    /// <summary>
    /// Map the handful of Win32 errors <c>RegisterHotKey</c> can realistically produce back
    /// to human-readable names. Unknown codes fall through to <c>"UNKNOWN"</c> — the
    /// numeric value is always logged alongside so diagnosis isn't blocked on the map
    /// having complete coverage.
    /// </summary>
    private static string DescribeWin32Error(int code) => code switch
    {
        0    => "ERROR_SUCCESS",
        5    => "ERROR_ACCESS_DENIED",
        87   => "ERROR_INVALID_PARAMETER",
        1400 => "ERROR_INVALID_WINDOW_HANDLE",
        1409 => "ERROR_HOTKEY_ALREADY_REGISTERED",
        1419 => "ERROR_HOTKEY_NOT_REGISTERED",
        _    => "UNKNOWN",
    };

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
