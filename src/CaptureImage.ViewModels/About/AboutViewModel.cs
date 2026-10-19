// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Runtime.InteropServices;
using CaptureImage.Core.Abstractions;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.Input;

namespace CaptureImage.ViewModels.About;

/// <summary>A library CaptureImage ships, with its license.</summary>
public sealed record ThirdPartyItem(string Name, string License, Uri Url);

/// <summary>A sponsor / donation destination (mirror of <c>.github/FUNDING.yml</c>).</summary>
public sealed record SponsorLink(string Name, Uri Url);

/// <summary>
/// About page: version, contact, links, sponsors, legal documents and third-party notices.
/// </summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    public const string RepositoryUrl = "https://github.com/poli0981/captureimg";
    public const string ContactEmail = "contact@poli0981.dev";
    public const string SponsorEmail = "sponsor@poli0981.dev";
    public const string LinksPageUrl = "https://poli0981.dev/links";

    private readonly IShellService _shell;

    public AboutViewModel(IShellService shell, ILocalizationService localization, AppInfo appInfo)
    {
        _shell = shell;
        Localization = localization;
        AppInfo = appInfo;
    }

    public ILocalizationService Localization { get; }

    public AppInfo AppInfo { get; }

    public string AppName => "CaptureImage";

    public string Version => AppInfo.DisplayVersion;

    public string Runtime => RuntimeInformation.FrameworkDescription;

    public string OperatingSystem => RuntimeInformation.OSDescription;

    public string License => "GPL-3.0-or-later";

    public string Developer => "poli0981";

    public string Contact => ContactEmail;

    public string LinksPage => LinksPageUrl;

    /// <summary>Libraries in the shipped app (build-only tools are not listed).</summary>
    public IReadOnlyList<ThirdPartyItem> ThirdPartyItems { get; } =
    [
        new(".NET (WPF, runtime)", "MIT", new Uri("https://github.com/dotnet/runtime")),
        new("WPF UI", "MIT", new Uri("https://github.com/lepoco/wpfui")),
        new("H.NotifyIcon", "MIT", new Uri("https://github.com/HavenDV/H.NotifyIcon")),
        new("CommunityToolkit.Mvvm", "MIT", new Uri("https://github.com/CommunityToolkit/dotnet")),
        new("Microsoft.Extensions.*", "MIT", new Uri("https://github.com/dotnet/runtime")),
        new("Serilog", "Apache-2.0", new Uri("https://github.com/serilog/serilog")),
        new("SkiaSharp", "MIT", new Uri("https://github.com/mono/SkiaSharp")),
        new("Vortice.Windows", "MIT", new Uri("https://github.com/amerkoleci/Vortice.Windows")),
        new("Velopack", "MIT", new Uri("https://github.com/velopack/velopack")),
    ];

    public IReadOnlyList<SponsorLink> SponsorLinks { get; } =
    [
        new("GitHub Sponsors", new Uri("https://github.com/sponsors/poli0981")),
        new("Ko-fi", new Uri("https://ko-fi.com/skullmute")),
        new("Buy Me a Coffee", new Uri("https://buymeacoffee.com/skullmute")),
        new("Patreon", new Uri("https://patreon.com/skullmute")),
    ];

    [RelayCommand]
    private void OpenRepository() => _shell.OpenUrl(new Uri(RepositoryUrl));

    [RelayCommand]
    private void OpenReleaseNotes() => _shell.OpenUrl(new Uri(RepositoryUrl + "/releases"));

    [RelayCommand]
    private void ReportBug() => _shell.OpenUrl(new Uri(RepositoryUrl + "/issues/new?template=bug_report.yml"));

    [RelayCommand]
    private void RequestFeature() => _shell.OpenUrl(new Uri(RepositoryUrl + "/issues/new?template=feature_request.yml"));

    [RelayCommand]
    private void OpenDiscussions() => _shell.OpenUrl(new Uri(RepositoryUrl + "/discussions"));

    [RelayCommand]
    private void EmailContact() => _shell.OpenUrl(new Uri("mailto:" + ContactEmail));

    [RelayCommand]
    private void EmailSponsor() => _shell.OpenUrl(new Uri("mailto:" + SponsorEmail));

    [RelayCommand]
    private void OpenLinksPage() => _shell.OpenUrl(new Uri(LinksPageUrl));

    [RelayCommand]
    private void OpenLink(Uri? url)
    {
        if (url is not null && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeMailto))
        {
            _shell.OpenUrl(url);
        }
    }

    /// <summary>
    /// Open a document shipped next to the executable (<c>LICENSE</c>, <c>docs/legal/PRIVACY.md</c>…);
    /// when running from source and the file is missing, open it on GitHub instead.
    /// </summary>
    [RelayCommand]
    private void OpenDocument(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            return;
        }

        var local = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(local))
        {
            _shell.Open(local);
        }
        else
        {
            _shell.OpenUrl(new Uri($"{RepositoryUrl}/blob/main/{relativePath.Replace('\\', '/')}"));
        }
    }
}
