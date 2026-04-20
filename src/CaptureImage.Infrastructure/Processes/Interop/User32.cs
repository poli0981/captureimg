using System.Runtime.InteropServices;
using System.Text;

namespace CaptureImage.Infrastructure.Processes.Interop;

/// <summary>
/// Minimal Win32 surface used by <see cref="WindowEnumerator"/>. Only the calls we actually
/// make land here — do not grow this file into a general user32 wrapper.
/// </summary>
internal static partial class User32
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW", SetLastError = true)]
    public static partial int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    // GetWindowLongPtr indices
    public const int GWL_EXSTYLE = -20;

    // Extended window styles
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_APPWINDOW  = 0x00040000;
    public const long WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

    // GetWindow constants
    public const uint GW_OWNER = 4;
}
