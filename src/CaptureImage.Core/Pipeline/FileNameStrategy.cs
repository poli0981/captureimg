using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Pipeline;

/// <summary>
/// Builds the output file name for a capture, expanding tokens in a template and resolving
/// collisions with a counter suffix.
/// </summary>
/// <remarks>
/// <para>Supported tokens (case-sensitive):</para>
/// <list type="bullet">
///   <item><c>{Game}</c> — display name of the target (<see cref="GameTarget.DisplayName"/>), sanitized.</item>
///   <item><c>{Process}</c> — short process name, sanitized.</item>
///   <item><c>{yyyy}</c>, <c>{MM}</c>, <c>{dd}</c>, <c>{HH}</c>, <c>{mm}</c>, <c>{ss}</c> — zero-padded date/time parts.</item>
///   <item><c>{counter}</c> — replaced with "_1", "_2", … only if the file already exists. If present, the token is the anchor for the disambiguating suffix; if absent, the suffix is appended at the end.</item>
/// </list>
/// <para>
/// The strategy is injected with a <see cref="Func{T, TResult}"/> for existence checks so unit
/// tests can drive collision logic with an in-memory fake.
/// </para>
/// </remarks>
public sealed class FileNameStrategy
{
    private readonly Func<string, bool> _fileExists;

    /// <summary>
    /// Default template used when settings haven't been loaded yet:
    /// <c>{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}</c>.
    /// </summary>
    public const string DefaultTemplate = "{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}";

    public FileNameStrategy(Func<string, bool> fileExists)
    {
        _fileExists = fileExists;
    }

    /// <summary>
    /// Build a full path under <paramref name="directory"/> that does not collide with an
    /// existing file. Template tokens are expanded using <paramref name="target"/> and
    /// <paramref name="captureTime"/>.
    /// </summary>
    public string BuildFilePath(
        string directory,
        string template,
        GameTarget target,
        DateTimeOffset captureTime,
        ImageFormat format)
    {
        var expanded = ExpandTokens(template, target, captureTime);
        var sanitized = Sanitize(expanded);
        var extension = format.Extension();

        // Fast path: no collision.
        var candidate = Path.Combine(directory, sanitized + "." + extension);
        if (!_fileExists(candidate))
        {
            return candidate;
        }

        // Collision: increment a counter until we find a free slot. Cap at 10_000 to avoid
        // runaway loops if the directory is completely full of matching files.
        for (var counter = 1; counter <= 10_000; counter++)
        {
            var withCounter = sanitized + "_" + counter;
            candidate = Path.Combine(directory, withCounter + "." + extension);
            if (!_fileExists(candidate))
            {
                return candidate;
            }
        }

        // Fall back to a timestamp-based suffix. Extremely unlikely, but correct.
        var fallback = sanitized + "_" + captureTime.ToUnixTimeMilliseconds();
        return Path.Combine(directory, fallback + "." + extension);
    }

    /// <summary>
    /// Expand all tokens in <paramref name="template"/>. Unknown tokens are left verbatim.
    /// Visible for testing.
    /// </summary>
    internal static string ExpandTokens(string template, GameTarget target, DateTimeOffset captureTime)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{Game}"]    = target.DisplayName,
            ["{Process}"] = target.ProcessName,
            ["{yyyy}"]    = captureTime.Year.ToString("D4"),
            ["{MM}"]      = captureTime.Month.ToString("D2"),
            ["{dd}"]      = captureTime.Day.ToString("D2"),
            ["{HH}"]      = captureTime.Hour.ToString("D2"),
            ["{mm}"]      = captureTime.Minute.ToString("D2"),
            ["{ss}"]      = captureTime.Second.ToString("D2"),
            ["{counter}"] = string.Empty, // stripped here; collision logic handles it
        };

        var sb = new StringBuilder(template.Length + 32);
        var i = 0;
        while (i < template.Length)
        {
            if (template[i] == '{')
            {
                var end = template.IndexOf('}', i + 1);
                if (end > 0)
                {
                    var token = template.Substring(i, end - i + 1);
                    if (tokens.TryGetValue(token, out var replacement))
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

    /// <summary>
    /// Strip characters that are illegal on NTFS/FAT32 and collapse runs of whitespace.
    /// </summary>
    internal static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "capture";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(raw.Length);
        var lastWasSeparator = false;
        foreach (var c in raw)
        {
            // Whitespace wins over the illegal check so that tabs, newlines, etc. collapse
            // into a single space rather than becoming '_'.
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSeparator)
                {
                    sb.Append(' ');
                    lastWasSeparator = true;
                }
                continue;
            }
            if (Array.IndexOf(invalid, c) >= 0 || c < 0x20)
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

        var trimmed = sb.ToString().Trim(' ', '_', '.');
        return trimmed.Length == 0 ? "capture" : trimmed;
    }
}
