using System;

namespace CaptureImage.Core.Models;

/// <summary>
/// Severity of a toast — drives icon + color in the UI.
/// </summary>
public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// A single toast shown in the in-app toast host (bottom-right overlay, bottom-left under RTL).
/// </summary>
/// <param name="Title">Short headline, e.g. <c>"Capture saved"</c>.</param>
/// <param name="Message">Supporting text — path, reason, counts.</param>
/// <param name="Kind">Severity bucket.</param>
/// <param name="Duration">
/// How long to display before auto-dismissing. <c>TimeSpan.Zero</c> means "stay until clicked".
/// </param>
public sealed record ToastItem(
    string Title,
    string Message,
    ToastKind Kind = ToastKind.Info,
    TimeSpan? Duration = null)
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);

    /// <summary>Effective duration — fills in <see cref="DefaultDuration"/> when not specified.</summary>
    public TimeSpan EffectiveDuration => Duration ?? DefaultDuration;

    /// <summary>Unique identifier used by the host to track removal animations.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
