using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Captures a single frame of a target window. The engine handles D3D/WinRT ceremony and
/// returns raw BGRA pixels — encoding is the encoder's responsibility (see <see cref="IImageEncoder"/>).
/// </summary>
public interface ICaptureEngine
{
    /// <summary>
    /// Capture one frame of the target's window. Throws <see cref="Errors.CaptureException"/>
    /// with a stable <see cref="Errors.CaptureError"/> on any known-bad path.
    /// </summary>
    Task<CapturedFrame> CaptureAsync(GameTarget target, CancellationToken cancellationToken = default);
}
