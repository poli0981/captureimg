using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace CaptureImage.UI.Converters;

/// <summary>
/// Converts raw PNG <c>byte[]</c> to an Avalonia <see cref="Bitmap"/>. Used by the
/// Dashboard list so icon data can travel through ViewModels without a direct
/// Avalonia dependency. Returns <c>null</c> on any failure so XAML falls back to
/// the default image placeholder.
/// </summary>
public sealed class BytesToBitmapConverter : IValueConverter
{
    public static readonly BytesToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
