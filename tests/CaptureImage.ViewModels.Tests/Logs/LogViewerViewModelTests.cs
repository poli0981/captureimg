using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Logs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Logs;

/// <summary>
/// Covers the view-side logic of <see cref="LogViewerViewModel"/> — hydration from the
/// buffer source, the live-tail append path, and the min-level filter that feeds the
/// viewer while leaving the underlying sink snapshot intact (so Export stays complete).
/// </summary>
public class LogViewerViewModelTests
{
    [Fact]
    public void EnsureHydrated_CopiesSnapshotIntoEntries()
    {
        var source = new FakeBuffer();
        source.Feed("one", LogLevel.Information);
        source.Feed("two", LogLevel.Warning);
        var vm = Build(source);

        vm.EnsureHydrated();

        vm.Entries.Should().HaveCount(2);
        vm.Entries[0].Message.Should().Be("one");
        vm.Entries[1].Message.Should().Be("two");
    }

    [Fact]
    public void EnsureHydrated_OnlySecondCallDoesNothing()
    {
        var source = new FakeBuffer();
        source.Feed("a", LogLevel.Information);
        var vm = Build(source);

        vm.EnsureHydrated();
        vm.EnsureHydrated(); // idempotent

        vm.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void LiveTail_AppendedEntryAppearsInEntries()
    {
        var source = new FakeBuffer();
        var vm = Build(source);

        source.Emit(new LogEntry(
            DateTimeOffset.UtcNow,
            LogLevel.Information,
            "TestCtx",
            "live",
            Exception: null));

        vm.Entries.Should().ContainSingle()
            .Which.Message.Should().Be("live");
    }

    [Fact]
    public void FilterChange_DropsLowerLevelEntries_KeepsHigherOnes()
    {
        var source = new FakeBuffer();
        source.Feed("dbg", LogLevel.Debug);
        source.Feed("inf", LogLevel.Information);
        source.Feed("wrn", LogLevel.Warning);
        source.Feed("err", LogLevel.Error);
        var vm = Build(source);
        vm.EnsureHydrated();

        vm.SelectedFilterLevel = LogLevel.Warning;

        vm.Entries.Should().HaveCount(2);
        vm.Entries.Should().OnlyContain(e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public void FilterChange_DoesNotTouchUnderlyingBuffer()
    {
        var source = new FakeBuffer();
        source.Feed("a", LogLevel.Debug);
        source.Feed("b", LogLevel.Information);
        var vm = Build(source);
        vm.EnsureHydrated();

        vm.SelectedFilterLevel = LogLevel.Error;

        vm.Entries.Should().BeEmpty();
        // Export path calls Snapshot directly — full set must still be there.
        source.Snapshot().Should().HaveCount(2);
    }

    [Fact]
    public void LiveTail_WhenEntryBelowFilter_Suppressed()
    {
        var source = new FakeBuffer();
        var vm = Build(source);
        vm.SelectedFilterLevel = LogLevel.Warning;

        source.Emit(new LogEntry(
            DateTimeOffset.UtcNow, LogLevel.Debug, "Ctx", "quiet", null));
        source.Emit(new LogEntry(
            DateTimeOffset.UtcNow, LogLevel.Error, "Ctx", "loud", null));

        vm.Entries.Should().ContainSingle()
            .Which.Message.Should().Be("loud");
    }

    // -- helpers --------------------------------------------------------------

    private static LogViewerViewModel Build(ILogBufferSource source)
    {
        var dispatcher = Substitute.For<IUIThreadDispatcher>();
        dispatcher.When(d => d.Post(Arg.Any<Action>()))
            .Do(call => call.Arg<Action>().Invoke());

        var localization = Substitute.For<ILocalizationService>();
        localization["Log_Pause"].Returns("Pause");
        localization["Log_Resume"].Returns("Resume");
        localization["Log_EventsCount"].Returns("{0} events");
        localization["Log_EmptyState"].Returns("no entries");
        localization["Log_Filter"].Returns("min:");
        localization["Log_RevealFolder"].Returns("open");

        var toasts = Substitute.For<IToastService>();

        return new LogViewerViewModel(
            source,
            dispatcher,
            localization,
            NullLogger<LogViewerViewModel>.Instance,
            toasts);
    }

    private sealed class FakeBuffer : ILogBufferSource
    {
        private readonly List<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Snapshot() => new List<LogEntry>(_entries);
        public event EventHandler<LogEntry>? Emitted;
        public bool Paused { get; set; }
        public void Clear() => _entries.Clear();

        public void Feed(string message, LogLevel level)
        {
            _entries.Add(new LogEntry(
                DateTimeOffset.UtcNow, level, "TestCtx", message, Exception: null));
        }

        /// <summary>Test-only: push an entry as if the live tail fired.</summary>
        public void Emit(LogEntry entry)
        {
            _entries.Add(entry);
            Emitted?.Invoke(this, entry);
        }
    }
}
