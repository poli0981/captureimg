// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace CaptureImage.Infrastructure.Capture;

/// <summary>
/// The three COM interop calls Windows.Graphics.Capture needs from a desktop app, made through
/// vtable function pointers so no runtime-callable wrapper is created (v1 leaked one per
/// capture and kept full-size GPU textures alive until the next garbage collection).
/// </summary>
internal static unsafe class WinRtCaptureInterop
{
    private const string GraphicsCaptureItemClass = "Windows.Graphics.Capture.GraphicsCaptureItem";

    private static readonly Guid IidGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid IidGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid IidDxgiInterfaceAccess = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

    /// <summary><c>IGraphicsCaptureItemInterop::CreateForWindow</c>.</summary>
    public static GraphicsCaptureItem CreateItemForWindow(nint windowHandle) => CreateItem(windowHandle, vtableSlot: 3);

    /// <summary><c>IGraphicsCaptureItemInterop::CreateForMonitor</c>.</summary>
    public static GraphicsCaptureItem CreateItemForMonitor(nint monitorHandle) => CreateItem(monitorHandle, vtableSlot: 4);

    /// <summary>Wrap a D3D11 device as a WinRT <see cref="IDirect3DDevice"/> for the frame pool.</summary>
    public static IDirect3DDevice CreateDirect3DDevice(ID3D11Device device)
    {
        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var inspectable));
        try
        {
            return MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    /// <summary>
    /// The <see cref="ID3D11Texture2D"/> behind a WGC frame surface
    /// (<c>IDirect3DDxgiInterfaceAccess::GetInterface</c>). The caller owns the returned texture.
    /// </summary>
    public static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        var surfacePtr = ((IWinRTObject)surface).NativeObject.ThisPtr;
        var iidAccess = IidDxgiInterfaceAccess;
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(surfacePtr, in iidAccess, out var access));
        try
        {
            var getInterface = (delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)(*(void***)access)[3];
            var iidTexture = typeof(ID3D11Texture2D).GUID;
            nint texture;
            Marshal.ThrowExceptionForHR(getInterface(access, &iidTexture, &texture));
            return new ID3D11Texture2D(texture);
        }
        finally
        {
            Marshal.Release(access);
        }
    }

    private static GraphicsCaptureItem CreateItem(nint handle, int vtableSlot)
    {
        using var factory = ActivationFactory.Get(GraphicsCaptureItemClass, IidGraphicsCaptureItemInterop);
        var thisPtr = factory.ThisPtr;
        var create = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)(*(void***)thisPtr)[vtableSlot];
        var iid = IidGraphicsCaptureItem;
        nint item;
        Marshal.ThrowExceptionForHR(create(thisPtr, handle, &iid, &item));
        try
        {
            return GraphicsCaptureItem.FromAbi(item);
        }
        finally
        {
            Marshal.Release(item);
        }
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
}
