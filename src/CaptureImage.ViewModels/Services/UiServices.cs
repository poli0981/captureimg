// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Reflection;
using CaptureImage.Core.Models;

namespace CaptureImage.ViewModels.Services;

/// <summary>File/folder pickers and confirmation dialogs, implemented by the UI layer.</summary>
public interface IDialogService
{
    Task<string?> PickFolderAsync(string title, string? initialFolder = null);

    /// <param name="title">Dialog title.</param>
    /// <param name="filter">Win32 filter string, e.g. <c>Programs (*.exe)|*.exe</c>.</param>
    Task<string?> PickFileAsync(string title, string filter);

    Task<string?> SaveFileAsync(string title, string defaultFileName, string filter);

    /// <summary>True when the user confirms.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText);
}

/// <summary>Applies System / Light / Dark / HighContrast.</summary>
public interface IThemeService
{
    void Apply(string theme);
}

/// <summary>Version information shown in the title bar, About page and diagnostics.</summary>
public sealed record AppInfo(string Version, string? Commit)
{
    /// <summary><c>2.0.0</c>, or <c>2.0.0 (abc1234)</c> when the build recorded its commit.</summary>
    public string DisplayVersion => string.IsNullOrEmpty(Commit) ? Version : $"{Version} ({Commit})";

    /// <summary>Read from the entry assembly: informational version plus <c>SourceRevisionId</c> metadata.</summary>
    public static AppInfo FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            version = version[..plus];
        }

        var commit = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SourceRevisionId")?.Value;
        if (commit is { Length: > 7 })
        {
            commit = commit[..7];
        }

        return new AppInfo(version, string.IsNullOrWhiteSpace(commit) ? null : commit);
    }
}

/// <summary>Navigation keys understood by the shell.</summary>
public static class PageKeys
{
    public const string Dashboard = "dashboard";
    public const string Library = "library";
    public const string Profiles = "profiles";
    public const string Settings = "settings";
    public const string Logs = "logs";
    public const string Update = "update";
    public const string About = "about";
}

/// <summary>Bring the main window to the front and, when <paramref name="PageKey"/> is set, show that page.</summary>
/// <param name="PageKey">One of <see cref="PageKeys"/>, or <c>null</c> to keep the current page.</param>
public sealed record NavigateMessage(string? PageKey);

/// <summary>Ask the Profiles page to start a new profile for a window.</summary>
public sealed record CreateProfileMessage(GameTarget Target);
