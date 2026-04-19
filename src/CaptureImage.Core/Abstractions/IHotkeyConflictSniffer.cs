using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Platform-neutral test: is this hotkey binding already owned by another process?
/// Implementations perform a best-effort check (e.g. Windows <c>RegisterHotKey</c> sniff)
/// and immediately release any claim they take — they must never leave the combination
/// bound after the call returns.
/// </summary>
public interface IHotkeyConflictSniffer
{
    /// <summary>
    /// <c>true</c> if the combination is already registered by another process or the
    /// shell; <c>false</c> if we successfully (and transiently) claimed it. Returning
    /// <c>false</c> does not prove the combination will work — the Windows shell has
    /// higher priority and consumes some combos before <c>RegisterHotKey</c> is consulted
    /// (see <see cref="Validation.ReservedHotkeys"/>).
    /// </summary>
    bool IsConflicted(HotkeyBinding binding);
}
