// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Infrastructure.Tests.TestSupport;

/// <summary>
/// A real, initially empty directory under the system temp folder, deleted recursively on
/// dispose. Tests use real directories instead of a mocked file system so that atomic
/// replaces, sharing violations and Windows path rules are exercised for real.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    private string FullPath { get; } = Directory.CreateTempSubdirectory("CaptureImage.Tests.").FullName;

    /// <summary>A path inside the directory; nothing is created.</summary>
    public string Combine(params string[] parts) => Path.Combine([FullPath, .. parts]);

    /// <summary>Create (if needed) a sub-directory and return its full path.</summary>
    public string CreateDirectory(params string[] parts) => Directory.CreateDirectory(Combine(parts)).FullName;

    public void Dispose()
    {
        // Antivirus scanners and WIC can hold a handle for a moment after the code under test
        // closed it; retry briefly, then leave the folder to the OS rather than fail a test.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(FullPath))
                {
                    Directory.Delete(FullPath, recursive: true);
                }
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
