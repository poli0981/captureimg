// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Library;

/// <summary>One saved capture in the Library grid. The thumbnail loads the first time the view asks for it.</summary>
public sealed partial class CaptureItemViewModel(CaptureRecord record, ICaptureLibrary library) : ViewModelBase
{
    private const int ThumbnailSize = 320;
    private readonly ICaptureLibrary _library = library;
    private bool _thumbnailRequested;
    private string? _thumbnailPath;

    public CaptureRecord Record { get; } = record;

    public string FileName => Record.FileName;

    public string Game => Record.Game ?? string.Empty;

    public bool HasHdr => Record.HasHdr;

    public string Dimensions => Record.Width > 0 ? $"{Record.Width} × {Record.Height}" : string.Empty;

    public string FormatName => Record.Format.Extension().ToUpperInvariant();

    public string CapturedAtText => Record.CapturedAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);

    public string SizeText => Record.SizeBytes >= 1024 * 1024
        ? (Record.SizeBytes / 1024d / 1024d).ToString("0.0 'MB'", CultureInfo.CurrentCulture)
        : (Record.SizeBytes / 1024d).ToString("0 'KB'", CultureInfo.CurrentCulture);

    /// <summary>Path of the cached JPEG thumbnail; <c>null</c> until it has been generated.</summary>
    public string? ThumbnailPath
    {
        get
        {
            if (!_thumbnailRequested)
            {
                _thumbnailRequested = true;
                _ = LoadThumbnailAsync();
            }
            return _thumbnailPath;
        }
    }

    public override string ToString() => FileName;

    private async Task LoadThumbnailAsync()
    {
        var path = await _library.GetThumbnailAsync(Record, ThumbnailSize).ConfigureAwait(true);
        if (path is not null)
        {
            _thumbnailPath = path;
            OnPropertyChanged(nameof(ThumbnailPath));
        }
    }
}

/// <summary>
/// Library page: every capture made with CaptureImage (and images already in the output
/// folders), filterable by game and format, with open / reveal / copy / OCR / pin / delete.
/// </summary>
public sealed partial class LibraryViewModel : ViewModelBase, IDisposable
{
    private readonly ICaptureLibrary _library;
    private readonly IImageDecoder _decoder;
    private readonly IShellService _shell;
    private readonly IClipboardService _clipboard;
    private readonly IOcrService _ocr;
    private readonly IPinnedThumbnailHost _pinned;
    private readonly IToastService _toasts;
    private readonly IDialogService _dialogs;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ILogger<LibraryViewModel> _logger;
    private List<CaptureItemViewModel> _all = [];
    private bool _isPageActive;
    private bool _stale = true;
    private bool _disposed;

    public LibraryViewModel(
        ICaptureLibrary library,
        IImageDecoder decoder,
        IShellService shell,
        IClipboardService clipboard,
        IOcrService ocr,
        IPinnedThumbnailHost pinned,
        IToastService toasts,
        IDialogService dialogs,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        ILogger<LibraryViewModel> logger)
    {
        _library = library;
        _decoder = decoder;
        _shell = shell;
        _clipboard = clipboard;
        _ocr = ocr;
        _pinned = pinned;
        _toasts = toasts;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        Localization = localization;
        _logger = logger;
        _library.Changed += OnLibraryChanged;
        Localization.PropertyChanged += OnLocalizationChanged;
    }

    public ILocalizationService Localization { get; }

    public ObservableCollection<CaptureItemViewModel> Items { get; } = [];

