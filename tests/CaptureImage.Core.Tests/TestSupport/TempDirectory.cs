// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Tests.TestSupport;

/// <summary>A fresh directory under the system temp folder, deleted again on dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        FullPath = Directory.CreateTempSubdirectory("captureimage-core-tests-").FullName;
    }

    public string FullPath { get; }

    public string Combine(params string[] parts) => Path.Combine([FullPath, .. parts]);

    /// <summary>Every file below the directory as a path relative to it, sorted.</summary>
    public IReadOnlyList<string> RelativeFiles() =>
        [.. Directory.EnumerateFiles(FullPath, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(FullPath, file))
            .Order(StringComparer.OrdinalIgnoreCase)];

    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a file a failing test still holds open must not fail the run.
        }
    }
}
