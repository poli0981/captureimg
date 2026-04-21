using Serilog.Core;
using Serilog.Events;

namespace CaptureImage.Infrastructure.Logging;

/// <summary>
/// Serilog <see cref="ILogEventEnricher"/> that fills empty defaults for the caller-info
/// properties (<c>File</c>, <c>Line</c>, <c>Member</c>) on every log event where the call
/// site didn't populate them via <c>CallerAwareLoggerExtensions</c>.
/// </summary>
/// <remarks>
/// Without this, the rolling-file output template <c>({File}:{Line})</c> would render as
/// the literal <c>({File}:{Line})</c> tokens on legacy call sites. With it, those call
/// sites produce <c>(:)</c> — ugly but honest ("no caller context here"), and visually
/// easy to filter out when grepping a log.
/// </remarks>
public sealed class CallerPropertyDefaultsEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.ContainsKey("File"))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("File", string.Empty));
        }
        if (!logEvent.Properties.ContainsKey("Line"))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Line", 0));
        }
        if (!logEvent.Properties.ContainsKey("Member"))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Member", string.Empty));
        }
    }
}
