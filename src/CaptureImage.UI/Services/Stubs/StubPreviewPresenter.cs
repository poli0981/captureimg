using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;

namespace CaptureImage.UI.Services.Stubs;

/// <summary>
/// Placeholder <see cref="IPreviewPresenter"/> for v1.3-M1. Auto-rejects every preview so
/// captures never persist while the real WinUI 3 modal lands in v1.3-M6.
/// </summary>
public sealed class StubPreviewPresenter : IPreviewPresenter
{
    private readonly ILogger<StubPreviewPresenter> _logger;

    public StubPreviewPresenter(ILogger<StubPreviewPresenter> logger)
    {
        _logger = logger;
    }

    public Task<bool> ShowAsync(
        CapturedFrame frame,
        GameTarget target,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Preview presenter stub: auto-rejecting capture (no-op until v1.3-M6).");
        return Task.FromResult(false);
    }
}
