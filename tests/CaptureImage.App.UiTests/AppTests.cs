// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Diagnostics;
using AwesomeAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using Xunit;

namespace CaptureImage.App.UiTests;

[Collection(DesktopCollection.Name)]
public sealed class AppTests
{
    private static readonly string[] Pages = ["Dashboard", "Library", "Profiles", "Settings", "Logs", "Update", "About"];

    [Fact]
    public void Starts_WithTheMainWindowAndDashboard()
    {
        using var session = AppSession.Start();
        var window = session.MainWindow();

        window.Title.Should().Be("CaptureImage");
        AppSession.Find(window, "DashboardPage").Should().NotBeNull();
        AppSession.Find(window, "Dashboard_ToggleArm").Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EveryPage_Opens()
    {
        using var session = AppSession.Start();
        var window = session.MainWindow();

        foreach (var page in Pages)
        {
            AppSession.Find(window, $"Nav_{page}").Click();
            AppSession.Find(window, $"{page}Page").Should().NotBeNull($"{page} should open");
        }
    }

    [Fact]
    public void Arm_ShowsTheArmedStatus()
    {
        using var session = AppSession.Start();
        var window = session.MainWindow();
        var status = AppSession.Find(window, "Dashboard_Status");
        var idle = status.Name;

        AppSession.Find(window, "Dashboard_ToggleArm").AsButton().Invoke();

        Retry.WhileTrue(() => status.Name == idle, TimeSpan.FromSeconds(5)).Success.Should().BeTrue();
    }

    [Fact]
    public void Language_SwitchesTheWholeUi()
    {
        using var session = AppSession.Start("""{ "version": 3, "culture": "en-US" }""");
        var window = session.MainWindow();
        AppSession.Find(window, "Nav_Settings").Click();

        var language = AppSession.Find(window, "Settings_Language").AsComboBox();
        language.Select("Tiếng Việt (Việt Nam)");

        Retry.WhileFalse(
            () => AppSession.Find(window, "Nav_Settings").Name.Contains("Cài đặt", StringComparison.Ordinal),
            TimeSpan.FromSeconds(5)).Success.Should().BeTrue();
        File.ReadAllText(Path.Combine(session.DataDirectory, "settings.json")).Should().Contain("vi-VN");
    }

    [Fact]
    public void Arabic_LaysOutRightToLeft()
    {
        using var session = AppSession.Start("""{ "version": 3, "culture": "ar-SA" }""");
        var window = session.MainWindow();

        var nav = AppSession.Find(window, "Nav_Dashboard").BoundingRectangle;
        var page = AppSession.Find(window, "DashboardPage").BoundingRectangle;

        nav.Left.Should().BeGreaterThan(page.Left, "the navigation pane moves to the right in Arabic");
    }

    [Fact]
    public void HotkeyRecorder_SavesANewCombination()
    {
        using var session = AppSession.Start();
        var window = session.MainWindow();
        AppSession.Find(window, "Nav_Settings").Click();
        var record = AppSession.Find(window, "Hotkey_Window_Record").AsButton();

        record.Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.F9);

        // Ctrl (0x0002) + Alt (0x0001) + F9 (VK 0x78 = 120), written after the store's short debounce.
        Retry.WhileFalse(() => ReadSettings(session).Contains("\"virtualKey\": 120", StringComparison.Ordinal), TimeSpan.FromSeconds(10))
            .Success.Should().BeTrue();
    }

    [Fact]
    public void Closing_HidesToTheTray_AndASecondLaunchBringsItBack()
    {
        using var session = AppSession.Start();
        var window = session.MainWindow();

        window.Close();
        Retry.WhileTrue(() => window.IsOffscreen == false && window.IsAvailable, TimeSpan.FromSeconds(5));
        session.Application.HasExited.Should().BeFalse("closing hides to the tray by default");

        using var second = Process.Start(AppSession.StartInfo(session.DataDirectory, session.InstanceId))!;
        second.WaitForExit(TimeSpan.FromSeconds(15)).Should().BeTrue("a second copy hands over and exits");
        second.ExitCode.Should().Be(0);

        Retry.WhileNull(() => session.Application.GetMainWindow(session.Automation, TimeSpan.FromSeconds(1)), TimeSpan.FromSeconds(10))
            .Result.Should().NotBeNull();
    }

    private static string ReadSettings(AppSession session)
    {
        try
        {
            return File.ReadAllText(Path.Combine(session.DataDirectory, "settings.json"));
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }
}
