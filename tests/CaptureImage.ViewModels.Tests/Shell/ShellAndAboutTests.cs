// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Xml.Linq;
using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.About;
using CaptureImage.ViewModels.Library;
using CaptureImage.ViewModels.Preview;
using CaptureImage.ViewModels.Services;
using CaptureImage.ViewModels.Shell;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Shell;

public sealed class ShellViewModelTests : IDisposable
{
    private readonly FakeSettingsStore _store = new();
    private readonly CoordinatorFixture _fixture;
    private readonly IAppLifetime _lifetime = Substitute.For<IAppLifetime>();
    private readonly IMessenger _messenger = new StrongReferenceMessenger();
    private readonly ShellViewModel _vm;

    public ShellViewModelTests()
    {
        _fixture = new CoordinatorFixture(_store);
        _vm = new ShellViewModel(_fixture.Coordinator, _store, Substitute.For<IShellService>(), _lifetime, new InlineDispatcher(),
            new KeyLocalization(), _messenger, new AppInfo("2.0.0", "abc1234"));
    }

    public void Dispose()
    {
        _vm.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void TrayToolTip_FollowsTheArmedState()
    {
        _vm.TrayToolTip.Should().Be("CaptureImage — Status_Idle");
        _vm.ArmMenuText.Should().Be("Tray_Arm");

        _vm.ToggleArmCommand.Execute(null);

        _vm.IsArmed.Should().BeTrue();
        _vm.TrayToolTip.Should().Be("CaptureImage — Status_Armed");
        _vm.ArmMenuText.Should().Be("Tray_Disarm");
        _vm.TrayToolTip.Length.Should().BeLessThanOrEqualTo(127);
    }

    [Fact]
    public void Navigate_SendsTheRequestedPage()
    {
        var pages = new List<string?>();
        _messenger.Register<NavigateMessage>(this, (_, m) => pages.Add(m.PageKey));

        _vm.NavigateCommand.Execute(PageKeys.Library);
        _vm.ShowWindowCommand.Execute(null);

        pages.Should().Equal(PageKeys.Library, null);
    }

    [Fact]
    public void Exit_ShutsDown()
    {
        _vm.ExitCommand.Execute(null);

        _lifetime.Received(1).Shutdown();
    }

    [Fact]
    public void Subtitle_ShowsVersionAndCommit()
    {
        _vm.Subtitle.Should().Be("2.0.0 (abc1234)");
    }
}

public sealed class AboutViewModelTests
{
    private readonly IShellService _shell = Substitute.For<IShellService>();
    private readonly AboutViewModel _vm;

    public AboutViewModelTests()
    {
        _vm = new AboutViewModel(_shell, new KeyLocalization(), new AppInfo("2.0.0", null));
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData(@"C:\Windows\win.ini")]
    [InlineData("")]
    public void OpenDocument_RejectsPathsOutsideTheApp(string path)
    {
        _vm.OpenDocumentCommand.Execute(path);

        _shell.DidNotReceiveWithAnyArgs().Open(default!);
        _shell.DidNotReceiveWithAnyArgs().OpenUrl(default!);
    }

    [Fact]
    public void OpenDocument_MissingLocally_OpensItOnGitHub()
    {
        _vm.OpenDocumentCommand.Execute("docs/legal/NOT_SHIPPED.md");

        _shell.Received(1).OpenUrl(new Uri("https://github.com/poli0981/captureimg/blob/main/docs/legal/NOT_SHIPPED.md"));
    }

    [Fact]
    public void OpenLink_OnlyAllowsHttpsAndMail()
    {
        _vm.OpenLinkCommand.Execute(new Uri("file:///C:/Windows/notepad.exe"));
        _shell.DidNotReceiveWithAnyArgs().OpenUrl(default!);

        _vm.OpenLinkCommand.Execute(new Uri("https://ko-fi.com/skullmute"));
        _shell.Received(1).OpenUrl(new Uri("https://ko-fi.com/skullmute"));
    }

    [Fact]
    public void Contact_UsesTheProjectAddresses()
    {
        _vm.EmailContactCommand.Execute(null);
        _vm.EmailSponsorCommand.Execute(null);

        _shell.Received(1).OpenUrl(new Uri("mailto:contact@poli0981.dev"));
        _shell.Received(1).OpenUrl(new Uri("mailto:sponsor@poli0981.dev"));
    }

