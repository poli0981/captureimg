// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Library;

/// <summary>One saved capture, as listed in the capture library.</summary>
/// <param name="FilePath">Absolute path of the saved image.</param>
/// <param name="CapturedAt">Local time the frame was captured.</param>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="Format">Image format of <paramref name="FilePath"/>.</param>
/// <param name="SizeBytes">File size at save time.</param>
/// <param name="Mode">How the image was captured.</param>
/// <param name="Game">Game / window name the capture was associated with, if any.</param>
/// <param name="HdrFilePath">Path of the JPEG XR HDR sidecar, when one was written.</param>
public sealed record CaptureRecord(
    string FilePath,
    DateTimeOffset CapturedAt,
    int Width,
    int Height,
    ImageFormat Format,
    long SizeBytes,
    CaptureMode Mode,
    string? Game,
    string? HdrFilePath = null)
{
    public string FileName => Path.GetFileName(FilePath);

    public bool HasHdr => !string.IsNullOrEmpty(HdrFilePath);
}
