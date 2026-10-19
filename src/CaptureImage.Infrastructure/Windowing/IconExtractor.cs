// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Windowing;

/// <summary>
/// Extracts an executable's icon as PNG bytes. Results (including failures) are cached per
/// path in a bounded least-recently-used cache, so long sessions don't grow without limit.
/// </summary>
public sealed partial class IconExtractor
{
    private const int Capacity = 256;

    private readonly ILogger<IconExtractor> _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, LinkedListNode<(string Path, byte[]? Png)>> _index = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<(string Path, byte[]? Png)> _order = new();

    public IconExtractor(ILogger<IconExtractor> logger)
    {
        _logger = logger;
    }

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

    private byte[]? Extract(string executablePath)
    {
        try
        {
            if (!File.Exists(executablePath))
            {
                return null;
            }

            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null)
            {
                return null;
            }

            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or ExternalException)
        {
            LogExtractFailed(ex, Path.GetFileName(executablePath));
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Icon extraction failed for {Executable}.")]
    private partial void LogExtractFailed(Exception exception, string executable);
}
