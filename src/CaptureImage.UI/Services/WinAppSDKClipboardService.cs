using System.Runtime.InteropServices.WindowsRuntime;
using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace CaptureImage.UI.Services;

/// <summary>
/// <see cref="IClipboardService"/> backed by the WinRT clipboard. Wraps
/// <c>Windows.ApplicationModel.DataTransfer.Clipboard.SetContent</c> with a
/// PNG <c>InMemoryRandomAccessStream</c> so the receiving app pastes a real bitmap
/// (Paint, Word, etc.) rather than a file reference.
/// </summary>
public sealed class WinAppSDKClipboardService : IClipboardService
{
    private readonly ILogger<WinAppSDKClipboardService> _logger;

    public WinAppSDKClipboardService(ILogger<WinAppSDKClipboardService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CopyImageAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        if (pngBytes is null || pngBytes.Length == 0) return false;

        try
        {
            // Need an InMemoryRandomAccessStream the DataPackage can hold a reference to —
            // the DataTransferManager copies bytes lazily, so a MemoryStream wrapper would
            // get GC'd before the receiving app pastes.
            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(pngBytes.AsBuffer()).AsTask(cancellationToken).ConfigureAwait(false);
            stream.Seek(0);

            var package = new DataPackage();
            package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));

            Clipboard.SetContent(package);
            // Flush makes the data survive process exit — important for capture-and-quit
            // scripted workflows. No exception here means SetContent accepted the package.
            Clipboard.Flush();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy captured image to clipboard.");
            return false;
        }
    }

    public bool CopyText(string text)
    {
        if (text is null) return false;
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy text to clipboard.");
            return false;
        }
    }
}
