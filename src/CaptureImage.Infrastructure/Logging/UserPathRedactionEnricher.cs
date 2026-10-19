// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using Serilog.Core;
using Serilog.Events;

namespace CaptureImage.Infrastructure.Logging;

/// <summary>
/// Replaces the user's profile folder (<c>C:\Users\name</c>) with <c>%USERPROFILE%</c> in
/// string properties, so log files attached to bug reports don't reveal the account name.
/// </summary>
public sealed class UserPathRedactionEnricher : ILogEventEnricher
{
    private const string Placeholder = "%USERPROFILE%";
    private readonly string _profile;

    public UserPathRedactionEnricher()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    {
    }

    public UserPathRedactionEnricher(string profilePath)
    {
        _profile = profilePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        if (_profile.Length < 3)
        {
            return;
        }

        foreach (var (name, value) in logEvent.Properties.ToArray())
        {
            if (value is ScalarValue { Value: string text } && text.Contains(_profile, StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Redact(text))));
            }
        }
    }

    public string Redact(string text) => text.Replace(_profile, Placeholder, StringComparison.OrdinalIgnoreCase);
}
