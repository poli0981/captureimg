// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;

namespace CaptureImage.App.UiTests;

/// <summary>
/// One running copy of the app with its own data folder and single-instance scope, so tests
/// never see the user's settings or talk to a copy they have open.
/// </summary>
internal sealed class AppSession : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    private AppSession(Application application, UIA3Automation automation, string dataDirectory, string instanceId)
    {
        Application = application;
        Automation = automation;
        DataDirectory = dataDirectory;
        InstanceId = instanceId;
    }

    public Application Application { get; }

    public UIA3Automation Automation { get; }

    public string DataDirectory { get; }

    public string InstanceId { get; }

    public static bool Enabled => Environment.GetEnvironmentVariable("CAPTUREIMAGE_UI_TESTS") == "1";

    /// <summary>Skip the calling test unless UI tests were requested.</summary>
    public static void RequireEnabled()
    {
        if (!Enabled)
        {
            Assert.Skip("UI tests drive the real desktop; set CAPTUREIMAGE_UI_TESTS=1 to run them.");
        }
    }

    public static AppSession Start(string? settingsJson = null)
    {
        RequireEnabled();
        var dataDirectory = Directory.CreateTempSubdirectory("captureimage-ui-").FullName;
        if (settingsJson is not null)
        {
            File.WriteAllText(Path.Combine(dataDirectory, "settings.json"), settingsJson);
        }

        var instanceId = "ui" + Guid.NewGuid().ToString("N")[..12];
        var application = Application.Launch(StartInfo(dataDirectory, instanceId));
        application.WaitWhileMainHandleIsMissing(StartTimeout);
        return new AppSession(application, new UIA3Automation(), dataDirectory, instanceId);
    }

    public static ProcessStartInfo StartInfo(string dataDirectory, string instanceId) =>
        new(AppExecutable())
        {
            ArgumentList = { "--data-dir", dataDirectory, "--instance-id", instanceId },
            UseShellExecute = false,
        };

    public Window MainWindow() => Application.GetMainWindow(Automation, StartTimeout)
        ?? throw new InvalidOperationException("The main window did not appear.");

    /// <summary>Wait for an element by automation id under <paramref name="root"/>.</summary>
    public static AutomationElement Find(AutomationElement root, string automationId, TimeSpan? timeout = null)
    {
        var result = Retry.WhileNull(
            () => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout ?? TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(200));
        return result.Result ?? throw new InvalidOperationException($"Element '{automationId}' not found.");
    }

    public void Dispose()
    {
        try
        {
            if (!Application.HasExited)
            {
                Application.Kill();
            }
        }
        finally
        {
            Automation.Dispose();
            Application.Dispose();
            TryDelete(DataDirectory);
        }
    }

    private static string AppExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("CAPTUREIMAGE_APP_EXE");
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        // tests/CaptureImage.App.UiTests/bin/<Configuration>/<tfm>/ -> src/CaptureImage.App/bin/<Configuration>/<tfm>/
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var tfm = testOutput.Name;
        var configuration = testOutput.Parent!.Name;
        var root = testOutput.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var exe = Path.Combine(root, "src", "CaptureImage.App", "bin", configuration, tfm, "CaptureImage.App.exe");
        return File.Exists(exe) ? exe : throw new FileNotFoundException("Build CaptureImage.App first.", exe);
    }

    /// <summary>Best effort: a log file can still be closing; the temp folder is cleaned up eventually.</summary>
    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the OS temp cleanup.
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesktopCollection
{
    public const string Name = "Desktop (one app at a time)";
}
