using System.Runtime.Versioning;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Processes;

/// <summary>
/// Default <see cref="IProcessDetector"/>. Composes <see cref="WindowEnumerator"/>,
/// <see cref="IconExtractor"/> and <see cref="ISteamDetector"/> into a single call.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GameDetector : IProcessDetector
{
    private readonly WindowEnumerator _windowEnumerator;
    private readonly IconExtractor _iconExtractor;
    private readonly ISteamDetector _steamDetector;
    private readonly ILogger<GameDetector> _logger;

    public GameDetector(
        WindowEnumerator windowEnumerator,
        IconExtractor iconExtractor,
        ISteamDetector steamDetector,
        ILogger<GameDetector> logger)
    {
        _windowEnumerator = windowEnumerator;
        _iconExtractor = iconExtractor;
        _steamDetector = steamDetector;
        _logger = logger;
    }

    public Task<IReadOnlyList<GameTarget>> EnumerateTargetsAsync(CancellationToken cancellationToken = default)
    {
        // EnumWindows is synchronous and fast (<10ms typically), so we run on the calling
        // thread. Wrap in Task.FromResult to honor the async contract without ceremony.
        var windows = _windowEnumerator.Enumerate();

        // Group by PID — a single process can own multiple top-level windows; we keep
        // the one with the longest title as the representative. This is a heuristic but
        // good enough for the Dashboard — capture itself always uses a specific HWND.
        var byPid = new Dictionary<uint, WindowInfo>();
        foreach (var w in windows)
        {
            if (!byPid.TryGetValue(w.ProcessId, out var existing) ||
                w.WindowTitle.Length > existing.WindowTitle.Length)
            {
                byPid[w.ProcessId] = w;
            }
        }

        var results = new List<GameTarget>(byPid.Count);
        foreach (var (pid, window) in byPid)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exePath = _windowEnumerator.TryGetExecutablePath(pid);
            var processName = _windowEnumerator.TryGetProcessName(pid);

            var iconBytes = string.IsNullOrEmpty(exePath)
                ? null
                : _iconExtractor.TryGetIconPngBytes(exePath);

            var steamInfo = string.IsNullOrEmpty(exePath)
                ? null
                : _steamDetector.TryGetAppInfo(exePath);

            results.Add(new GameTarget(
                ProcessId: pid,
                WindowHandle: window.Hwnd,
                ProcessName: processName,
                WindowTitle: window.WindowTitle,
                ExecutablePath: exePath,
                IconBytes: iconBytes,
                SteamInfo: steamInfo));
        }

        // Stable sort for deterministic UI.
        results.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName));

        _logger.LogDebug("Enumerated {Count} capture target(s).", results.Count);
        return Task.FromResult<IReadOnlyList<GameTarget>>(results);
    }
}
