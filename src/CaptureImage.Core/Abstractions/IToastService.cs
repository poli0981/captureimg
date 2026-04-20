using System.Collections.ObjectModel;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// In-app toast notifications. Anchors to a corner of the main window rather than the
/// OS Action Center — see plan §9.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Live collection the UI binds to. Always mutated on the UI thread (callers are
    /// responsible for marshalling).
    /// </summary>
    ObservableCollection<ToastItem> Visible { get; }

    /// <summary>Push a toast. Oldest toasts drop off when the visible cap is reached.</summary>
    void Show(ToastItem item);

    /// <summary>Dismiss a specific toast (user click, auto-timeout, or VM cleanup).</summary>
    void Dismiss(Guid id);

    /// <summary>Convenience helpers that build a <see cref="ToastItem"/> for you.</summary>
    void ShowSuccess(string title, string message);
    void ShowError(string title, string message);
    void ShowInfo(string title, string message);
    void ShowWarning(string title, string message);
}
