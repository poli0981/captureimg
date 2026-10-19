// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Models;

/// <summary>Image file formats CaptureImage writes.</summary>
public enum ImageFormat
{
    Png,
    Jpeg,
    Webp,
    Tiff,

    /// <summary>JPEG XR — only used for the HDR sidecar file, not offered as a default format.</summary>
    Jxr,
}

public static class ImageFormatExtensions
{
    /// <summary>Formats the user can pick as the default output format.</summary>
    public static IReadOnlyList<ImageFormat> UserSelectable { get; } =
        [ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Webp, ImageFormat.Tiff];

    /// <summary>File extension without the dot.</summary>
    public static string Extension(this ImageFormat format) => format switch
    {
        ImageFormat.Png => "png",
        ImageFormat.Jpeg => "jpg",
        ImageFormat.Webp => "webp",
        ImageFormat.Tiff => "tiff",
        ImageFormat.Jxr => "jxr",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>Format from a file extension (with or without the dot); null when unknown.</summary>
    public static ImageFormat? FromExtension(string? extension) =>
        extension?.TrimStart('.').ToUpperInvariant() switch
        {
            "PNG" => ImageFormat.Png,
            "JPG" or "JPEG" => ImageFormat.Jpeg,
            "WEBP" => ImageFormat.Webp,
            "TIF" or "TIFF" => ImageFormat.Tiff,
            "JXR" or "WDP" => ImageFormat.Jxr,
            _ => null,
        };
}
