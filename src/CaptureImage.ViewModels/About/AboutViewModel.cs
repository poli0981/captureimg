using System.Collections.ObjectModel;
using System.Diagnostics;
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

        // About view binds every label through `{Binding Localization[About_*]}`. Push an
        // explicit PropertyChanged on culture switch so the compiled indexer bindings
        // re-resolve their path — the Localization service's own Item[] notification
        // isn't picked up through the intermediate property in every case.
        //
        // Disclaimer bodies are computed properties reading through Localization[...] but
        // the view binds them by property name (`{Binding TranslationDisclaimer}`), so the
        // indexer refresh alone won't wake those bindings — raise each disclaimer property
        // explicitly so their bound TextBlocks retranslate in place.
        //
        // AboutViewModel is a DI singleton with process-long lifetime (nav owns it), so we
        // skip IDisposable and rely on the container tearing the subscription down on app
        // exit — adding IDisposable here would be cosmetic and the leak is theoretical.
        Localization.PropertyChanged += OnLocalizationChanged;

        var asm = typeof(AboutViewModel).Assembly;
        AppVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";

        ThirdPartyItems = new ObservableCollection<ThirdPartyItem>
        {
            new("Windows App SDK / WinUI 3",  "MIT",        "https://github.com/microsoft/WindowsAppSDK"),
            new("H.NotifyIcon.WinUI",         "MIT",        "https://github.com/HavenDV/H.NotifyIcon"),
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
        };
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            OnPropertyChanged(nameof(Localization));
            OnPropertyChanged(nameof(TranslationDisclaimer));
            OnPropertyChanged(nameof(CaptureLimitationDisclaimer));
            OnPropertyChanged(nameof(LiabilityDisclaimer));
        }
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

    public string TranslationDisclaimer => Localization["About_TranslationDisclaimerBody"];

    public string CaptureLimitationDisclaimer => Localization["About_CaptureLimitationDisclaimerBody"];

    public string LiabilityDisclaimer => Localization["About_LiabilityDisclaimerBody"];

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
                _logger.LogWarning("Document not found at {Path}.", full);
                return;
            }
            Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't open the document at {Path}.", relativePath);
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
            _logger.LogError(ex, "Couldn't open the link {Url}.", url);
        }
    }
}
