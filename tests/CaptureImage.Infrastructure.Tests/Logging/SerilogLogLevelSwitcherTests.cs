using CaptureImage.Infrastructure.Logging;
using FluentAssertions;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Logging;

/// <summary>
/// Verifies the canonical mapping between <see cref="AppSettings.LogLevel"/> strings and
/// Serilog's <see cref="LogEventLevel"/>. Covers the alias forms (info/warn) and the
/// unknown-value fallback.
/// </summary>
public class SerilogLogLevelSwitcherTests
{
    [Theory]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("debug", LogEventLevel.Debug)]
    [InlineData("Information", LogEventLevel.Information)]
    [InlineData("info", LogEventLevel.Information)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("warn", LogEventLevel.Warning)]
    [InlineData("Error", LogEventLevel.Error)]
    [InlineData("ERROR", LogEventLevel.Error)]
    public void SetLevel_CanonicalAndAliasNames_MapToSerilogLevel(string input, LogEventLevel expected)
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);
        var switcher = new SerilogLogLevelSwitcher(levelSwitch);

        switcher.SetLevel(input);

        levelSwitch.MinimumLevel.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("TRACE")]
    [InlineData("Verbose")]
    [InlineData(null)]
    [InlineData("nonsense")]
    public void SetLevel_UnknownOrNull_FallsBackToInformation(string? input)
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);
        var switcher = new SerilogLogLevelSwitcher(levelSwitch);

        switcher.SetLevel(input!);

        levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Information);
    }

    [Fact]
    public void CurrentLevel_ReportsCanonicalNameAfterSetLevel()
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var switcher = new SerilogLogLevelSwitcher(levelSwitch);

        switcher.SetLevel("debug");

        switcher.CurrentLevel.Should().Be("Debug");
    }
}
