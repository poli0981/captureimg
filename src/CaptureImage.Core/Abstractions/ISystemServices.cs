// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Abstractions;

/// <summary>Shell integration (open files and folders, links, Recycle Bin).</summary>
public interface IShellService
{
    /// <summary>Open a file with its default program, or a folder in Explorer.</summary>
    bool Open(string path);

    /// <summary>Open Explorer with <paramref name="filePath"/> selected.</summary>
    bool RevealInExplorer(string filePath);

    bool OpenUrl(Uri url);

    /// <summary>Move a file to the Recycle Bin (never a permanent delete).</summary>
    bool MoveToRecycleBin(string path);
}

public interface ISoundService
{
    void PlayCaptureSound();
}

/// <summary>
/// Windows 11 "efficiency mode" (EcoQoS) while the app idles in the tray, lifted while a
/// capture runs so encoding isn't throttled.
/// </summary>
public interface IPerformanceModeService
{
    void SetBackground(bool isBackground);

    /// <summary>Temporarily run at normal priority; dispose to return to the previous mode.</summary>
    IDisposable BeginForegroundWork();
}

/// <summary>"Start with Windows" (per-user Run key).</summary>
public interface IAutostartService
{
    bool IsEnabled { get; }

    /// <summary>Returns false if the registry could not be updated.</summary>
    bool SetEnabled(bool enabled);
}

/// <summary>Bundles logs, settings and system information for a bug report.</summary>
public interface IDiagnosticsExporter
{
    /// <summary>Write a zip to <paramref name="destinationPath"/> and return its path.</summary>
    Task<string> ExportAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>A default file name such as <c>CaptureImage-diagnostics-20261019-071500.zip</c>.</summary>
    string SuggestFileName();
}

/// <summary>Application shutdown, so view models can quit without referencing the UI framework.</summary>
public interface IAppLifetime
{
    void Shutdown();
}
