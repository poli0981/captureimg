namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Live control over the Serilog minimum-level floor. The Settings view mutates this
/// when the user picks a new log level, and the wrapper — implemented in
/// <c>CaptureImage.Infrastructure.Logging</c> — pushes the value through to Serilog's
/// <c>LoggingLevelSwitch</c> so new events are filtered immediately.
/// </summary>
/// <remarks>
/// Level strings accepted by <see cref="SetLevel"/>: <c>Debug</c>, <c>Information</c>,
/// <c>Warning</c>, <c>Error</c> (case-insensitive, aliases <c>info</c> / <c>warn</c>
/// accepted). Unknown strings fall back to <c>Information</c>.
/// </remarks>
public interface ILogLevelSwitcher
{
    /// <summary>Set the minimum log level from a canonical string.</summary>
    void SetLevel(string level);

    /// <summary>Canonical name of the currently active minimum level.</summary>
    string CurrentLevel { get; }
}
