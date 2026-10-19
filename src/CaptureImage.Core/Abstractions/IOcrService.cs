// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

public enum OcrStatus
{
    /// <summary>Text was recognized.</summary>
    Success,

    /// <summary>The engine ran but found no text.</summary>
    NoText,

    /// <summary>No OCR language pack is installed for the requested or user languages.</summary>
    EngineUnavailable,

    /// <summary>Recognition failed.</summary>
    Failed,
}

public sealed record OcrResult(OcrStatus Status, string Text)
{
    public static OcrResult Failed { get; } = new(OcrStatus.Failed, string.Empty);
}

/// <summary>Offline text recognition (Windows.Media.Ocr).</summary>
public interface IOcrService
{
    /// <summary>Recognize text in an 8-bit frame; large frames are downscaled to the engine's limit.</summary>
    Task<OcrResult> RecognizeAsync(CapturedFrame frame, string? languageTag, CancellationToken cancellationToken = default);
}
