using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CaptureImage.Core.Abstractions;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.About;

/// <summary>
/// About tab. Shows app metadata, developer info, the AI assistance disclosure, the
/// capture / translation / liability disclaimers, and links to the shipped legal files
/// (DISCLAIMER, PRIVACY, TERMS, THIRD_PARTY_NOTICES, LICENSE, NOTICE, CONTRIBUTORS).
/// </summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    private readonly ILogger<AboutViewModel> _logger;

    public ILocalizationService Localization { get; }

    public AboutViewModel(ILocalizationService localization, ILogger<AboutViewModel> logger)
    {
        Localization = localization;
        _logger = logger;

        var asm = typeof(AboutViewModel).Assembly;
        AppVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";

        ThirdPartyItems = new ObservableCollection<ThirdPartyItem>
        {
            new("Avalonia",                   "MIT",        "https://github.com/AvaloniaUI/Avalonia"),
            new("SkiaSharp",                  "MIT",        "https://github.com/mono/SkiaSharp"),
            new("CommunityToolkit.Mvvm",      "MIT",        "https://github.com/CommunityToolkit/dotnet"),
            new("Microsoft.Extensions.*",     "MIT",        "https://github.com/dotnet/runtime"),
            new("Serilog",                    "Apache-2.0", "https://github.com/serilog/serilog"),
            new("Vortice.Direct3D11 / DXGI",  "MIT",        "https://github.com/amerkoleci/Vortice.Windows"),
            new("SixLabors.ImageSharp",       "Apache-2.0", "https://github.com/SixLabors/ImageSharp"),
            new("SharpHook",                  "MIT",        "https://github.com/TolikPylypchuk/SharpHook"),
            new("libuiohook",                 "GPL-3.0",    "https://github.com/kwhat/libuiohook"),
            new("Stateless",                  "Apache-2.0", "https://github.com/dotnet-state-machine/stateless"),
            new("Velopack",                   "MIT",        "https://github.com/velopack/velopack"),
            new("Inter typeface",             "OFL-1.1",    "https://github.com/rsms/inter"),
        };
    }

    public string AppName => "CaptureImage";

    public string AppVersion { get; }

    public string License => "GPL-3.0-or-later";

    public string Developer => "poli0981";

    public string RepositoryUrl => "https://github.com/poli0981/captureimg";

    /// <summary>
    /// Plain-text dev disclosure about AI involvement — kept in code (not resx) because
    /// the content describes the development process, not the user-facing functionality.
    /// </summary>
    public string AiAssistanceNotice =>
        "Development of CaptureImage was carried out with significant assistance from the " +
        "Claude Opus 4.6 large language model (Anthropic), used through Claude Code. AI " +
        "assistance included code generation, unit tests, P/Invoke signatures, XAML layouts, " +
        "commit messages, and documentation. Every change was reviewed, accepted, and tested " +
        "by the human maintainer before landing. Commits that involve meaningful AI authorship " +
        "carry a Co-Authored-By trailer naming the model. See docs/CONTRIBUTORS.md for details.";

    public string TranslationDisclaimer =>
        "Vietnamese and Arabic language packs are machine-assisted. Terminology may not match " +
        "native conventions, and some technical words (hotkey, toast, frame, etc.) are kept in " +
        "English on purpose. Native speakers willing to proofread are warmly invited to submit " +
        "pull requests against the .resx files in src/CaptureImage.UI/Resources/Strings/.";

    public string CaptureLimitationDisclaimer =>
        "CaptureImage can capture most windowed and borderless games via Windows.Graphics.Capture, " +
        "but not every title works. DRM-protected surfaces, exclusive-fullscreen Direct3D 9 games, " +
        "and titles with anti-cheat hooks may return black frames or refuse capture. A GDI PrintWindow " +
        "fallback is attempted for legacy apps. See docs/legal/DISCLAIMER.md §2 for the full list.";

    public string LiabilityDisclaimer =>
        "CaptureImage is distributed under GPL-3.0-or-later AS IS, without warranty of any kind. " +
        "The authors are not liable for lost screenshots, game crashes, anti-cheat sanctions, or " +
        "data loss. Use at your own risk. See docs/legal/DISCLAIMER.md for the full text.";

    public ObservableCollection<ThirdPartyItem> ThirdPartyItems { get; }

    // -- legal document open commands ---------------------------------------

    [RelayCommand]
    private void OpenDisclaimer() => OpenDocument("docs/legal/DISCLAIMER.md");

    [RelayCommand]
    private void OpenPrivacy() => OpenDocument("docs/legal/PRIVACY.md");

    [RelayCommand]
    private void OpenTerms() => OpenDocument("docs/legal/TERMS.md");

    [RelayCommand]
    private void OpenThirdParty() => OpenDocument("docs/legal/THIRD_PARTY_NOTICES.md");

    [RelayCommand]
    private void OpenContributors() => OpenDocument("docs/CONTRIBUTORS.md");

    [RelayCommand]
    private void OpenLicense() => OpenDocument("LICENSE");

    [RelayCommand]
    private void OpenRepository() => OpenUrl(RepositoryUrl);

    /// <summary>
    /// Resolve a doc path relative to the app's base directory and open it with the OS
    /// default handler (Markdown files open in whichever editor the user has associated,
    /// plain LICENSE opens in Notepad or similar).
    /// </summary>
    private void OpenDocument(string relativePath)
    {
        try
        {
            var basePath = AppContext.BaseDirectory;
            var full = Path.Combine(basePath, relativePath);
            if (!File.Exists(full))
            {
                _logger.LogWarning("Document not found: {Path}", full);
                return;
            }
            Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open document {Path}.", relativePath);
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open URL {Url}.", url);
        }
    }
}
