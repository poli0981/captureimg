using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Core.Logging;

/// <summary>
/// Extension methods that inject <c>[CallerMemberName]</c>, <c>[CallerFilePath]</c>, and
/// <c>[CallerLineNumber]</c> as Serilog structured properties (<c>Member</c>, <c>File</c>,
/// <c>Line</c>) so every entry the log viewer shows — and every line the rolling file
/// receives — carries a cheap jump-to-source anchor.
/// </summary>
/// <remarks>
/// <para>
/// The properties are attached via <see cref="ILogger.BeginScope{TState}"/>. The
/// <c>Serilog.Extensions.Logging</c> provider forwards MEL scope state as
/// <see cref="Serilog.Events.LogEvent"/> properties, so the output template
/// <c>{File}:{Line}</c> and the <see cref="Models.LogEntry"/> mapper both see them.
/// </para>
/// <para>
/// <c>Path.GetFileName</c> strips the build-machine absolute path so we don't leak
/// <c>E:\capture\captureimg\…</c> into shipped binaries and user log exports.
/// </para>
/// <para>
/// Overloads are provided for 0–3 template arguments — that covers the vast majority of
/// call sites. For more arguments, fall back to <see cref="ILogger.BeginScope{TState}"/>
/// with a manual scope and regular <c>LogInformation</c>/<c>LogError</c>/etc.
/// </para>
/// </remarks>
public static class CallerAwareLoggerExtensions
{
    // --- Information --------------------------------------------------------

    public static void LogInformationAt(
        this ILogger logger,
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogInformation(message);
        }
    }

    public static void LogInformationAt<T0>(
        this ILogger logger,
        string messageTemplate,
        T0 arg0,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogInformation(messageTemplate, arg0);
        }
    }

    public static void LogInformationAt<T0, T1>(
        this ILogger logger,
        string messageTemplate,
        T0 arg0,
        T1 arg1,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogInformation(messageTemplate, arg0, arg1);
        }
    }

    public static void LogInformationAt<T0, T1, T2>(
        this ILogger logger,
        string messageTemplate,
        T0 arg0,
        T1 arg1,
        T2 arg2,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogInformation(messageTemplate, arg0, arg1, arg2);
        }
    }

    // --- Debug --------------------------------------------------------------

    public static void LogDebugAt(
        this ILogger logger,
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogDebug(message);
        }
    }

    public static void LogDebugAt<T0>(
        this ILogger logger,
        string messageTemplate,
        T0 arg0,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogDebug(messageTemplate, arg0);
        }
    }

    public static void LogDebugAt<T0, T1>(
        this ILogger logger,
        string messageTemplate,
        T0 arg0,
        T1 arg1,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogDebug(messageTemplate, arg0, arg1);
        }
    }

    // --- Warning ------------------------------------------------------------

    public static void LogWarningAt(
        this ILogger logger,
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogWarning(message);
        }
    }

    public static void LogWarningAt<T0>(
        this ILogger logger,
        string messageTemplate,
        T0 arg0,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogWarning(messageTemplate, arg0);
        }
    }

    public static void LogWarningAt<T0, T1>(
        this ILogger logger,
        string messageTemplate,
        T0 arg0,
        T1 arg1,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogWarning(messageTemplate, arg0, arg1);
        }
    }

    public static void LogWarningAt(
        this ILogger logger,
        Exception exception,
        string messageTemplate,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogWarning(exception, messageTemplate);
        }
    }

    public static void LogWarningAt<T0>(
        this ILogger logger,
        Exception exception,
        string messageTemplate,
        T0 arg0,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogWarning(exception, messageTemplate, arg0);
        }
    }

    public static void LogWarningAt<T0, T1>(
        this ILogger logger,
        Exception exception,
        string messageTemplate,
        T0 arg0,
        T1 arg1,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogWarning(exception, messageTemplate, arg0, arg1);
        }
    }

    // --- Error --------------------------------------------------------------

    public static void LogErrorAt(
        this ILogger logger,
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogError(message);
        }
    }

    public static void LogErrorAt(
        this ILogger logger,
        Exception exception,
        string messageTemplate,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogError(exception, messageTemplate);
        }
    }

    public static void LogErrorAt<T0>(
        this ILogger logger,
        Exception exception,
        string messageTemplate,
        T0 arg0,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogError(exception, messageTemplate, arg0);
        }
    }

    public static void LogErrorAt<T0, T1>(
        this ILogger logger,
        Exception exception,
        string messageTemplate,
        T0 arg0,
        T1 arg1,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        using (logger.BeginScope(CallerScope(member, filePath, line)))
        {
            logger.LogError(exception, messageTemplate, arg0, arg1);
        }
    }

    // --- Shared scope builder -----------------------------------------------

    private static Dictionary<string, object> CallerScope(string member, string filePath, int line)
        => new(3)
        {
            ["Member"] = member ?? string.Empty,
            // Path.GetFileName tolerates forward and back slashes, returns the input if the
            // string doesn't look like a path — so empty / odd CallerFilePath values degrade
            // gracefully.
            ["File"]   = string.IsNullOrEmpty(filePath) ? string.Empty : Path.GetFileName(filePath),
            ["Line"]   = line,
        };
}
