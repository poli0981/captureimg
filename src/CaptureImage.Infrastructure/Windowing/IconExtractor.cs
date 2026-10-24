// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using Microsoft.Extensions.Logging;
using SharpGen.Runtime;
using Vortice.WIC;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureImage.Infrastructure.Windowing;

/// <summary>
/// Extracts an executable's icon as PNG bytes: its own 48 px icon, or the shell's icon for the
/// file when it has none. Results (including failures) are cached per path in a bounded
/// least-recently-used cache, so long sessions don't grow without limit.
/// </summary>
public sealed partial class IconExtractor(ILogger<IconExtractor> logger) : IDisposable
{
    private const int Capacity = 256;
    private const uint IconSize = 48;

    private readonly ILogger<IconExtractor> _logger = logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, LinkedListNode<(string Path, byte[]? Png)>> _index = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<(string Path, byte[]? Png)> _order = new();
    private readonly Lazy<IWICImagingFactory> _factory = new(() => new IWICImagingFactory(), LazyThreadSafetyMode.ExecutionAndPublication);

    public byte[]? TryGetIconPngBytes(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        lock (_gate)
        {
            if (_index.TryGetValue(executablePath, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                return node.Value.Png;
            }
        }

        var png = Extract(executablePath);
        lock (_gate)
        {
            if (!_index.ContainsKey(executablePath))
            {
                _index[executablePath] = _order.AddFirst((executablePath, png));
                if (_order.Count > Capacity && _order.Last is { } last)
                {
                    _index.Remove(last.Value.Path);
                    _order.RemoveLast();
                }
            }
        }
        return png;
    }

    public void Dispose()
    {
        if (_factory.IsValueCreated)
        {
            _factory.Value.Dispose();
        }
    }

    private byte[]? Extract(string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            return null;
        }

        var icon = LoadIcon(executablePath);
        if (icon.IsNull)
        {
            return null;
        }

        try
        {
            return EncodePng(icon);
        }
        catch (SharpGenException ex)
        {
            LogExtractFailed(ex, Path.GetFileName(executablePath));
            return null;
        }
        finally
        {
            PInvoke.DestroyIcon(icon);
        }
    }

    private static unsafe HICON LoadIcon(string path)
    {
        HICON large = default;
        fixed (char* file = path)
        {
            // S_FALSE (the file has no icons) leaves the handle null and falls through.
            if (PInvoke.SHDefExtractIcon(file, 0, 0, &large, null, IconSize).Succeeded && !large.IsNull)
            {
                return large;
            }
        }

        SHFILEINFOW info = default;
        var found = PInvoke.SHGetFileInfo(path, FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL, ref info, SHGFI_FLAGS.SHGFI_ICON | SHGFI_FLAGS.SHGFI_LARGEICON);
        return found != 0 ? info.hIcon : default;
    }

    private byte[] EncodePng(HICON icon)
    {
        using var buffer = new MemoryStream();
        lock (_factory)
        {
            var factory = _factory.Value;
            using var bitmap = factory.CreateBitmapFromHICON(icon);
            using var encoder = factory.CreateEncoder(ContainerFormat.Png, buffer, BitmapEncoderCacheOption.NoCache);
            using var frame = encoder.CreateNewFrame(out var options);
            using (options)
            {
                frame.Initialize(options);
            }

            frame.WriteSource(bitmap);
            frame.Commit();
            encoder.Commit();
        }
        return buffer.ToArray();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Icon extraction failed for {Executable}.")]
    private partial void LogExtractFailed(Exception exception, string executable);
}
