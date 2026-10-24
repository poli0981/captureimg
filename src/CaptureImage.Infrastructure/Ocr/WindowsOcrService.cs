// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using OcrResult = CaptureImage.Core.Abstractions.OcrResult;

namespace CaptureImage.Infrastructure.Ocr;

/// <summary>
/// Offline OCR with Windows.Media.Ocr. The BGRA frame goes straight into a
/// <see cref="SoftwareBitmap"/> (v1 encoded a PNG only to decode it again), frames larger than
/// the engine limit are downscaled, engines are cached per language, and failures are
/// reported as failures instead of "no text found".
/// </summary>
public sealed partial class WindowsOcrService(ILogger<WindowsOcrService> logger) : IOcrService
{
    private readonly ILogger<WindowsOcrService> _logger = logger;
    private readonly ConcurrentDictionary<string, OcrEngine?> _engines = new(StringComparer.OrdinalIgnoreCase);

    public async Task<OcrResult> RecognizeAsync(CapturedFrame frame, string? languageTag, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Format != FramePixelFormat.Bgra8 || frame.Width == 0 || frame.Height == 0)
        {
            return OcrResult.Failed;
        }

        var engine = _engines.GetOrAdd(languageTag ?? string.Empty, CreateEngine);
        if (engine is null)
        {
            LogNoEngine(languageTag ?? "(user languages)");
            return new OcrResult(OcrStatus.EngineUnavailable, string.Empty);
        }

        try
        {
            var input = Downscale(frame, (int)OcrEngine.MaxImageDimension);
            using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
                input.ToTightlyPacked().AsBuffer(),
                BitmapPixelFormat.Bgra8,
                input.Width,
                input.Height,
                BitmapAlphaMode.Ignore);
            var result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken).ConfigureAwait(false);
            var text = result.Text?.Trim() ?? string.Empty;
            return new OcrResult(text.Length == 0 ? OcrStatus.NoText : OcrStatus.Success, text);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            LogFailed(ex);
            return OcrResult.Failed;
        }
    }

    private static OcrEngine? CreateEngine(string languageTag)
    {
        if (!string.IsNullOrWhiteSpace(languageTag) && Language.IsWellFormed(languageTag))
        {
            var language = new Language(languageTag);
            if (OcrEngine.IsLanguageSupported(language))
            {
                return OcrEngine.TryCreateFromLanguage(language);
            }
        }

        return OcrEngine.TryCreateFromUserProfileLanguages()
            ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"));
    }

    /// <summary>Box-filter downscale so the longest edge fits the OCR engine's limit.</summary>
    private static CapturedFrame Downscale(CapturedFrame frame, int maxDimension)
    {
        var longest = Math.Max(frame.Width, frame.Height);
        if (longest <= maxDimension)
        {
            return frame;
        }

        var factor = (int)Math.Ceiling(longest / (double)maxDimension);
        var width = frame.Width / factor;
        var height = frame.Height / factor;
        var output = new byte[width * height * 4];
        var samples = factor * factor;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int b = 0, g = 0, r = 0;
                for (var dy = 0; dy < factor; dy++)
                {
                    var row = ((y * factor) + dy) * frame.RowStride;
                    for (var dx = 0; dx < factor; dx++)
                    {
                        var i = row + (((x * factor) + dx) * 4);
                        b += frame.Pixels[i];
                        g += frame.Pixels[i + 1];
                        r += frame.Pixels[i + 2];
                    }
                }
                var o = ((y * width) + x) * 4;
                output[o] = (byte)(b / samples);
                output[o + 1] = (byte)(g / samples);
                output[o + 2] = (byte)(r / samples);
                output[o + 3] = 0xFF;
            }
        }
        return new CapturedFrame(width, height, width * 4, output);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "No OCR language is installed for {Language}.")]
    private partial void LogNoEngine(string language);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Text recognition failed.")]
    private partial void LogFailed(Exception exception);
}