    /// <summary>Game filter choices; the first entry means "all games".</summary>
    public ObservableCollection<string> Games { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedGame { get; set; }

    [ObservableProperty]
    public partial bool HdrOnly { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand), nameof(RevealCommand), nameof(CopyImageCommand), nameof(CopyPathCommand),
        nameof(ExtractTextCommand), nameof(PinCommand), nameof(DeleteCommand), nameof(OpenHdrCommand))]
    public partial CaptureItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    public bool IsEmpty => !IsLoading && Items.Count == 0;

    public bool IsPageActive
    {
        get => _isPageActive;
        set
        {
            _isPageActive = value;
            if (value && _stale)
            {
                _ = RefreshAsync();
            }
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = Items.Count == 0;
        try
        {
            var records = await _library.GetAllAsync().ConfigureAwait(true);
            var previous = SelectedItem?.Record.FilePath;
            _all = [.. records.Select(r => new CaptureItemViewModel(r, _library))];
            RebuildGames();
            ApplyFilter();
            SelectedItem = Items.FirstOrDefault(i => i.Record.FilePath == previous);
            _stale = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            LogRefreshFailed(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Open() => _shell.Open(SelectedItem!.Record.FilePath);

    [RelayCommand(CanExecute = nameof(HasHdrSelection))]
    private void OpenHdr() => _shell.Open(SelectedItem!.Record.HdrFilePath!);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Reveal() => _shell.RevealInExplorer(SelectedItem!.Record.FilePath);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task CopyImageAsync()
    {
        var frame = await _decoder.LoadAsync(SelectedItem!.Record.FilePath).ConfigureAwait(true);
        if (frame is not null && await _clipboard.CopyImageAsync(frame).ConfigureAwait(true))
        {
            _toasts.ShowSuccess(Localization["Toast_ClipboardCopied"], SelectedItem.FileName);
        }
        else
        {
            _toasts.ShowError(Localization["Toast_Error"], Localization["Library_CopyFailed"]);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task CopyPathAsync()
    {
        if (await _clipboard.CopyTextAsync(SelectedItem!.Record.FilePath).ConfigureAwait(true))
        {
            _toasts.ShowSuccess(Localization["Toast_PathCopied"], SelectedItem.FileName);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ExtractTextAsync()
    {
        var frame = await _decoder.LoadAsync(SelectedItem!.Record.FilePath).ConfigureAwait(true);
        if (frame is null)
        {
            _toasts.ShowError(Localization["Toast_Error"], Localization["Library_CopyFailed"]);
            return;
        }

        var result = await _ocr.RecognizeAsync(frame, Localization.CurrentCulture.Name).ConfigureAwait(true);
        await OcrFeedback.ReportAsync(result, _clipboard, _toasts, Localization).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task PinAsync()
    {
        var item = SelectedItem!;
        var frame = await _decoder.LoadAsync(item.Record.FilePath, maxDimension: 1600).ConfigureAwait(true);
        if (frame is not null)
        {
            _pinned.Show(frame, item.Record.FilePath, item.Game.Length > 0 ? item.Game : item.FileName);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        var item = SelectedItem!;
        var confirmed = await _dialogs.ConfirmAsync(
            Localization["Library_DeleteTitle"],
            Localization.Format("Library_DeleteMessage", item.FileName),
            Localization["Library_Delete"],
            Localization["Common_Cancel"]).ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        if (await _library.DeleteAsync(item.Record).ConfigureAwait(true))
        {
            _all.Remove(item);
            Items.Remove(item);
            UpdateSummary();
            _toasts.ShowInfo(Localization["Library_Deleted"], item.FileName);
        }
        else
        {
            _toasts.ShowError(Localization["Toast_Error"], Localization["Library_DeleteFailed"]);
        }
    }

    private bool HasSelection() => SelectedItem is not null;

    private bool HasHdrSelection() => SelectedItem?.HasHdr == true;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedGameChanged(string? value) => ApplyFilter();

    partial void OnHdrOnlyChanged(bool value) => ApplyFilter();

    private void RebuildGames()
    {
        var selected = SelectedGame;
        Games.Clear();
        Games.Add(Localization["Library_AllGames"]);
        foreach (var game in _all.Select(i => i.Game).Where(g => g.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase))
        {
            Games.Add(game);
        }
        SelectedGame = selected is not null && Games.Contains(selected) ? selected : Games[0];
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var game = SelectedGame is { } g && Games.Count > 0 && g != Games[0] ? g : null;
        Items.Clear();
        foreach (var item in _all)
        {
            if ((game is null || string.Equals(item.Game, game, StringComparison.OrdinalIgnoreCase))
                && (!HdrOnly || item.HasHdr)
                && (query.Length == 0
                    || item.FileName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || item.Game.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            {
                Items.Add(item);
            }
        }
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        SummaryText = Localization.Format("Library_Summary", Items.Count, _all.Count);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        _stale = true;
        if (!_disposed && _isPageActive)
        {
            _ = RefreshAsync();
        }
    });

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed && e.PropertyName == "Item[]")
        {
            RebuildGames();
            UpdateSummary();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _library.Changed -= OnLibraryChanged;
        Localization.PropertyChanged -= OnLocalizationChanged;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the capture library.")]
    private partial void LogRefreshFailed(Exception exception);
}

/// <summary>Shared OCR result handling (Library and Preview): copy text and tell the user.</summary>
public static class OcrFeedback
{
    public static async Task ReportAsync(OcrResult result, IClipboardService clipboard, IToastService toasts, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(localization);

        switch (result.Status)
        {
            case OcrStatus.Success when await clipboard.CopyTextAsync(result.Text).ConfigureAwait(true):
                var words = result.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                toasts.ShowSuccess(localization.Format("Toast_OcrSuccess", words), result.Text.Length > 80 ? result.Text[..80] + "…" : result.Text);
                break;
            case OcrStatus.NoText:
                toasts.ShowInfo(localization["Toast_OcrEmpty"], string.Empty);
                break;
            case OcrStatus.EngineUnavailable:
                toasts.ShowWarning(localization["Toast_OcrUnavailable"], string.Empty);
                break;
            default:
                toasts.ShowError(localization["Toast_Error"], localization["Toast_OcrFailed"]);
                break;
        }
    }
}
