namespace CaptureImage.Core.Models;

/// <summary>
/// Image formats supported by CaptureImage. Deliberately limited to the four the plan
/// ratified — adding new formats requires an explicit decision, not an impl detail.
/// </summary>
public enum ImageFormat
{
    /// <summary>PNG (lossless, default).</summary>
    Png,

    /// <summary>JPEG (lossy, smaller files, no alpha).</summary>
    Jpeg,

    /// <summary>WebP (modern, smaller than PNG at comparable quality).</summary>
    Webp,

    /// <summary>TIFF (archival, supports multiple compression modes).</summary>
    Tiff,
}

/// <summary>Extension helpers for <see cref="ImageFormat"/>.</summary>
public static class ImageFormatExtensions
{
    /// <summary>
    /// Canonical lowercase file extension without leading dot, e.g. <c>"png"</c>.
    /// </summary>
    public static string Extension(this ImageFormat format) => format switch
    {
        ImageFormat.Png  => "png",
        ImageFormat.Jpeg => "jpg",
        ImageFormat.Webp => "webp",
        ImageFormat.Tiff => "tiff",
        _ => throw new System.ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// Canonical MIME type, e.g. <c>"image/png"</c>.
    /// </summary>
    public static string MimeType(this ImageFormat format) => format switch
    {
        ImageFormat.Png  => "image/png",
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Webp => "image/webp",
        ImageFormat.Tiff => "image/tiff",
        _ => throw new System.ArgumentOutOfRangeException(nameof(format), format, null),
    };
}