    /// <summary>Every package family the app ships is credited on the About page.</summary>
    [Fact]
    public void ThirdPartyList_CoversShippedPackages()
    {
        var props = Path.Combine(RepositoryRoot(), "Directory.Packages.props");
        var shipped = XDocument.Load(props).Descendants("PackageVersion")
            .Select(e => (string)e.Attribute("Include")!)
            .Where(id => !IsBuildOrTestOnly(id))
            .ToList();
        var credited = string.Join(" | ", _vm.ThirdPartyItems.Select(i => i.Name));

        foreach (var id in shipped)
        {
            var family = id switch
            {
                "WPF-UI" => "WPF UI",
                _ when id.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) => "Microsoft.Extensions.*",
                _ when id.StartsWith("Serilog", StringComparison.Ordinal) => "Serilog",
                _ when id.StartsWith("Vortice.", StringComparison.Ordinal) => "Vortice.Windows",
                _ when id.StartsWith("H.NotifyIcon", StringComparison.Ordinal) => "H.NotifyIcon",
                "System.Drawing.Common" => ".NET",
                _ => id,
            };
            credited.Should().Contain(family, "{0} ships with the app", id);
        }
    }

    private static bool IsBuildOrTestOnly(string id) =>
        id is "Microsoft.Windows.CsWin32" or "xunit.v3" or "AwesomeAssertions" or "NSubstitute" or "Xunit.StaFact" or "FlaUI.UIA3"
        || id.StartsWith("Microsoft.Testing.", StringComparison.Ordinal)
        || id.StartsWith("Microsoft.Extensions.TimeProvider.Testing", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CaptureImage.sln")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("CaptureImage.sln not found.");
    }
}

public sealed class PreviewViewModelTests
{
    private readonly IOcrService _ocr = Substitute.For<IOcrService>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IToastService _toasts = Substitute.For<IToastService>();
    private readonly PreviewViewModel _vm;

    public PreviewViewModelTests()
    {
        _vm = new PreviewViewModel(new CapturedFrame(2, 1, 8, new byte[8]), "Elden Ring", _ocr, _clipboard, _toasts, new KeyLocalization());
    }

    [Fact]
    public async Task Save_CompletesWithTrue()
    {
        _vm.SaveCommand.Execute(null);
        _vm.DiscardCommand.Execute(null);

        (await _vm.Result).Should().BeTrue();
        _vm.Dimensions.Should().Be("2 × 1");
    }

    [Fact]
    public async Task Discard_CompletesWithFalse()
    {
        _vm.DiscardCommand.Execute(null);

        (await _vm.Result).Should().BeFalse();
    }

    [Fact]
    public async Task ExtractText_CopiesRecognizedText()
    {
        _ocr.RecognizeAsync(Arg.Any<CapturedFrame>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new OcrResult(OcrStatus.Success, "HP 100 MP 50"));
        _clipboard.CopyTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await _vm.ExtractTextCommand.ExecuteAsync(null);

        await _clipboard.Received(1).CopyTextAsync("HP 100 MP 50", Arg.Any<CancellationToken>());
        _toasts.Received(1).Show(ToastKind.Success, "Toast_OcrSuccess", "HP 100 MP 50", null);
        _vm.IsRecognizing.Should().BeFalse();
    }

    [Theory]
    [InlineData(OcrStatus.NoText, ToastKind.Info, "Toast_OcrEmpty")]
    [InlineData(OcrStatus.EngineUnavailable, ToastKind.Warning, "Toast_OcrUnavailable")]
    [InlineData(OcrStatus.Failed, ToastKind.Error, "Toast_Error")]
    public async Task OcrFeedback_MapsEveryStatus(OcrStatus status, ToastKind kind, string title)
    {
        await OcrFeedback.ReportAsync(new OcrResult(status, string.Empty), _clipboard, _toasts, new KeyLocalization());

        _toasts.Received(1).Show(kind, title, Arg.Any<string>(), null);
    }
}

public sealed class ServicesTests
{
    [Fact]
    public void AppInfo_StripsBuildMetadataAndShortensTheCommit()
    {
        var info = AppInfo.FromAssembly(typeof(ServicesTests).Assembly);

        info.Version.Should().NotContain("+");
        (info.Commit is null || info.Commit.Length <= 7).Should().BeTrue();
    }

    [Fact]
    public void AppInfo_DisplayVersion()
    {
        new AppInfo("2.0.0", null).DisplayVersion.Should().Be("2.0.0");
        new AppInfo("2.0.0", "abc1234").DisplayVersion.Should().Be("2.0.0 (abc1234)");
    }

    [Fact]
    public void LocalizedOption_RefreshRaisesLabel()
    {
        var label = "a";
        var option = new LocalizedOption<int>(1, () => label);
        var raised = 0;
        option.PropertyChanged += (_, _) => raised++;

        label = "b";
        option.Refresh();

        raised.Should().Be(1);
        option.Label.Should().Be("b");
        option.ToString().Should().Be("b");
    }
}
