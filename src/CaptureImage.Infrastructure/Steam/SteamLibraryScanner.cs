// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace CaptureImage.Infrastructure.Steam;

/// <summary>
/// Maps executables to installed Steam apps by reading <c>libraryfolders.vdf</c> and the
/// <c>appmanifest_*.acf</c> files of every library. Scanned once, lazily, thread-safe; a scan that
/// fails is retried a minute later rather than failing every lookup from then on.
/// </summary>
public sealed partial class SteamLibraryScanner : ISteamDetector
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private readonly Func<string?> _steamRoot;
    private readonly ILogger<SteamLibraryScanner> _logger;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private IReadOnlyList<SteamLibrary>? _libraries;
    private DateTimeOffset _retryAt = DateTimeOffset.MinValue;

    /// <summary>Production constructor: Steam's install folder comes from the registry.</summary>
    public SteamLibraryScanner(ILogger<SteamLibraryScanner> logger)
        : this(FindSteamRootInRegistry, logger)
    {
    }

    /// <param name="steamRoot">Returns Steam's install folder, or <c>null</c> when Steam isn't installed.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="timeProvider">Clock for the retry delay (tests); the system clock by default.</param>
    public SteamLibraryScanner(Func<string?> steamRoot, ILogger<SteamLibraryScanner> logger, TimeProvider? timeProvider = null)
    {
        _steamRoot = steamRoot;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The scanned libraries, or none while a failed scan waits for its retry.</summary>
    private IReadOnlyList<SteamLibrary> Libraries
    {
        get
        {
            lock (_gate)
            {
                if (_libraries is not null)
                {
                    return _libraries;
                }

                var now = _time.GetUtcNow();
                if (now < _retryAt)
                {
                    return [];
                }

                try
                {
                    return _libraries = LoadLibraries();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
                    or ArgumentException or NotSupportedException or InvalidOperationException)
                {
                    LogScanFailed(ex);
                    _retryAt = now + RetryDelay;
                    return [];
                }
            }
        }
    }

    public SteamAppInfo? TryGetAppInfo(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        string normalized;
        try
        {
            normalized = Normalize(executablePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        foreach (var library in Libraries)
        {
            var common = Normalize(Path.Combine(library.Path, "steamapps", "common")) + Path.DirectorySeparatorChar;
            if (!normalized.StartsWith(common, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var installDir = normalized[common.Length..].Split(['\\', '/'], 2, StringSplitOptions.RemoveEmptyEntries)[0];
            return library.Apps.Values.FirstOrDefault(app => app.InstallDir.Equals(installDir, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static string? FindSteamRootInRegistry()
    {
        string?[] candidates =
        [
            Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string,
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")?.GetValue("InstallPath") as string,
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam")?.GetValue("InstallPath") as string,
        ];
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path));
    }

    private List<SteamLibrary> LoadLibraries()
    {
        string? root;
        try
        {
            root = _steamRoot();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            LogRootLookupFailed(ex);
            return [];
        }

        if (root is null)
        {
            LogNoSteam();
            return [];
        }

        var libraryFolders = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        IReadOnlyList<string> paths;
        try
        {
            paths = File.Exists(libraryFolders) ? ExtractLibraryPaths(VdfParser.Parse(File.ReadAllText(libraryFolders))) : [root];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidDataException)
        {
            LogParseFailed(ex, "libraryfolders.vdf");
            paths = [root];
        }

        var libraries = paths.Select(path => new SteamLibrary(path, LoadApps(path))).ToList();
        LogScanned(libraries.Count, libraries.Sum(l => l.Apps.Count));
        return libraries;
    }

    private static List<string> ExtractLibraryPaths(VdfNode root)
    {
        var container = root.Key.Equals("libraryfolders", StringComparison.OrdinalIgnoreCase) ? root : root["libraryfolders"] ?? root;
        return [.. container.BranchChildren().Select(child => child.ValueOf("path")).OfType<string>().Where(p => !string.IsNullOrWhiteSpace(p))];
    }

    private Dictionary<uint, SteamAppInfo> LoadApps(string libraryPath)
    {
        var apps = new Dictionary<uint, SteamAppInfo>();
        var steamapps = Path.Combine(libraryPath, "steamapps");
        if (!Directory.Exists(steamapps))
        {
            return apps;
        }

        List<string> manifests;
        try
        {
            manifests = [.. Directory.EnumerateFiles(steamapps, "appmanifest_*.acf")];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // One unreadable library (offline drive, no permission) must not hide the others.
            LogLibraryUnreadable(ex, libraryPath);
            return apps;
        }

        foreach (var manifest in manifests)
        {
            try
            {
                var root = VdfParser.Parse(File.ReadAllText(manifest));
                var state = root.Key.Equals("AppState", StringComparison.OrdinalIgnoreCase) ? root : root["AppState"];
                if (state is null
                    || !uint.TryParse(state.ValueOf("appid"), out var appId)
                    || state.ValueOf("installdir") is not { Length: > 0 } installDir)
                {
                    continue;
                }
                apps[appId] = new SteamAppInfo(appId, state.ValueOf("name") ?? string.Empty, installDir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidDataException)
            {
                LogParseFailed(ex, Path.GetFileName(manifest));
            }
        }
        return apps;
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd('\\', '/');

    [LoggerMessage(Level = LogLevel.Debug, Message = "Steam is not installed; no Steam badges.")]
    private partial void LogNoSteam();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not look up Steam's install folder.")]
    private partial void LogRootLookupFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not parse Steam file {FileName}.")]
    private partial void LogParseFailed(Exception exception, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Steam scan: {LibraryCount} libraries, {AppCount} installed apps.")]
    private partial void LogScanned(int libraryCount, int appCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read Steam library {LibraryPath}; skipping it.")]
    private partial void LogLibraryUnreadable(Exception exception, string libraryPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Steam scan failed; trying again in a minute.")]
    private partial void LogScanFailed(Exception exception);
}
