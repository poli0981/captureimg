using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Encodes a <see cref="CapturedFrame"/> to a byte stream in a target <see cref="ImageFormat"/>.
/// Encoders may be format-specialized (SkiaSharp for PNG/JPEG/WebP, ImageSharp for TIFF);
/// the dispatcher picks the right one per request.
/// </summary>
public interface IImageEncoder
{
    /// <summary>True if this encoder can handle <paramref name="format"/>.</summary>
    bool Supports(ImageFormat format);

    /// <summary>
    /// Encode <paramref name="frame"/> into <paramref name="destination"/>.
    /// </summary>
    /// <param name="frame">Raw BGRA source frame.</param>
    /// <param name="format">Target format. Must be supported by this encoder.</param>
    /// <param name="jpegQuality">1-100, used only when format is JPEG.</param>
    /// <param name="webpQuality">1-100, used only when format is WebP.</param>
    /// <param name="destination">Writable stream; flushed but not closed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EncodeAsync(
        CapturedFrame frame,
        ImageFormat format,
        int jpegQuality,
        int webpQuality,
        Stream destination,
        CancellationToken cancellationToken = default);
}
