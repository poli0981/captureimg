using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Processes;

/// <summary>
/// Extracts the associated icon for an executable and encodes it as PNG bytes.
/// Cached by (case-insensitive) executable path so repeated calls for the same
/// path reuse the work.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class IconExtractor
{
    private readonly ILogger<IconExtractor> _logger;
    private readonly ConcurrentDictionary<string, byte[]?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public IconExtractor(ILogger<IconExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Return PNG bytes for the executable's icon, or <c>null</c> if extraction fails
    /// (missing file, permissions, no embedded icon, etc.). Never throws for normal
    /// failure modes.
    /// </summary>
    public byte[]? TryGetIconPngBytes(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;

        return _cache.GetOrAdd(executablePath, Extract);
    }

    private byte[]? Extract(string executablePath)
    {
        try
        {
            if (!File.Exists(executablePath)) return null;

            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null) return null;

            // Convert HICON -> Bitmap -> PNG stream -> byte[]
            using var bitmap = icon.ToBitmap();
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Png);
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Icon extraction failed for {Path}.", executablePath);
            return null;
        }
    }
}
