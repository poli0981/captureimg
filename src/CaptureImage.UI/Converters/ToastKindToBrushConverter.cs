using CaptureImage.Core.Models;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CaptureImage.UI.Converters;

/// <summary>
/// Maps a <see cref="ToastKind"/> to the accent <see cref="SolidColorBrush"/> the toast
/// card should use for its bottom-edge stripe. Same colors as the v1.2 Avalonia version
/// (Tailwind sky-500 / green-500 / amber-500 / red-500).
/// </summary>
public sealed class ToastKindToBrushConverter : IValueConverter
{
    public static readonly ToastKindToBrushConverter Instance = new();

    private static readonly SolidColorBrush InfoBrush    = new(ColorHelper.FromArgb(0xFF, 0x3B, 0x82, 0xF6));
    private static readonly SolidColorBrush SuccessBrush = new(ColorHelper.FromArgb(0xFF, 0x22, 0xC5, 0x5E));
    private static readonly SolidColorBrush WarningBrush = new(ColorHelper.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
    private static readonly SolidColorBrush ErrorBrush   = new(ColorHelper.FromArgb(0xFF, 0xEF, 0x44, 0x44));

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            ToastKind.Success => SuccessBrush,
            ToastKind.Warning => WarningBrush,
            ToastKind.Error   => ErrorBrush,
            _                 => InfoBrush,
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

internal static class ColorHelper
{
    public static Color FromArgb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
}
