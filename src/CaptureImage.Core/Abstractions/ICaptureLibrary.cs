// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Library;

namespace CaptureImage.Core.Abstractions;

/// <summary>Index of saved captures backing the Library page.</summary>
public interface ICaptureLibrary
{
    /// <summary>The index changed (capture added or removed). Raised on any thread.</summary>
    event EventHandler? Changed;

    /// <summary>All known captures, newest first; files deleted outside the app are dropped.</summary>
    Task<IReadOnlyList<CaptureRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(CaptureRecord record, CancellationToken cancellationToken = default);

    /// <summary>Move the image (and its HDR sidecar) to the Recycle Bin and forget it.</summary>
    Task<bool> DeleteAsync(CaptureRecord record, CancellationToken cancellationToken = default);

    /// <summary>Path of a cached JPEG thumbnail (at most <paramref name="maxSize"/> px on the long edge).</summary>
    Task<string?> GetThumbnailAsync(CaptureRecord record, int maxSize, CancellationToken cancellationToken = default);
}
