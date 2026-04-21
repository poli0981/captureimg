using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using System.Threading.Tasks;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Settings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Settings;

public class JsonSettingsStoreTests
{
    private static string ExpectedPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "CaptureImage", "settings.json");
        }
    }

    private static (JsonSettingsStore store, MockFileSystem fs) Build()
    {
        var fs = new MockFileSystem();
        var store = new JsonSettingsStore(fs, NullLogger<JsonSettingsStore>.Instance);
        return (store, fs);
    }

    [Fact]
    public async Task LoadAsync_NoFileExists_UsesDefaults()
    {
        var (store, _) = Build();

        await store.LoadAsync();

        store.Current.Version.Should().Be(2);
        store.Current.Culture.Should().Be("en-US");
        store.Current.CaptureHotkey.Should().Be(HotkeyBinding.Default);
        store.Current.Capture.DefaultFormat.Should().Be(ImageFormat.Png);
        store.Current.LogLevel.Should().Be("Information");
    }

    [Fact]
    public async Task Update_MutatorApplied_CurrentReflectsChange()
    {
        var (store, _) = Build();
        await store.LoadAsync();

        store.Update(s => s with { Culture = "vi-VN" });

        store.Current.Culture.Should().Be("vi-VN");
    }

    [Fact]
    public async Task Update_FiresChangedEvent()
    {
        var (store, _) = Build();
        await store.LoadAsync();

        var fired = 0;
        store.Changed += (_, _) => fired++;

        store.Update(s => s with { Culture = "ar-SA" });

        fired.Should().Be(1);
    }

    [Fact]
    public async Task FlushAsync_WritesJsonToDisk()
    {
        var (store, fs) = Build();
        await store.LoadAsync();

        store.Update(s => s with { Culture = "vi-VN" });
        await store.FlushAsync();

        fs.File.Exists(ExpectedPath).Should().BeTrue();
        var written = fs.File.ReadAllText(ExpectedPath);
        written.Should().Contain("\"culture\": \"vi-VN\"");
    }

    [Fact]
    public async Task FlushAsync_ThenLoadAsync_RoundTripsAllFields()
    {
        var (store, fs) = Build();
        await store.LoadAsync();

        var newHotkey = new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x70); // Ctrl+Alt+F1
        store.Update(s => s with
        {
            Culture = "ar-SA",
            Theme = "Dark",
            CaptureHotkey = newHotkey,
            Capture = s.Capture with
            {
                DefaultFormat = ImageFormat.Webp,
                JpegQuality = 75,
                WebpQuality = 60,
                PreviewBeforeSave = true,
                FileNameTemplate = "custom_{yyyy}",
            },
            UI = s.UI with
            {
                StartMinimized = true,
                SoundEnabled = false,
            },
        });
        await store.FlushAsync();

        // Load into a fresh store
        var (store2, _) = (new JsonSettingsStore(fs, NullLogger<JsonSettingsStore>.Instance), fs);
        await store2.LoadAsync();

        store2.Current.Culture.Should().Be("ar-SA");
        store2.Current.Theme.Should().Be("Dark");
        store2.Current.CaptureHotkey.Should().Be(newHotkey);
        store2.Current.Capture.DefaultFormat.Should().Be(ImageFormat.Webp);
        store2.Current.Capture.JpegQuality.Should().Be(75);
        store2.Current.Capture.WebpQuality.Should().Be(60);
        store2.Current.Capture.PreviewBeforeSave.Should().BeTrue();
        store2.Current.Capture.FileNameTemplate.Should().Be("custom_{yyyy}");
        store2.Current.UI.StartMinimized.Should().BeTrue();
        store2.Current.UI.SoundEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_VersionNewerThanSupported_RevertsToDefaults()
    {
        var (store, fs) = Build();
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appDataRoot, "CaptureImage");
        fs.Directory.CreateDirectory(dir);
        fs.File.WriteAllText(ExpectedPath, "{\"version\": 999, \"culture\": \"fr-FR\"}");

        await store.LoadAsync();

        store.Current.Version.Should().Be(2);
        store.Current.Culture.Should().Be("en-US");
    }

    [Fact]
    public async Task LoadAsync_CorruptJson_RevertsToDefaults()
    {
        var (store, fs) = Build();
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appDataRoot, "CaptureImage");
        fs.Directory.CreateDirectory(dir);
        fs.File.WriteAllText(ExpectedPath, "{ not valid json");

        await store.LoadAsync();

        store.Current.Culture.Should().Be("en-US");
    }

    [Fact]
    public async Task ExportAsync_WritesCurrentSettings()
    {
        var (store, fs) = Build();
        await store.LoadAsync();
        store.Update(s => s with { Culture = "ar-SA" });

        var exportPath = @"C:\exports\my_settings.json";
        fs.Directory.CreateDirectory(@"C:\exports");
        await store.ExportAsync(exportPath);

        fs.File.Exists(exportPath).Should().BeTrue();
        var content = fs.File.ReadAllText(exportPath);
        content.Should().Contain("\"culture\": \"ar-SA\"");
    }

    [Fact]
    public async Task ImportAsync_AdoptsExportedSettings()
    {
        var (store, fs) = Build();
        await store.LoadAsync();

        // Create a valid settings file to import.
        var importPath = @"C:\imports\from_other_machine.json";
        fs.Directory.CreateDirectory(@"C:\imports");
        var doc = """
            {
                "version": 1,
                "culture": "vi-VN",
                "theme": "Dark",
                "captureHotkey": { "modifiers": "Control, Shift", "virtualKey": 123 },
                "capture": {
                    "defaultFormat": "Jpeg",
                    "jpegQuality": 80,
                    "webpQuality": 70,
                    "outputDirectory": "D:\\Captures",
                    "fileNameTemplate": "{Game}_{ss}",
                    "previewBeforeSave": true
                },
                "ui": { "startMinimized": false, "minimizeToTray": true, "showLogViewer": true, "soundEnabled": true }
            }
            """;
        fs.File.WriteAllText(importPath, doc);

        await store.ImportAsync(importPath);

        store.Current.Culture.Should().Be("vi-VN");
        store.Current.Capture.DefaultFormat.Should().Be(ImageFormat.Jpeg);
        store.Current.Capture.JpegQuality.Should().Be(80);
        store.Current.Capture.OutputDirectory.Should().Be(@"D:\Captures");
        store.Current.Capture.PreviewBeforeSave.Should().BeTrue();
        store.Current.UI.ShowLogViewer.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_MissingFile_Throws()
    {
        var (store, _) = Build();
        await store.LoadAsync();

        var act = async () => await store.ImportAsync(@"C:\missing\nope.json");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ImportAsync_VersionTooNew_Throws()
    {
        var (store, fs) = Build();
        await store.LoadAsync();
        var importPath = @"C:\imports\future.json";
        fs.Directory.CreateDirectory(@"C:\imports");
        fs.File.WriteAllText(importPath, "{\"version\": 999, \"culture\": \"fr-FR\"}");

        var act = async () => await store.ImportAsync(importPath);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task FlushAsync_UsesAtomicReplace_NoTempFileLeftOver()
    {
        var (store, fs) = Build();
        await store.LoadAsync();
        store.Update(s => s with { Culture = "vi-VN" });

        await store.FlushAsync();

        fs.File.Exists(ExpectedPath).Should().BeTrue();
        fs.File.Exists(ExpectedPath + ".tmp").Should().BeFalse();
    }
}
