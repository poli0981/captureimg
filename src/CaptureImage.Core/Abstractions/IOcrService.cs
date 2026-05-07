namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Result of an OCR pass over a captured image.
/// </summary>
/// <param name="Text">Extracted text in reading order. Empty if no glyphs were recognised.</param>
/// <param name="AverageConfidence">0.0–1.0; 0 if no engine was available.</param>
/// <param name="EngineAvailable">
/// <c>false</c> when the OS has no recognizer pack installed for the requested language
/// (and no usable fallback). UIs should disable the action and surface a hint.
/// </param>
public sealed record OcrResult(string Text, double AverageConfidence, bool EngineAvailable);

/// <summary>
/// Offline OCR. Wraps the OS-supplied <c>Windows.Media.Ocr</c> engine on the
/// implementation side; abstracted here so the portable VM layer can stay free of
/// WinRT references and test fakes can stub it.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Recognise text from the given PNG-encoded image. <paramref name="languageTag"/>
    /// is a BCP-47 hint (e.g. <c>en-US</c>); <c>null</c> defers to the user's profile
    /// languages, falling back to en-US, then to <see cref="OcrResult.EngineAvailable"/>=false.
    /// </summary>
    Task<OcrResult> RecognizeAsync(byte[] pngBytes, string? languageTag, CancellationToken cancellationToken = default);
}
