using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace CaptureImage.UI.Services.Stubs;

/// <summary>
/// Placeholder <see cref="ITrayIconHost"/> for v1.3-M1. The real
/// <c>H.NotifyIcon.WindowsAppSDK</c>-backed implementation lands in v1.3-M6.
/// </summary>
public sealed class StubTrayIconHost : ITrayIconHost
{
    private readonly ILogger<StubTrayIconHost> _logger;

    public StubTrayIconHost(ILogger<StubTrayIconHost> logger)
    {
        _logger = logger;
    }

    public void Initialize(object mainWindow)
    {
        _logger.LogInformation(
            "Tray icon stub initialized (no-op). Real impl deferred to v1.3-M6.");
    }

    public void Dispose()
    {
    }
}
