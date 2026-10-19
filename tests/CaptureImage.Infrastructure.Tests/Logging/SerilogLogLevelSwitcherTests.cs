// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Infrastructure.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Logging;

/// <summary>
/// <see cref="SerilogLogLevelSwitcher"/>: settings strings ("Debug", "Information", "Warning",
/// "Error", any case) to Serilog levels, the fallback for anything else, and the live effect
/// on a logger controlled by the switch.
/// </summary>
public class SerilogLogLevelSwitcherTests
{
    [Theory]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("debug", LogEventLevel.Debug)]
    [InlineData("Information", LogEventLevel.Information)]
    [InlineData("INFORMATION", LogEventLevel.Information)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("warning", LogEventLevel.Warning)]
    [InlineData("Error", LogEventLevel.Error)]
    [InlineData("ERROR", LogEventLevel.Error)]
    public void Parse_SettingsValues_MapToSerilogLevels(string value, LogEventLevel expected)
    {
        SerilogLogLevelSwitcher.Parse(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Verbose")]
    [InlineData("Fatal")]
    [InlineData("TRACE")]
    [InlineData("nonsense")]
    public void Parse_AnythingElse_FallsBackToInformation(string? value)
    {
        SerilogLogLevelSwitcher.Parse(value).Should().Be(LogEventLevel.Information);
    }

    [Theory]
    [InlineData("debug", LogEventLevel.Debug)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("ERROR", LogEventLevel.Error)]
    [InlineData("bogus", LogEventLevel.Information)]
    public void SetLevel_MovesTheSwitch(string value, LogEventLevel expected)
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);

        new SerilogLogLevelSwitcher(levelSwitch).SetLevel(value);

        levelSwitch.MinimumLevel.Should().Be(expected);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, "Debug")]
    [InlineData(LogEventLevel.Debug, "Debug")]
    [InlineData(LogEventLevel.Information, "Information")]
    [InlineData(LogEventLevel.Warning, "Warning")]
    [InlineData(LogEventLevel.Error, "Error")]
    [InlineData(LogEventLevel.Fatal, "Error")]
    public void CurrentLevel_ReportsTheSettingsName(LogEventLevel level, string expected)
    {
        new SerilogLogLevelSwitcher(new LoggingLevelSwitch(level)).CurrentLevel.Should().Be(expected);
    }

    [Fact]
    public void SetLevel_FiltersALiveLoggerImmediately()
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var switcher = new SerilogLogLevelSwitcher(levelSwitch);
        var sink = new InMemorySink();
        using var logger = new LoggerConfiguration().MinimumLevel.ControlledBy(levelSwitch).WriteTo.Sink(sink).CreateLogger();

        logger.Debug("hidden at Information");
        switcher.SetLevel("Debug");
        logger.Debug("shown at Debug");
        switcher.SetLevel("Error");
        logger.Warning("hidden at Error");
        logger.Error("shown at Error");

        sink.Snapshot().Select(e => e.Message).Should().Equal("shown at Debug", "shown at Error");
        switcher.CurrentLevel.Should().Be("Error");
    }
}
