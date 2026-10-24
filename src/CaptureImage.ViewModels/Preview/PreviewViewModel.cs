// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Library;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptureImage.ViewModels.Preview;

/// <summary>
/// "Save this capture?" window. <see cref="Result"/> completes with <c>true</c> (save) or
/// <c>false</c> (discard — also when the window is closed).
/// </summary>
public sealed partial class PreviewViewModel(
    CapturedFrame frame,
    string title,
    IOcrService ocr,
    IClipboardService clipboard,
    IToastService toasts,
    ILocalizationService localization) : ViewModelBase
{
    private readonly IOcrService _ocr = ocr;
    private readonly IClipboardService _clipboard = clipboard;
    private readonly IToastService _toasts = toasts;
    private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ILocalizationService Localization { get; } = localization;

    public CapturedFrame Frame { get; } = frame;

    public string Title { get; } = title;

    public string Dimensions => $"{Frame.Width} × {Frame.Height}";

    public Task<bool> Result => _result.Task;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExtractTextCommand))]
    public partial bool IsRecognizing { get; set; }

    [RelayCommand]
    private void Save() => _result.TrySetResult(true);

    [RelayCommand]
    private void Discard() => _result.TrySetResult(false);

    [RelayCommand]
    private async Task CopyImageAsync()
    {
        if (await _clipboard.CopyImageAsync(Frame).ConfigureAwait(true))
        {
            _toasts.ShowSuccess(Localization["Toast_ClipboardCopied"], Title);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExtractText))]
    private async Task ExtractTextAsync()
    {
        IsRecognizing = true;
        try
        {
            var result = await _ocr.RecognizeAsync(Frame, Localization.CurrentCulture.Name).ConfigureAwait(true);
            await OcrFeedback.ReportAsync(result, _clipboard, _toasts, Localization).ConfigureAwait(true);
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    private bool CanExtractText() => !IsRecognizing;
}
