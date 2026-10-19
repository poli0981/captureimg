// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Paths;
using CaptureImage.ViewModels.Logs;
using CaptureImage.ViewModels.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Logs;

public sealed class LogViewerViewModelTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 19, 8, 30, 0, TimeSpan.Zero);

    private readonly ILogBufferSource _buffer = Substitute.For<ILogBufferSource>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly LogViewerViewModel _vm;

    public LogViewerViewModelTests()
    {
        _buffer.Snapshot().Returns(
        [
            Entry(LogEntryLevel.Debug, "Dashboard shows 5 windows."),
            Entry(LogEntryLevel.Information, "Armed: Window=Ctrl+Shift+F12:Registered."),
            Entry(LogEntryLevel.Warning, "Window hotkey not registered: Conflict."),
            Entry(LogEntryLevel.Error, "Capture failed.", "System.IO.IOException: disk full"),
        ]);
        _vm = new LogViewerViewModel(_buffer, _clipboard, Substitute.For<IShellService>(), Substitute.For<IDialogService>(),
            Substitute.For<IDiagnosticsExporter>(), Substitute.For<IToastService>(), new InlineDispatcher(), new KeyLocalization(),
            new AppPaths(Path.GetTempPath()), NullLogger<LogViewerViewModel>.Instance);
    }

    public void Dispose() => _vm.Dispose();

    [Fact]
    public void BecomingVisible_LoadsTheBuffer()
    {
        _vm.Entries.Should().BeEmpty();

        _vm.IsPageActive = true;

        _vm.Entries.Should().HaveCount(4);
    }

    [Fact]
    public void MinimumLevel_AndSearch_Filter()
    {
        _vm.IsPageActive = true;

        _vm.MinimumLevel = LogEntryLevel.Warning;
        _vm.Entries.Select(e => e.Level).Should().Equal(LogEntryLevel.Warning, LogEntryLevel.Error);

        _vm.SearchText = "disk";
        _vm.Entries.Should().ContainSingle().Which.Level.Should().Be(LogEntryLevel.Error);
    }

    [Fact]
    public void NewEntries_AppearOnlyWhileVisible()
    {
        _buffer.Emitted += Raise.Event<EventHandler<LogEntry>>(_buffer, Entry(LogEntryLevel.Information, "hidden"));
        _vm.Entries.Should().BeEmpty();

        _vm.IsPageActive = true;
        _buffer.Emitted += Raise.Event<EventHandler<LogEntry>>(_buffer, Entry(LogEntryLevel.Information, "live"));

        _vm.Entries.Last().Message.Should().Be("live");
    }

    [Fact]
    public void ListIsCapped()
    {
        _vm.IsPageActive = true;

        for (var i = 0; i < 2100; i++)
        {
            _buffer.Emitted += Raise.Event<EventHandler<LogEntry>>(_buffer, Entry(LogEntryLevel.Information, $"line {i}"));
        }

        _vm.Entries.Should().HaveCount(2000);
        _vm.Entries.Last().Message.Should().Be("line 2099");
    }

    [Fact]
    public void Pause_IsForwardedToTheBuffer()
    {
        _vm.IsPaused = true;
        _buffer.Paused.Should().BeTrue();

        _vm.IsPaused = false;
        _buffer.Paused.Should().BeFalse();
    }

    [Fact]
    public void Clear_EmptiesBufferAndList()
    {
        _vm.IsPageActive = true;

        _vm.ClearCommand.Execute(null);

        _buffer.Received(1).Clear();
        _vm.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task CopySelected_CopiesTheFormattedLine()
    {
        _vm.IsPageActive = true;
        _vm.SelectedEntry = _vm.Entries[3];
        _clipboard.CopyTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await _vm.CopySelectedCommand.ExecuteAsync(null);

        await _clipboard.Received(1).CopyTextAsync(
            Arg.Is<string>(text => text.Contains("[Error] Test.Source: Capture failed.", StringComparison.Ordinal)
                && text.Contains("disk full", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Format_IsCultureInvariant()
    {
        LogViewerViewModel.Format(Entry(LogEntryLevel.Warning, "x")).Should().Be("2026-10-19 08:30:00.000 [Warning] Test.Source: x");
    }

    private static LogEntry Entry(LogEntryLevel level, string message, string? exception = null) =>
        new(T0, level, "Test.Source", message, exception);
}
