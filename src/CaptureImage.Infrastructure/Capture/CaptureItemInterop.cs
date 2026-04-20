using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Graphics.Capture;
using WinRT;

namespace CaptureImage.Infrastructure.Capture;

/// <summary>
/// Bridge from a Win32 <c>HWND</c> to a WinRT <see cref="GraphicsCaptureItem"/>, using
/// <c>IGraphicsCaptureItemInterop</c> — the interop shim Microsoft defines for callers that
/// don't live inside the WinAppSDK activation world.
/// </summary>
/// <remarks>
/// The interface IID and method layout come from <c>windows.graphics.capture.interop.h</c>
/// in the Windows SDK. We obtain the GraphicsCaptureItem activation factory via
/// <c>RoGetActivationFactory</c> directly, then call <c>CreateForWindow</c> and marshal the
/// returned IInspectable pointer into the managed projection via
/// <see cref="MarshalInspectable{T}"/>.
/// </remarks>
[SupportedOSPlatform("windows10.0.22621.0")]
internal static class CaptureItemInterop
{
    /// <summary>
    /// IID for <c>IGraphicsCaptureItemInterop</c>. Stable across Win10 1803+ / Win11.
    /// Source: <c>windows.graphics.capture.interop.h</c>.
    /// </summary>
    private static readonly Guid IidIGraphicsCaptureItemInterop =
        new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

    /// <summary>
    /// IID for <c>IGraphicsCaptureItem</c> — the COM interface returned by
    /// <c>CreateForWindow</c>. Source: <c>windows.graphics.capture.h</c>.
    /// </summary>
    private static readonly Guid IidIGraphicsCaptureItem =
        new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    private const string GraphicsCaptureItemRuntimeClass =
        "Windows.Graphics.Capture.GraphicsCaptureItem";

    /// <summary>
    /// Create a <see cref="GraphicsCaptureItem"/> from the given top-level HWND.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the HWND is zero or the interop call fails (e.g. the window is protected
    /// or has been destroyed).
    /// </exception>
    public static GraphicsCaptureItem CreateForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("CreateForWindow: hwnd must be non-zero.");
        }

        // 1. Wrap the runtime class name in an HSTRING — RoGetActivationFactory insists on
        //    the Windows.Runtime string type, not a classic LPWSTR.
        Marshal.ThrowExceptionForHR(
            WindowsCreateString(
                GraphicsCaptureItemRuntimeClass,
                GraphicsCaptureItemRuntimeClass.Length,
                out var classIdHstring));

        IntPtr factoryPtr;
        try
        {
            var iid = IidIGraphicsCaptureItemInterop;
            Marshal.ThrowExceptionForHR(
                RoGetActivationFactory(classIdHstring, in iid, out factoryPtr));
        }
        finally
        {
            WindowsDeleteString(classIdHstring);
        }

        if (factoryPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "RoGetActivationFactory returned a null pointer for " + GraphicsCaptureItemRuntimeClass + ".");
        }

        try
        {
            var factory = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);

            // 2. Call CreateForWindow with the IGraphicsCaptureItem IID so we get back an
            //    IInspectable* pointing at the managed projection's ABI layout.
            var itemIid = IidIGraphicsCaptureItem;
            var itemAbi = factory.CreateForWindow(hwnd, in itemIid);

            try
            {
                return MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemAbi);
            }
            finally
            {
                MarshalInspectable<object>.DisposeAbi(itemAbi);
            }
        }
        finally
        {
            Marshal.Release(factoryPtr);
        }
    }

    [DllImport("combase.dll", PreserveSig = true, CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        int length,
        out IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        IntPtr activatableClassId,
        in Guid iid,
        out IntPtr factory);

    /// <summary>
    /// <c>IGraphicsCaptureItemInterop</c> projection. Declared as a classic
    /// <see cref="ComImportAttribute"/> IUnknown interface — CLR wires the vtable,
    /// we just list the methods in vtable order.
    /// </summary>
    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, in Guid iid);

        IntPtr CreateForMonitor(IntPtr monitor, in Guid iid);
    }
}
