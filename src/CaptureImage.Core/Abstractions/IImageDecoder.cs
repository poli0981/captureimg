// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>Loads saved images back into frames (library: copy, OCR, pin).</summary>
public interface IImageDecoder
{
    /// <summary>
    /// Decode <paramref name="path"/> into an opaque 8-bit frame. With
    /// <paramref name="maxDimension"/> &gt; 0 the image is downscaled so its long edge fits.
    /// Returns <c>null</c> when the file is missing or not a readable image.
    /// </summary>
    Task<CapturedFrame?> LoadAsync(string path, int maxDimension = 0, CancellationToken cancellationToken = default);
}
