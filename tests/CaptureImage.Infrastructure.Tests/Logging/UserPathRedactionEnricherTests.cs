// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text.Json;
using AwesomeAssertions;
using CaptureImage.Infrastructure.Logging;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Logging;

/// <summary>
/// <see cref="UserPathRedactionEnricher"/>: the profile folder (which contains the account
/// name) becomes <c>%USERPROFILE%</c> in log properties and in exported diagnostics text,
/// whatever its casing, while everything else is left alone.
/// </summary>
public class UserPathRedactionEnricherTests
{
    private const string Profile = @"C:\Users\Alice";

    private readonly UserPathRedactionEnricher _enricher = new(Profile);

    [Theory]
    [InlineData(@"C:\Users\Alice\Pictures\shot.png", @"%USERPROFILE%\Pictures\shot.png")]
    [InlineData(@"c:\users\alice\Pictures\shot.png", @"%USERPROFILE%\Pictures\shot.png")]
    [InlineData(@"C:\USERS\ALICE", "%USERPROFILE%")]
    [InlineData(@"copy C:\Users\Alice\a.png to C:\Users\Alice\b.png", @"copy %USERPROFILE%\a.png to %USERPROFILE%\b.png")]
    public void Redact_ReplacesTheProfileFolder_IgnoringCase(string text, string expected)
    {
        _enricher.Redact(text).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"D:\Games\Elden Ring\eldenring.exe")]
    [InlineData(@"C:\Users\Public\Pictures\shot.png")]
    [InlineData(@"C:\Users")]
    [InlineData("Captured 1920x1080 in 12 ms")]
    [InlineData("")]
    public void Redact_LeavesOtherTextUntouched(string text)
    {
        _enricher.Redact(text).Should().Be(text);
    }

    [Fact]
    public void Redact_TrailingSeparatorOnTheProfilePath_IsIgnored()
    {
        new UserPathRedactionEnricher(Profile + @"\").Redact(@"C:\Users\Alice\x.png").Should().Be(@"%USERPROFILE%\x.png");
    }

    [Fact]
    public void Redact_ProfileInsideJsonText_IsReplacedToo()
    {
        // settings.json escapes backslashes, so the profile appears as C:\\Users\\Alice there.
        var json = JsonSerializer.Serialize(new { outputDirectory = @"C:\Users\Alice\Pictures\CaptureImage" });

        var redacted = _enricher.Redact(json);

        redacted.Should().NotContainEquivalentOf("Alice");
        using var document = JsonDocument.Parse(redacted);
        document.RootElement.GetProperty("outputDirectory").GetString().Should().Be(@"%USERPROFILE%\Pictures\CaptureImage");
    }

    [Fact]
    public void Redact_NonAsciiProfileInsideJsonText_IsReplacedToo()
    {
        var enricher = new UserPathRedactionEnricher(@"C:\Users\Nguyễn");
        var json = JsonSerializer.Serialize(new { outputDirectory = @"C:\Users\Nguyễn\Pictures" });

        enricher.Redact(json).Should().NotContain("Nguy").And.Contain("%USERPROFILE%");
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"C:\")]
    public void Redact_UnusableProfilePath_ChangesNothing(string profile)
    {
        var enricher = new UserPathRedactionEnricher(profile);

        enricher.Redact(@"C:\Users\Alice\x.png").Should().Be(@"C:\Users\Alice\x.png");
    }

    [Fact]
    public void DefaultConstructor_UsesTheCurrentUsersProfile()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        new UserPathRedactionEnricher().Redact(Path.Combine(profile, "Pictures", "x.png")).Should().Be(@"%USERPROFILE%\Pictures\x.png");
    }

    [Fact]
    public void Enrich_RedactsStringProperties_AndLeavesTheRestAlone()
    {
        var evt = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("Saved {Path} ({Width}px) for {Game}"),
            [
                new LogEventProperty("Path", new ScalarValue(@"C:\Users\ALICE\Pictures\a.png")),
                new LogEventProperty("Width", new ScalarValue(1920)),
                new LogEventProperty("Game", new ScalarValue("Elden Ring")),
            ]);
        var untouched = evt.Properties["Game"];

        _enricher.Enrich(evt, Substitute.For<ILogEventPropertyFactory>());

        evt.Properties["Path"].Should().BeEquivalentTo(new ScalarValue(@"%USERPROFILE%\Pictures\a.png"));
        evt.Properties["Width"].Should().BeEquivalentTo(new ScalarValue(1920));
        evt.Properties["Game"].Should().BeSameAs(untouched);
        evt.MessageTemplate.Text.Should().Be("Saved {Path} ({Width}px) for {Game}");
    }

    [Fact]
    public void Enrich_InALoggerPipeline_RedactsWhatTheSinksSee()
    {
        var sink = new InMemorySink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With(new UserPathRedactionEnricher(Profile))
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Saved {Path}", @"C:\Users\Alice\Pictures\CaptureImage\shot.png");

        sink.Snapshot()[0].Message.Should().Be(@"Saved %USERPROFILE%\Pictures\CaptureImage\shot.png");
    }

    [Fact]
    public void Enrich_UnusableProfilePath_ChangesNothing()
    {
        var evt = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("{Path}"),
            [new LogEventProperty("Path", new ScalarValue(@"C:\Users\Alice\a.png"))]);
        var original = evt.Properties["Path"];

        new UserPathRedactionEnricher(string.Empty).Enrich(evt, Substitute.For<ILogEventPropertyFactory>());

        evt.Properties["Path"].Should().BeSameAs(original);
    }
}
