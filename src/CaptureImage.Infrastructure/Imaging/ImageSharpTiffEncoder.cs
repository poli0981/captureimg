using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace CaptureImage.Infrastructure.Imaging;

/// <summary>
/// TIFF-only encoder powered by <see cref="SixLabors.ImageSharp"/>. SkiaSharp cannot emit TIFF,
/// so this is the only code path that pulls ImageSharp into the runtime. Keep the dependency
/// surface narrow — don't grow this class into a general "ImageSharp encoder".
/// </summary>
public sealed class ImageSharpTiffEncoder : IImageEncoder
{
    public bool Supports(ImageFormat format) => format == ImageFormat.Tiff;

    public async Task EncodeAsync(
        CapturedFrame frame,
        ImageFormat format,
        int jpegQuality,
        int webpQuality,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        if (format != ImageFormat.Tiff)
        {
            throw new CaptureException(CaptureError.EncodingFailure,
                $"ImageSharpTiffEncoder does not support {format}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ImageSharp needs a tightly-packed buffer (width*4 == stride).
        var tight = frame.IsTightlyPacked ? frame.BgraPixels : frame.ToTightlyPacked();

        using var image = Image.LoadPixelData<Bgra32>(tight, frame.Width, frame.Height);

        // Default to LZW compression — lossless, widely supported, much smaller than raw.
        var encoder = new TiffEncoder
        {
            Compression = TiffCompression.Lzw,
            BitsPerPixel = TiffBitsPerPixel.Bit32,
        };

        await image.SaveAsTiffAsync(destination, encoder, cancellationToken).ConfigureAwait(false);
    }
}
