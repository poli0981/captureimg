// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Paths;
using CaptureImage.ViewModels.Services;
using CaptureImage.ViewModels.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Settings;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly FakeSettingsStore _store = new(new AppSettings
    {
        Theme = "dark",
        LogLevel = "warning",
        Capture = new CaptureSettings { JpegQuality = 250, CountdownSeconds = 7, FileNameTemplate = "{Game}_{yyyy}" },
    });

    private readonly CoordinatorFixture _coordinator;
    private readonly IAutostartService _autostart = Substitute.For<IAutostartService>();
    private readonly ILogLevelSwitcher _logLevel = Substitute.For<ILogLevelSwitcher>();
    private readonly IThemeService _theme = Substitute.For<IThemeService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IToastService _toasts = Substitute.For<IToastService>();
    private readonly KeyLocalization _localization = new();
    private readonly SettingsViewModel _vm;

    public SettingsViewModelTests()
    {
        _coordinator = new CoordinatorFixture(_store);
        _vm = new SettingsViewModel(
            _store,
            _coordinator.Coordinator,
            _coordinator.Hotkeys,
            _autostart,
            _logLevel,
            _theme,
            _dialogs,
            Substitute.For<IShellService>(),
            Substitute.For<IDiagnosticsExporter>(),
            _toasts,
            new InlineDispatcher(),
            _localization,
            new AppPaths(Path.GetTempPath()),
            TimeProvider.System,
            NullLogger<SettingsViewModel>.Instance);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _coordinator.Dispose();
    }

    [Fact]
    public void Load_NormalizesStoredValues()
    {
        _vm.Theme.Should().Be("Dark");
        _vm.LogLevel.Should().Be("Warning");
        _vm.JpegQuality.Should().Be(100);
        _vm.CountdownSeconds.Should().Be(0);
        _vm.Hotkeys.Select(h => h.Mode).Should().Equal(Enum.GetValues<CaptureMode>());
    }

    [Fact]
    public void Loading_DoesNotWriteBack()
    {
        _store.UpdateCount.Should().Be(0);
    }

    [Fact]
    public void ChangingAValue_WritesThrough()
    {
        _vm.WebpQuality = 70;
        _vm.PreviewBeforeSave = true;
        _vm.ClipboardMode = "CopyAndSave";

        _store.Current.Capture.WebpQuality.Should().Be(70);
        _store.Current.Capture.PreviewBeforeSave.Should().BeTrue();
        _store.Current.Capture.ClipboardMode.Should().Be("CopyAndSave");
    }

    [Fact]
    public void Theme_IsAppliedAndSaved()
    {
        _vm.Theme = "Light";

        _theme.Received(1).Apply("Light");
        _store.Current.Theme.Should().Be("Light");
    }

    [Fact]
    public void Language_SwitchesCultureAndIsSaved()
    {
        _vm.Language = "vi-VN";

        _localization.CurrentCulture.Name.Should().Be("vi-VN");
        _store.Current.Culture.Should().Be("vi-VN");
    }

    [Fact]
    public void LogLevel_IsAppliedToTheLogger()
    {
        _vm.LogLevel = "Debug";

        _logLevel.Received(1).SetLevel("Debug");
        _store.Current.LogLevel.Should().Be("Debug");
    }

    [Fact]
    public void StartWithWindows_WhenRegistryFails_RevertsAndWarns()
    {
        _autostart.SetEnabled(true).Returns(false);
        _autostart.IsEnabled.Returns(false);

        _vm.StartWithWindows = true;

        _vm.StartWithWindows.Should().BeFalse();
        _store.Current.UI.StartWithWindows.Should().BeFalse();
        _toasts.Received(1).Show(ToastKind.Error, "Toast_Error", "Settings_StartWithWindowsFailed", null);
    }

    [Fact]
    public void ExternalChange_ReloadsThePage()
    {
        _store.ReplaceExternally(_store.Current with { Capture = _store.Current.Capture with { WebpQuality = 42 } });

        _vm.WebpQuality.Should().Be(42);
    }

    [Fact]
    public void FileNamePreview_FollowsTheTemplateAndFormat()
    {
        _vm.FileNameTemplate = "shot_{Mode}";
        _vm.DefaultFormat = ImageFormat.Jpeg;

        _vm.FileNamePreview.Should().Be("shot_Window.jpg");
    }

    [Fact]
    public void ResetFolderAndTemplate_RestoreDefaults()
    {
        _vm.OutputDirectory = @"D:\Shots";
        _vm.ResetOutputFolderCommand.Execute(null);
        _vm.ResetFileNameTemplateCommand.Execute(null);

        _store.Current.Capture.OutputDirectory.Should().BeEmpty();
        _vm.HasCustomOutputDirectory.Should().BeFalse();
        _vm.FileNameTemplate.Should().Be(Core.Pipeline.FileNameStrategy.DefaultTemplate);
    }

    [Fact]
    public async Task ImportSettings_InvalidFile_ShowsAnErrorAndKeepsSettings()
    {
        var store = Substitute.For<ISettingsStore>();
        store.Current.Returns(new AppSettings());
        store.ImportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidDataException("bad"));
        _dialogs.PickFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns("settings.json");
        using var coordinator = new CoordinatorFixture(store);
        using var vm = new SettingsViewModel(
            store, coordinator.Coordinator, coordinator.Hotkeys, _autostart, _logLevel, _theme, _dialogs,
            Substitute.For<IShellService>(), Substitute.For<IDiagnosticsExporter>(), _toasts, new InlineDispatcher(),
            _localization, new AppPaths(Path.GetTempPath()), TimeProvider.System, NullLogger<SettingsViewModel>.Instance);

        await vm.ImportSettingsCommand.ExecuteAsync(null);

        _toasts.Received(1).Show(ToastKind.Error, "Toast_Error", "Settings_ImportFailed", null);
        _theme.DidNotReceive().Apply(Arg.Any<string>());
    }

    [Fact]
    public async Task ResetSettings_RequiresConfirmation()
    {
        _store.Update(s => s with { Theme = "Light" });
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        await _vm.ResetSettingsCommand.ExecuteAsync(null);
        _store.Current.Theme.Should().Be("Light");

        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        await _vm.ResetSettingsCommand.ExecuteAsync(null);
        _store.Current.Theme.Should().Be("System");
        _vm.Theme.Should().Be("System");
    }

    [Fact]
    public void LanguageSwitch_RefreshesOptionLabels()
    {
        var raised = new List<string?>();
        _vm.ThemeOptions[0].PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _localization.SetCulture(System.Globalization.CultureInfo.GetCultureInfo("ar-SA"));

        raised.Should().Contain(nameof(LocalizedOption<string>.Label));
    }
}
