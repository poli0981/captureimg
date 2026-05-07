using CaptureImage.Core.Abstractions;
using CaptureImage.UI.Views;
using Microsoft.Extensions.Logging;

namespace CaptureImage.UI.Services;

/// <summary>
/// Spawns <see cref="PinnedThumbnailWindow"/> instances on demand. Hops to the UI
/// thread because <see cref="DashboardViewModel"/> can fire Show from a continuation
/// thread after the encoder finishes.
/// </summary>
public sealed class PinnedThumbnailHost : IPinnedThumbnailHost
{
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settings;
    private readonly ILogger<PinnedThumbnailHost> _logger;

    public PinnedThumbnailHost(
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        ISettingsStore settings,
        ILogger<PinnedThumbnailHost> logger)
    {
        _dispatcher = dispatcher;
        _localization = localization;
        _settings = settings;
        _logger = logger;
    }

    public void Show(byte[] pngBytes, string? filePath, string targetName)
    {
        if (pngBytes is null || pngBytes.Length == 0) return;

        _dispatcher.Post(() =>
        {
            try
            {
                var window = new PinnedThumbnailWindow(_localization);
                window.SetThumbnail(pngBytes, filePath, targetName);
                window.AttachThemeStore(_settings);
                window.Activate();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to spawn pinned thumbnail window.");
            }
        });
    }
}
