// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.Text;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Pipeline;

/// <summary>
/// Builds output file names from a template and keeps them safe for Windows.
/// </summary>
/// <remarks>
/// <para>Tokens (case-sensitive): <c>{Game}</c> (window title or process), <c>{Process}</c>,
/// <c>{Mode}</c> (Window / Region / Monitor), <c>{yyyy}</c> <c>{MM}</c> <c>{dd}</c> <c>{HH}</c>
/// <c>{mm}</c> <c>{ss}</c>, and <c>{counter}</c> — empty for the first file, <c>_1</c>,
/// <c>_2</c>… on a name collision. Without <c>{counter}</c> the suffix is appended.</para>
/// <para>Sanitizing: Windows-invalid characters, control and invisible formatting characters
/// (bidi overrides, zero-width joiners — they can disguise a file name) become <c>_</c>;
/// reserved device names (CON, NUL, COM1…) get a trailing <c>_</c>; the name is capped at
/// <see cref="MaxNameLength"/> characters.</para>
/// </remarks>
/// <param name="fileExists">Existence check (injected so tests can fake collisions).</param>
public sealed class FileNameStrategy(Func<string, bool> fileExists)
{
    /// <summary>Default template: <c>{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}</c>.</summary>
    public const string DefaultTemplate = "{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}";

    /// <summary>Maximum length of the generated name (without counter and extension).</summary>
    public const int MaxNameLength = 120;

    private const string CounterToken = "{counter}";
    private const int MaxCounter = 10_000;

    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³",
    };

    private readonly Func<string, bool> _fileExists = fileExists;

    /// <summary>
    /// A full path under <paramref name="directory"/> that does not collide with an existing
    /// file. The final rename still uses no-overwrite semantics, so a race that steals the name
    /// is detected by the caller and retried.
    /// </summary>
    public string BuildFilePath(
        string directory,
        string template,
        GameTarget? target,
        CaptureMode mode,
        DateTimeOffset captureTime,
        ImageFormat format)
    {
        var effectiveTemplate = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
        var extension = "." + format.Extension();
        var hasCounterToken = effectiveTemplate.Contains(CounterToken, StringComparison.Ordinal);

        for (var counter = 0; counter <= MaxCounter; counter++)
        {
            var suffix = counter == 0 ? string.Empty : "_" + counter.ToString(CultureInfo.InvariantCulture);
            var expanded = ExpandTokens(effectiveTemplate, target, mode, captureTime, hasCounterToken ? suffix : string.Empty);
            var name = Sanitize(expanded);
            if (!hasCounterToken)
            {
                name += suffix;
            }

            var candidate = Path.Combine(directory, name + extension);
            if (!_fileExists(candidate))
            {
                return candidate;
            }
        }

        // Directory packed with matching names — fall back to a millisecond timestamp.
        var fallback = Sanitize(ExpandTokens(effectiveTemplate, target, mode, captureTime, string.Empty))
            + "_" + captureTime.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        return Path.Combine(directory, fallback + extension);
    }

    /// <summary>Expand tokens; unknown tokens stay verbatim. Visible for testing.</summary>
    internal static string ExpandTokens(
        string template,
        GameTarget? target,
        CaptureMode mode,
        DateTimeOffset captureTime,
        string counterSuffix)
    {
        var game = target?.DisplayName ?? mode.ToString();
        var process = target?.ProcessName ?? mode.ToString();
        var sb = new StringBuilder(template.Length + 32);
        var i = 0;
        while (i < template.Length)
        {
            if (template[i] == '{')
            {
                var end = template.IndexOf('}', i + 1);
                if (end > 0)
                {
                    var replacement = template.Substring(i, end - i + 1) switch
                    {
                        "{Game}" => game,
                        "{Process}" => process,
                        "{Mode}" => mode.ToString(),
                        "{yyyy}" => captureTime.Year.ToString("D4", CultureInfo.InvariantCulture),
                        "{MM}" => captureTime.Month.ToString("D2", CultureInfo.InvariantCulture),
                        "{dd}" => captureTime.Day.ToString("D2", CultureInfo.InvariantCulture),
                        "{HH}" => captureTime.Hour.ToString("D2", CultureInfo.InvariantCulture),
                        "{mm}" => captureTime.Minute.ToString("D2", CultureInfo.InvariantCulture),
                        "{ss}" => captureTime.Second.ToString("D2", CultureInfo.InvariantCulture),
                        CounterToken => counterSuffix,
                        _ => null,
                    };
                    if (replacement is not null)
                    {
                        sb.Append(replacement);
                        i = end + 1;
                        continue;
                    }
                }
            }
            sb.Append(template[i]);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>Make <paramref name="raw"/> a safe Windows file name (without extension). Visible for testing.</summary>
    internal static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "capture";
        }

        var sb = new StringBuilder(raw.Length);
        var lastWasSeparator = false;
        foreach (var c in raw)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSeparator)
                {
                    sb.Append(' ');
                    lastWasSeparator = true;
                }
                continue;
            }

            if (c < 0x20 || c == 0x7F || Array.IndexOf(InvalidChars, c) >= 0
                || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.Control)
            {
                if (!lastWasSeparator)
                {
                    sb.Append('_');
                    lastWasSeparator = true;
                }
                continue;
            }

            sb.Append(c);
            lastWasSeparator = false;
        }

        var name = sb.ToString().Trim(' ', '_', '.');
        if (name.Length > MaxNameLength)
        {
            name = name[..MaxNameLength].TrimEnd(' ', '_', '.');
            if (name.Length > 0 && char.IsHighSurrogate(name[^1]))
            {
                name = name[..^1];
            }
        }

        if (name.Length == 0)
        {
            return "capture";
        }

        // "CON", "con.txt" and "NUL " are all device names to Windows.
        var stem = name.Split('.')[0].TrimEnd(' ');
        return ReservedNames.Contains(stem) ? name + "_" : name;
    }
}
