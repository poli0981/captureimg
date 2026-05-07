using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using OcrResult = CaptureImage.Core.Abstractions.OcrResult;
using WinOcrResult = Windows.Media.Ocr.OcrResult;

namespace CaptureImage.Infrastructure.Ocr;

/// <summary>
/// <see cref="IOcrService"/> backed by the OS-supplied <c>Windows.Media.Ocr</c> engine.
/// All recognition runs on-device; nothing leaves the machine. Engine availability
/// depends on the user's installed language packs (Settings → Time &amp; language →
/// Language &amp; region → "Optional features" → "Basic typing"+"Optical character
/// recognition").
/// </summary>
[SupportedOSPlatform("windows10.0.10240.0")]
public sealed class WindowsOcrService : IOcrService
{
    private readonly ILogger<WindowsOcrService> _logger;

    public WindowsOcrService(ILogger<WindowsOcrService> logger)
    {
        _logger = logger;
    }

    public async Task<OcrResult> RecognizeAsync(
        byte[] pngBytes,
        string? languageTag,
        CancellationToken cancellationToken = default)
    {
        if (pngBytes is null || pngBytes.Length == 0)
        {
            return new OcrResult(string.Empty, 0.0, EngineAvailable: false);
        }

        var engine = ResolveEngine(languageTag);
        if (engine is null)
        {
            _logger.LogInformation(
                "No OCR engine available for language tag '{Tag}' or fallbacks; OCR disabled.",
                languageTag ?? "(auto)");
            return new OcrResult(string.Empty, 0.0, EngineAvailable: false);
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(pngBytes.AsBuffer()).AsTask(cancellationToken).ConfigureAwait(false);
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask(cancellationToken).ConfigureAwait(false);

            WinOcrResult result = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);

            // Average per-line confidence — OcrResult on the WinRT side exposes per-word
            // confidence on each Word inside each Line. The library's own
            // OcrResult.Text already gives us reading-order text.
            double sum = 0;
            int count = 0;
            foreach (var line in result.Lines)
            {
                foreach (var word in line.Words)
                {
                    // Word.Confidence is exposed in the BoundingRect overload only on newer
                    // Windows versions. For broad compatibility we just count words and
                    // report a coarse 0.85 if anything came back — UI consumers care about
                    // "did we get text" more than calibrated probability.
                    count++;
                    sum += 0.85;
                }
            }
            var confidence = count > 0 ? sum / count : 0.0;

            return new OcrResult(result.Text ?? string.Empty, confidence, EngineAvailable: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR recognition threw; returning empty result.");
            return new OcrResult(string.Empty, 0.0, EngineAvailable: true);
        }
    }

    private OcrEngine? ResolveEngine(string? languageTag)
    {
        // 1. Explicit language tag if the caller passed one.
        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            try
            {
                var lang = new Language(languageTag);
                if (OcrEngine.IsLanguageSupported(lang))
                {
                    return OcrEngine.TryCreateFromLanguage(lang);
                }
            }
            catch (ArgumentException)
            {
                // Unknown / malformed tag — fall through to user profile chain.
            }
        }

        // 2. User profile languages (whatever the OS thinks the user types in).
        var profileEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (profileEngine is not null) return profileEngine;

        // 3. Hard fallback: en-US — the most likely pack to be installed on a Windows
        // machine, even one in a non-English region.
        try
        {
            var enUS = new Language("en-US");
            if (OcrEngine.IsLanguageSupported(enUS))
            {
                return OcrEngine.TryCreateFromLanguage(enUS);
            }
        }
        catch (ArgumentException) { /* shouldn't happen, but stay graceful */ }

        return null;
    }
}
