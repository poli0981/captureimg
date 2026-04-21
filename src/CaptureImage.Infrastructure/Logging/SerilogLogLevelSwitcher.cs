using CaptureImage.Core.Abstractions;
using Serilog.Core;
using Serilog.Events;

namespace CaptureImage.Infrastructure.Logging;

/// <summary>
/// <see cref="ILogLevelSwitcher"/> backed by Serilog's <see cref="LoggingLevelSwitch"/>.
/// </summary>
public sealed class SerilogLogLevelSwitcher : ILogLevelSwitcher
{
    private readonly LoggingLevelSwitch _levelSwitch;

    public SerilogLogLevelSwitcher(LoggingLevelSwitch levelSwitch)
    {
        _levelSwitch = levelSwitch;
    }

    public string CurrentLevel => _levelSwitch.MinimumLevel switch
    {
        LogEventLevel.Verbose     => "Debug",
        LogEventLevel.Debug       => "Debug",
        LogEventLevel.Information => "Information",
        LogEventLevel.Warning     => "Warning",
        LogEventLevel.Error       => "Error",
        LogEventLevel.Fatal       => "Error",
        _                         => "Information",
    };

    public void SetLevel(string level)
    {
        _levelSwitch.MinimumLevel = level?.Trim().ToLowerInvariant() switch
        {
            "debug"       => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "info"        => LogEventLevel.Information,
            "warning"     => LogEventLevel.Warning,
            "warn"        => LogEventLevel.Warning,
            "error"       => LogEventLevel.Error,
            _             => LogEventLevel.Information,
        };
    }
}
