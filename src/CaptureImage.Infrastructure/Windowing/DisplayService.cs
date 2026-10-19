// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Runtime.InteropServices;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using Microsoft.Extensions.Logging;
using Vortice.DXGI;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace CaptureImage.Infrastructure.Windowing;

/// <summary>
/// Monitor layout in physical pixels plus each output's HDR state. HDR detection walks every
/// DXGI adapter (hybrid-GPU laptops attach outputs to different adapters) and recreates the
/// DXGI factory when Windows reports it stale (HDR toggled, display plugged in).
/// </summary>
public sealed partial class DisplayService : IDisplayService, IDisposable
{
    private const uint MonitorDefaultToNearest = 2;

    private readonly ILogger<DisplayService> _logger;
    private readonly Lock _gate = new();
    private IDXGIFactory1? _factory;

    public DisplayService(ILogger<DisplayService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var handles = new List<HMONITOR>(4);
        unsafe
        {
            PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, (monitor, _, _, _) =>
            {
                handles.Add(monitor);
                return true;
            }, default);
        }

        var hdr = ReadHdrState();
        var monitors = new List<MonitorInfo>(handles.Count);
        foreach (var handle in handles)
        {
            if (Describe(handle, hdr) is { } monitor)
            {
                monitors.Add(monitor);
            }
        }
        return monitors;
    }

    public MonitorInfo? GetMonitorForWindow(nint windowHandle)
    {
        var monitor = PInvoke.MonitorFromWindow(new HWND(windowHandle), (MONITOR_FROM_FLAGS)MonitorDefaultToNearest);
        return monitor.IsNull ? null : Describe(monitor, ReadHdrState());
    }

    public MonitorInfo? GetMonitorAtCursor()
    {
        if (!PInvoke.GetCursorPos(out var point))
        {
            return null;
        }

        var monitor = PInvoke.MonitorFromPoint(point, (MONITOR_FROM_FLAGS)MonitorDefaultToNearest);
        return monitor.IsNull ? null : Describe(monitor, ReadHdrState());
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _factory?.Dispose();
            _factory = null;
        }
    }

    private static unsafe MonitorInfo? Describe(HMONITOR handle, Dictionary<nint, (bool IsHdr, float MaxNits)> hdrByMonitor)
    {
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (!PInvoke.GetMonitorInfo(handle, (MONITORINFO*)&info))
        {
            return null;
        }

        uint dpi = 96;
        if (PInvoke.GetDpiForMonitor(handle, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded)
        {
            dpi = dpiX;
        }

        var deviceName = info.szDevice.ToString();
        var bounds = ToRect(info.monitorInfo.rcMonitor);
        var hdr = HdrInfo.Sdr;
        if (hdrByMonitor.TryGetValue((nint)handle.Value, out var state) && state.IsHdr)
        {
            hdr = new HdrInfo(true, GetSdrWhiteNits(deviceName), Math.Max(state.MaxNits, HdrInfo.ScRgbReferenceWhiteNits));
        }

        return new MonitorInfo(
            (nint)handle.Value,
            deviceName,
            bounds,
            ToRect(info.monitorInfo.rcWork),
            dpi,
            (info.monitorInfo.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0,
            hdr);
    }

    private static PixelRect ToRect(RECT r) => new(r.left, r.top, r.right - r.left, r.bottom - r.top);

    /// <summary>HMONITOR → (HDR active, peak nits) for every output on every adapter.</summary>
    private Dictionary<nint, (bool IsHdr, float MaxNits)> ReadHdrState()
    {
        var result = new Dictionary<nint, (bool, float)>();
        lock (_gate)
        {
            try
            {
                if (_factory is null || !_factory.IsCurrent)
                {
                    _factory?.Dispose();
                    _factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
                }

                for (uint a = 0; _factory.EnumAdapters1(a, out var adapter).Success; a++)
                {
                    using (adapter)
                    {
                        for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
                        {
                            using (output)
                            {
                                using var output6 = output.QueryInterfaceOrNull<IDXGIOutput6>();
                                if (output6 is null)
                                {
                                    continue;
                                }
                                var desc = output6.Description1;
                                var isHdr = desc.ColorSpace == ColorSpaceType.RgbFullG2084NoneP2020;
                                result[desc.Monitor] = (isHdr, desc.MaxLuminance);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is SharpGen.Runtime.SharpGenException or COMException)
            {
                LogHdrQueryFailed(ex);
            }
        }
        return result;
    }

    /// <summary>
    /// The "SDR content brightness" Windows uses on an HDR display, from
    /// <c>DISPLAYCONFIG_SDR_WHITE_LEVEL</c> (value/1000 × 80 nits). Falls back to 80 nits.
    /// </summary>
    private static unsafe float GetSdrWhiteNits(string gdiDeviceName)
    {
        const uint QdcOnlyActivePaths = 0x2;
        if (PInvoke.GetDisplayConfigBufferSizes((QUERY_DISPLAY_CONFIG_FLAGS)QdcOnlyActivePaths, out var pathCount, out var modeCount) != WIN32_ERROR.ERROR_SUCCESS)
        {
            return HdrInfo.ScRgbReferenceWhiteNits;
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        if (PInvoke.QueryDisplayConfig((QUERY_DISPLAY_CONFIG_FLAGS)QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes) != WIN32_ERROR.ERROR_SUCCESS)
        {
            return HdrInfo.ScRgbReferenceWhiteNits;
        }

        for (var i = 0; i < pathCount; i++)
        {
            var path = paths[i];
            var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            source.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            source.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
            source.header.adapterId = path.sourceInfo.adapterId;
            source.header.id = path.sourceInfo.id;
            if (PInvoke.DisplayConfigGetDeviceInfo(&source.header) != 0
                || !string.Equals(source.viewGdiDeviceName.ToString(), gdiDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var white = new DISPLAYCONFIG_SDR_WHITE_LEVEL();
            white.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL;
            white.header.size = (uint)sizeof(DISPLAYCONFIG_SDR_WHITE_LEVEL);
            white.header.adapterId = path.targetInfo.adapterId;
            white.header.id = path.targetInfo.id;
            if (PInvoke.DisplayConfigGetDeviceInfo(&white.header) == 0 && white.SDRWhiteLevel > 0)
            {
                return white.SDRWhiteLevel / 1000f * HdrInfo.ScRgbReferenceWhiteNits;
            }
        }

        return HdrInfo.ScRgbReferenceWhiteNits;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Querying DXGI outputs for HDR state failed.")]
    private partial void LogHdrQueryFailed(Exception exception);
}
