// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Serilog.Core;
using Serilog.Events;

namespace CaptureImage.Infrastructure.Logging;

/// <summary>Changes the live Serilog minimum level (no restart needed).</summary>
public sealed class SerilogLogLevelSwitcher : ILogLevelSwitcher
{
    private readonly LoggingLevelSwitch _levelSwitch;

    public SerilogLogLevelSwitcher(LoggingLevelSwitch levelSwitch)
    {
        _levelSwitch = levelSwitch;
    }

    public string CurrentLevel => _levelSwitch.MinimumLevel switch
    {
        LogEventLevel.Verbose or LogEventLevel.Debug => "Debug",
        LogEventLevel.Warning => "Warning",
        LogEventLevel.Error or LogEventLevel.Fatal => "Error",
        _ => "Information",
    };

    public void SetLevel(string level) => _levelSwitch.MinimumLevel = Parse(level);

    /// <summary>Settings value ("Debug", "Information", …; case-insensitive) to a Serilog level.</summary>
    public static LogEventLevel Parse(string? level) => SettingValues.NormalizeLogLevel(level) switch
    {
        "Debug" => LogEventLevel.Debug,
        "Warning" => LogEventLevel.Warning,
        "Error" => LogEventLevel.Error,
        _ => LogEventLevel.Information,
    };
}
