// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>Persisted user settings (<c>settings.json</c> in the data directory).</summary>
public interface ISettingsStore
{
    AppSettings Current { get; }

    /// <summary>Raised after <see cref="Current"/> changes, on the thread that made the change.</summary>
    event EventHandler? Changed;

    string SettingsFilePath { get; }

    /// <summary>Load (migrating older schemas and the v1 settings location) — call once at startup.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Apply a change and schedule a debounced write.</summary>
    void Update(Func<AppSettings, AppSettings> mutator);

    /// <summary>Write pending changes now.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    Task ExportAsync(string exportPath, CancellationToken cancellationToken = default);

    /// <summary>Validate and apply a settings file exported earlier.</summary>
    /// <exception cref="InvalidDataException">The file is not a valid settings document.</exception>
    Task ImportAsync(string importPath, CancellationToken cancellationToken = default);

    /// <summary>Back up the current file and restore defaults.</summary>
    Task ResetAsync(CancellationToken cancellationToken = default);
}
