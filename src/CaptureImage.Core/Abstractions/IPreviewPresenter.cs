using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Shows the user a preview of a captured frame and awaits their accept/reject decision.
/// </summary>
/// <remarks>
/// Declared in Core so the orchestrator / view models don't reference Avalonia. The UI
/// project provides an implementation that opens a modal <c>PreviewWindow</c>.
/// </remarks>
public interface IPreviewPresenter
{
    /// <summary>
    /// Show the preview dialog for <paramref name="frame"/>. The returned task completes
    /// with <c>true</c> when the user accepts and <c>false</c> when they discard or close
    /// the dialog without saving.
    /// </summary>
    Task<bool> ShowAsync(
        CapturedFrame frame,
        GameTarget target,
        CancellationToken cancellationToken = default);
}
