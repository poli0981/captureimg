using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Preview;

/// <summary>
/// VM for the preview window. Holds the captured frame's dimensions + PNG bytes so the view
/// can render it without asking the engine to re-encode.
/// </summary>
public sealed partial class PreviewViewModel : ViewModelBase, IDisposable
{
    private readonly IOcrService _ocr;
    private readonly IClipboardService _clipboard;
    private readonly IToastService _toasts;
    private readonly ILogger<PreviewViewModel>? _logger;
    private bool _disposed;

    public ILocalizationService Localization { get; }

    [ObservableProperty]
    private byte[]? _pngBytes;

    [ObservableProperty]
    private string _targetName = string.Empty;

    [ObservableProperty]
    private int _width;

    [ObservableProperty]
    private int _height;

    [ObservableProperty]
    private bool _isExtractingText;

    public PreviewViewModel(
        ILocalizationService localization,
        IOcrService ocr,
        IClipboardService clipboard,
        IToastService toasts,
        ILogger<PreviewViewModel>? logger = null)
    {
        Localization = localization;
        _ocr = ocr;
        _clipboard = clipboard;
        _toasts = toasts;
        _logger = logger;

        // Preview window is modal and short-lived, but the user can still switch language
        // from the tray menu while it is open. Raise Localization on culture change so the
        // `{Binding Localization[Preview_*]}` indexer bindings in PreviewWindow.axaml
        // re-resolve in place. Matches the v1.1.1 pattern used by every other VM.
        Localization.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            OnPropertyChanged(nameof(Localization));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Localization.PropertyChanged -= OnLocalizationChanged;
    }

    /// <summary>
    /// Fill the VM from a captured frame. The frame is encoded to PNG bytes once so the
    /// WPF-style binding to <see cref="PngBytes"/> works with our existing
    /// <c>BytesToBitmapConverter</c>.
    /// </summary>
    public void SetFrame(CapturedFrame frame, GameTarget target, byte[] pngBytes)
    {
        PngBytes = pngBytes;
        TargetName = target.DisplayName;
        Width = frame.Width;
        Height = frame.Height;
    }

    /// <summary>
    /// Run OCR on the captured PNG and copy the recognised text to the clipboard.
    /// Surfaces a single toast based on the outcome (success / empty / unavailable).
    /// </summary>
    [RelayCommand]
    private async Task ExtractTextAsync()
    {
        if (PngBytes is null || PngBytes.Length == 0 || IsExtractingText) return;

        IsExtractingText = true;
        try
        {
            var result = await _ocr.RecognizeAsync(PngBytes, languageTag: null).ConfigureAwait(true);
            if (!result.EngineAvailable)
            {
                _toasts.ShowError(Localization["Toast_OcrUnavailable"], string.Empty);
                return;
            }

            var text = result.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                _toasts.ShowInfo(Localization["Toast_OcrEmpty"], string.Empty);
                return;
            }

            _clipboard.CopyText(text);
            var wordCount = CountWords(text);
            _logger?.LogInformation(
                "OCR extracted {WordCount} words at avg confidence {Conf:F2}; copied to clipboard.",
                wordCount, result.AverageConfidence);
            _toasts.ShowSuccess(
                string.Format(Localization["Toast_OcrSuccess"], wordCount),
                Truncate(text, 80));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "OCR extraction failed.");
            _toasts.ShowError(Localization["Toast_Error"], ex.Message);
        }
        finally
        {
            IsExtractingText = false;
        }
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var count = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max) return text;
        return text[..max] + "…";
    }
}
