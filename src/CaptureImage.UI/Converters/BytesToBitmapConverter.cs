using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CaptureImage.UI.Converters;

/// <summary>
/// Converts a raw PNG <c>byte[]</c> to a <see cref="BitmapImage"/>. Used by the Dashboard
/// list so icon data flows through the portable VMs without taking a UI-framework
/// dependency. Returns <c>null</c> on any failure so XAML falls back to its default
/// placeholder.
/// </summary>
public sealed class BytesToBitmapConverter : IValueConverter
{
    public static readonly BytesToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is not byte[] { Length: > 0 } bytes)
        {
            return null;
        }

        try
        {
            // BitmapImage.SetSource is sync in WinUI 3 — it just hands the stream off to the
            // decoder, which loads asynchronously on a background thread. The Image control
            // shows nothing until decode completes, then refreshes in place.
            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.SetSource(ms.AsRandomAccessStream());
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
