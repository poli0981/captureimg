using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Steam;

/// <summary>
/// Scans Steam library folders by reading <c>steamapps\libraryfolders.vdf</c> and every
/// <c>steamapps\appmanifest_*.acf</c> under the discovered libraries. Results are cached
/// for the lifetime of the scanner; call <see cref="Refresh"/> to re-scan after user installs
/// or removes a library.
/// </summary>
public sealed class SteamLibraryScanner : ISteamDetector
{
    private readonly IFileSystem _fs;
    private readonly ISteamRootLocator _rootLocator;
    private readonly ILogger<SteamLibraryScanner> _logger;
    private readonly object _gate = new();
    private IReadOnlyList<SteamLibrary>? _libraries;

    public SteamLibraryScanner(
        IFileSystem fs,
        ISteamRootLocator rootLocator,
        ILogger<SteamLibraryScanner> logger)
    {
        _fs = fs;
        _rootLocator = rootLocator;
        _logger = logger;
    }

    public IReadOnlyList<SteamLibrary> Libraries
    {
        get
        {
            EnsureLoaded();
            return _libraries!;
        }
    }

    public void Refresh()
    {
        lock (_gate)
        {
            _libraries = null;
        }
    }

    public SteamAppInfo? TryGetAppInfo(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;
        EnsureLoaded();

        var normalized = NormalizePath(executablePath);

        foreach (var lib in _libraries!)
        {
            var commonRoot = _fs.Path.Combine(lib.Path, "steamapps", "common");
            var commonNormalized = NormalizePath(commonRoot);
            var prefix = commonNormalized.EndsWith(_fs.Path.DirectorySeparatorChar)
                ? commonNormalized
                : commonNormalized + _fs.Path.DirectorySeparatorChar;

            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = normalized[prefix.Length..];
            var separators = new[] { _fs.Path.DirectorySeparatorChar, _fs.Path.AltDirectorySeparatorChar };
            var installDir = relative.Split(separators, 2, StringSplitOptions.RemoveEmptyEntries)[0];

            foreach (var app in lib.Apps.Values)
            {
                if (app.InstallDir.Equals(installDir, StringComparison.OrdinalIgnoreCase))
                {
                    return app;
                }
            }
            return null;
        }

        return null;
    }

    private void EnsureLoaded()
    {
        if (_libraries is not null) return;

        lock (_gate)
        {
            if (_libraries is not null) return;
            _libraries = LoadLibraries();
        }
    }

    private IReadOnlyList<SteamLibrary> LoadLibraries()
    {
        var steamRoot = _rootLocator.TryFindSteamRoot();
        if (steamRoot is null)
        {
            _logger.LogDebug("Steam not detected on this machine; scanner will return an empty library list.");
            return Array.Empty<SteamLibrary>();
        }

        var libraryFoldersVdf = _fs.Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!_fs.File.Exists(libraryFoldersVdf))
        {
            _logger.LogWarning("libraryfolders.vdf not found at {Path}.", libraryFoldersVdf);
            return Array.Empty<SteamLibrary>();
        }

        IReadOnlyList<string> libraryPaths;
        try
        {
            var root = VdfParser.Parse(_fs.File.ReadAllText(libraryFoldersVdf));
            libraryPaths = ExtractLibraryPaths(root);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse {Path}.", libraryFoldersVdf);
            return Array.Empty<SteamLibrary>();
        }

        var libraries = new List<SteamLibrary>(libraryPaths.Count);
        foreach (var libPath in libraryPaths)
        {
            var apps = LoadAppsInLibrary(libPath);
            libraries.Add(new SteamLibrary(libPath, apps));
        }
        _logger.LogInformation(
            "Steam scan complete: {LibraryCount} librar(ies), {AppCount} total app manifest(s).",
            libraries.Count,
            TotalAppCount(libraries));
        return libraries;
    }

    private static int TotalAppCount(IReadOnlyList<SteamLibrary> libraries)
    {
        var total = 0;
        foreach (var lib in libraries) total += lib.Apps.Count;
        return total;
    }

    /// <summary>
    /// Walk the parsed <c>libraryfolders.vdf</c> and collect every <c>path</c> leaf
    /// that lives one level under a numeric branch. The schema is:
    /// <code>
    /// "libraryfolders" {
    ///     "0" { "path" "C:\\Program Files (x86)\\Steam" ... }
    ///     "1" { "path" "D:\\SteamLibrary"                 ... }
    /// }
    /// </code>
    /// </summary>
    private static IReadOnlyList<string> ExtractLibraryPaths(VdfNode root)
    {
        var paths = new List<string>();

        // `root` may be the outer "libraryfolders" branch or our synthetic wrapper.
        var container = root.Key.Equals("libraryfolders", StringComparison.OrdinalIgnoreCase)
            ? root
            : root["libraryfolders"] ?? root;

        foreach (var child in container.BranchChildren())
        {
            var path = child.ValueOf("path");
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }
        return paths;
    }

    /// <summary>
    /// Read every <c>appmanifest_*.acf</c> under <c>{libraryPath}\steamapps</c> and
    /// extract <c>appid</c> + <c>name</c> + <c>installdir</c>. Missing or malformed
    /// manifests are skipped with a warning.
    /// </summary>
    private IReadOnlyDictionary<uint, SteamAppInfo> LoadAppsInLibrary(string libraryPath)
    {
        var steamappsDir = _fs.Path.Combine(libraryPath, "steamapps");
        if (!_fs.Directory.Exists(steamappsDir))
        {
            return new Dictionary<uint, SteamAppInfo>();
        }

        var apps = new Dictionary<uint, SteamAppInfo>();
        foreach (var manifestPath in _fs.Directory.EnumerateFiles(steamappsDir, "appmanifest_*.acf"))
        {
            try
            {
                var text = _fs.File.ReadAllText(manifestPath);
                var root = VdfParser.Parse(text);
                var state = root.Key.Equals("AppState", StringComparison.OrdinalIgnoreCase)
                    ? root
                    : root["AppState"];
                if (state is null) continue;

                var appIdStr = state.ValueOf("appid");
                var name = state.ValueOf("name") ?? string.Empty;
                var installDir = state.ValueOf("installdir") ?? string.Empty;

                if (!uint.TryParse(appIdStr, out var appId) || string.IsNullOrWhiteSpace(installDir))
                {
                    continue;
                }

                apps[appId] = new SteamAppInfo(appId, name, installDir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Steam app manifest {Path}.", manifestPath);
            }
        }
        return apps;
    }

    /// <summary>
    /// Canonicalize a path for prefix comparison: collapse slashes, remove trailing
    /// separator, keep the drive letter case-insensitive. Does NOT resolve symlinks.
    /// </summary>
    private string NormalizePath(string path)
    {
        var fullPath = _fs.Path.GetFullPath(path);
        return fullPath.TrimEnd(_fs.Path.DirectorySeparatorChar, _fs.Path.AltDirectorySeparatorChar);
    }
}
