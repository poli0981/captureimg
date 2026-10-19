// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Services;
using CaptureImage.ViewModels.Update;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Update;

public sealed class UpdateViewModelTests : IDisposable
{
    private readonly IUpdateService _updates = Substitute.For<IUpdateService>();
    private readonly IAppLifetime _lifetime = Substitute.For<IAppLifetime>();
    private UpdateViewModel? _vm;

    public void Dispose() => _vm?.Dispose();

    [Fact]
    public void NotInstalled_ReportsUnavailable()
    {
        _updates.IsAvailable.Returns(false);

        var vm = Create();

        vm.Status.Should().Be(UpdateStatus.Unavailable);
        vm.StatusText.Should().Be("Update_Status_Unavailable");
        vm.CheckCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task CheckDownloadInstall_FullFlow()
    {
        _updates.IsAvailable.Returns(true);
        _updates.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(new UpdateCheckResult(UpdateStatus.UpdateAvailable, "1.5.1", "2.0.0", "## Notes"));
        _updates.DownloadAsync(Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<IProgress<int>?>()?.Report(50);
                return Task.CompletedTask;
            });
        var vm = Create();

        await vm.CheckCommand.ExecuteAsync(null);
        vm.Status.Should().Be(UpdateStatus.UpdateAvailable);
        vm.AvailableVersion.Should().Be("2.0.0");
        vm.HasReleaseNotes.Should().BeTrue();
        vm.DownloadCommand.CanExecute(null).Should().BeTrue();

        await vm.DownloadCommand.ExecuteAsync(null);
        vm.Status.Should().Be(UpdateStatus.Ready);
        vm.Progress.Should().Be(50);

        vm.InstallAndRestartCommand.Execute(null);
        _updates.Received(1).ApplyOnExit();
        _lifetime.Received(1).Shutdown();
    }

    [Fact]
    public async Task DownloadFailure_IsReported()
    {
        _updates.IsAvailable.Returns(true);
        _updates.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(new UpdateCheckResult(UpdateStatus.UpdateAvailable, "1.5.1", "2.0.0", null));
        _updates.DownloadAsync(Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        var vm = Create();

        await vm.CheckCommand.ExecuteAsync(null);
        await vm.DownloadCommand.ExecuteAsync(null);

        vm.Status.Should().Be(UpdateStatus.Failed);
        vm.LogLines.Should().Contain("offline");
    }

    [Fact]
    public void FirstVisit_ChecksOnce()
    {
        _updates.IsAvailable.Returns(true);
        _updates.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(new UpdateCheckResult(UpdateStatus.UpToDate, "2.0.0", null, null));
        var vm = Create();

        vm.IsPageActive = true;
        vm.IsPageActive = false;
        vm.IsPageActive = true;

        _updates.Received(1).CheckAsync(Arg.Any<CancellationToken>());
        vm.StatusText.Should().Be("Update_Status_UpToDate");
    }

    [Fact]
    public void UpdaterLog_IsShown()
    {
        _updates.IsAvailable.Returns(true);
        var vm = Create();

        _updates.LogEmitted += Raise.Event<EventHandler<string>>(_updates, "Checking GitHub Releases…");

        vm.LogLines.Should().Equal("Checking GitHub Releases…");
    }

    private UpdateViewModel Create()
    {
        _vm = new UpdateViewModel(_updates, _lifetime, Substitute.For<IShellService>(), new InlineDispatcher(), new KeyLocalization(),
            new AppInfo("2.0.0", "abc1234"), NullLogger<UpdateViewModel>.Instance);
        return _vm;
    }
}
