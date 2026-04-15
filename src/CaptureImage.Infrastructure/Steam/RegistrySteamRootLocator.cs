using System;
using System.IO.Abstractions;
using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace CaptureImage.Infrastructure.Steam;

/// <summary>
/// Locates the Steam install directory on Windows. Tries the standard registry keys first,
/// then falls back to the canonical Program Files paths.
/// </summary>
public sealed class RegistrySteamRootLocator : ISteamRootLocator
{
    private static readonly string[] RegistryKeyPaths =
    {
        @"SOFTWARE\WOW6432Node\Valve\Steam",
        @"SOFTWARE\Valve\Steam",
    };

    private static readonly string[] FallbackPaths =
    {
        @"C:\Program Files (x86)\Steam",
        @"C:\Program Files\Steam",
    };

    private readonly IFileSystem _fs;
    private readonly ILogger<RegistrySteamRootLocator> _logger;

    public RegistrySteamRootLocator(IFileSystem fs, ILogger<RegistrySteamRootLocator> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public string? TryFindSteamRoot()
    {
        foreach (var keyPath in RegistryKeyPaths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                var install = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(install) &&
                    _fs.Directory.Exists(install))
                {
                    _logger.LogInformation("Steam root resolved from registry {Key}: {Path}", keyPath, install);
                    return install;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Reading Steam registry key {Key} failed.", keyPath);
            }
        }

        foreach (var path in FallbackPaths)
        {
            if (_fs.Directory.Exists(path))
            {
                _logger.LogInformation("Steam root resolved from fallback path: {Path}", path);
                return path;
            }
        }

        _logger.LogInformation("Steam install directory not found.");
        return null;
    }
}
