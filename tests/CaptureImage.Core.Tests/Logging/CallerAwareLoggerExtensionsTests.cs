using CaptureImage.Core.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CaptureImage.Core.Tests.Logging;

/// <summary>
/// Validates that <c>LogXAt</c> extensions attach <c>File</c> / <c>Line</c> / <c>Member</c>
/// as MEL scope state — which Serilog's MEL bridge then surfaces as structured
/// <see cref="Serilog.Events.LogEvent"/> properties at runtime.
/// </summary>
/// <remarks>
/// Rather than stand up the real Serilog pipeline, we use a tiny spy <see cref="ILogger"/>
/// that captures whatever scope state arrives. That's enough to assert the extensions
/// pass the caller context through correctly; the end-to-end "scope reaches the rolling
/// file" behaviour is covered by manual verification.
/// </remarks>
public class CallerAwareLoggerExtensionsTests
{
    [Fact]
    public void LogInformationAt_InjectsCallerContextIntoScope()
    {
        var spy = new ScopeCapturingLogger();

        spy.LogInformationAt("hello");

        var captured = spy.Scopes.Should().ContainSingle().Subject;
        captured.Should().ContainKey("Member");
        captured.Should().ContainKey("File");
        captured.Should().ContainKey("Line");
        captured["Member"].Should().Be(nameof(LogInformationAt_InjectsCallerContextIntoScope));
        captured["File"].Should().Be("CallerAwareLoggerExtensionsTests.cs");
        ((int)captured["Line"]!).Should().BeGreaterThan(0);
    }

    [Fact]
    public void LogWarningAt_WithException_ForwardsExceptionAndCaller()
    {
        var spy = new ScopeCapturingLogger();
        var ex = new InvalidOperationException("boom");

        spy.LogWarningAt(ex, "with exception");

        spy.Entries.Should().ContainSingle()
            .Which.Exception.Should().Be(ex);
        spy.Scopes.Should().ContainSingle()
            .Which.Should().ContainKey("File").WhoseValue
            .Should().Be("CallerAwareLoggerExtensionsTests.cs");
    }

    [Fact]
    public void LogErrorAt_WithTwoArgs_PassesArgsThroughToLogger()
    {
        var spy = new ScopeCapturingLogger();
        var ex = new IOException("disk full");

        spy.LogErrorAt(ex, "failed to save {Path} size={Size}", "/tmp/x", 1024);

        spy.Entries.Should().ContainSingle()
            .Which.State!.ToString().Should().Contain("/tmp/x").And.Contain("1024");
    }

    [Fact]
    public void CallerScope_StripsBuildMachineAbsolutePath()
    {
        // Simulate [CallerFilePath] having produced an absolute path — the helper must
        // strip the directory portion so we don't leak E:\build-agent\... into the log.
        var spy = new ScopeCapturingLogger();
        spy.LogInformationAt(
            "probe",
            member: "test",
            filePath: @"C:\build\machine\deep\path\Module.cs",
            line: 7);

        var scope = spy.Scopes.Should().ContainSingle().Subject;
        scope["File"].Should().Be("Module.cs");
    }

    // -- helpers --------------------------------------------------------------

    /// <summary>Minimal spy capturing every Log call + its surrounding scope state.</summary>
    private sealed class ScopeCapturingLogger : ILogger
    {
        public List<Dictionary<string, object?>> Scopes { get; } = new();
        public List<(LogLevel Level, Exception? Exception, object? State)> Entries { get; } = new();

        private readonly Stack<Dictionary<string, object?>> _currentScopes = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var dict = new Dictionary<string, object?>();
            if (state is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                foreach (var kvp in pairs)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }
            Scopes.Add(dict);
            _currentScopes.Push(dict);
            return new ScopeToken(this);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception, state));
        }

        private sealed class ScopeToken : IDisposable
        {
            private readonly ScopeCapturingLogger _owner;
            public ScopeToken(ScopeCapturingLogger owner) => _owner = owner;
            public void Dispose()
            {
                if (_owner._currentScopes.Count > 0) _owner._currentScopes.Pop();
            }
        }
    }
}
