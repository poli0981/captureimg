using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace CaptureImage.Infrastructure.Capture;

/// <summary>
/// Bridges between the Vortice D3D11 world and the WinRT
/// <see cref="Windows.Graphics.DirectX.Direct3D11"/> projection.
/// </summary>
/// <remarks>
/// WGC demands an <see cref="IDirect3DDevice"/> (a WinRT abstraction) but <c>D3D11CreateDevice</c>
/// gives us a plain <c>ID3D11Device</c>. The two are connected by a pair of C helpers in
/// <c>windows.graphics.directx.direct3d11.interop.h</c> — <c>CreateDirect3D11DeviceFromDXGIDevice</c>
/// and <c>CreateDirect3D11SurfaceFromDXGISurface</c>. We P/Invoke them here and marshal the
/// resulting IInspectable pointers into the managed projections via WinRT runtime helpers.
/// </remarks>
[SupportedOSPlatform("windows10.0.22621.0")]
internal static class Direct3D11Interop
{
    /// <summary>IID for <c>IDirect3DDxgiInterfaceAccess</c> — defined in the same header.</summary>
    private static readonly Guid IidIDirect3DDxgiInterfaceAccess =
        new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

    /// <summary>
    /// Wrap an <see cref="ID3D11Device"/> in a WinRT <see cref="IDirect3DDevice"/> so we can
    /// hand it to <see cref="Windows.Graphics.Capture.Direct3D11CaptureFramePool.CreateFreeThreaded"/>.
    /// </summary>
    public static IDirect3DDevice CreateDirect3DDevice(ID3D11Device d3dDevice)
    {
        ArgumentNullException.ThrowIfNull(d3dDevice);

        using var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
        Marshal.ThrowExceptionForHR(
            CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var inspectablePtr));

        if (inspectablePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("CreateDirect3D11DeviceFromDXGIDevice returned null.");
        }

        try
        {
            return MarshalInspectable<IDirect3DDevice>.FromAbi(inspectablePtr);
        }
        finally
        {
            MarshalInspectable<object>.DisposeAbi(inspectablePtr);
        }
    }

    /// <summary>
    /// Unwrap a WinRT <see cref="Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface"/> to the
    /// underlying Vortice <see cref="ID3D11Texture2D"/>. This is how we get from a captured
    /// frame's surface to something we can copy and map.
    /// </summary>
    /// <remarks>
    /// The surface is a CsWinRT projection — we cannot cast it directly to a
    /// <see cref="ComImportAttribute"/> interface. Instead, fetch the underlying IUnknown
    /// pointer via <see cref="IWinRTObject.NativeObject"/>, then let the CLR's COM interop
    /// wrap it in an RCW and QI for <c>IDirect3DDxgiInterfaceAccess</c>.
    /// </remarks>
    public static ID3D11Texture2D GetTexture2D(IDirect3DSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        if (surface is not IWinRTObject winrtObj)
        {
            throw new InvalidOperationException(
                "IDirect3DSurface is not a CsWinRT-projected object; cannot extract native pointer.");
        }

        var thisPtr = winrtObj.NativeObject.ThisPtr;
        var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(thisPtr);

        var textureIid = typeof(ID3D11Texture2D).GUID;
        access.GetInterface(in textureIid, out var texturePtr);
        if (texturePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("IDirect3DDxgiInterfaceAccess returned a null texture pointer.");
        }

        try
        {
            return new ID3D11Texture2D(texturePtr);
        }
        catch
        {
            Marshal.Release(texturePtr);
            throw;
        }
    }

    [DllImport("d3d11.dll", PreserveSig = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        void GetInterface(in Guid iid, out IntPtr ppv);
    }
}
