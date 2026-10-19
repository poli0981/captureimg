// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.IO;

namespace CaptureImage.App;

/// <summary>
/// Command-line switches: <c>--minimized</c> (start in the tray; "Start with Windows" uses it),
/// <c>--smoke-test</c> (open every page and exit 0 — CI), <c>--instance-id &lt;id&gt;</c>
/// (separate single-instance scope, so tests never talk to a copy the user is running) and
/// <c>--data-dir &lt;path&gt;</c> (read by <c>AppPaths</c>). <c>--screenshots &lt;dir&gt;</c> runs the
/// smoke test and saves a PNG of every page (docs, visual review in CI).
/// </summary>
internal sealed record StartupOptions(bool StartMinimized, bool SmokeTest, string InstanceId, string? ScreenshotDirectory)
{
    private const int MaxInstanceIdLength = 32;

    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var minimized = args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));
        var smokeTest = args.Any(a => string.Equals(a, "--smoke-test", StringComparison.OrdinalIgnoreCase));
        var instanceId = "default";
        string? screenshots = null;
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], "--instance-id", StringComparison.OrdinalIgnoreCase))
            {
                instanceId = Sanitize(args[i + 1]);
            }
            else if (string.Equals(args[i], "--screenshots", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                screenshots = Path.GetFullPath(args[i + 1]);
            }
        }

        return new StartupOptions(minimized, smokeTest || screenshots is not null, instanceId, screenshots);
    }

    /// <summary>Letters, digits, '-' and '_' only — the id becomes part of a mutex and pipe name.</summary>
    private static string Sanitize(string value)
    {
        var clean = new string([.. value.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').Take(MaxInstanceIdLength)]);
        return clean.Length == 0 ? "default" : clean;
    }
}
