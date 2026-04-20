using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CaptureImage.Core.Models;

namespace CaptureImage.UI.Converters;

/// <summary>
/// Maps a <see cref="ToastKind"/> to the accent color the toast card should use.
/// </summary>
public sealed class ToastKindToBrushConverter : IValueConverter
{
    public static readonly ToastKindToBrushConverter Instance = new();

    private static readonly IBrush InfoBrush    = new SolidColorBrush(Color.Parse("#3B82F6"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#22C55E"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush ErrorBrush   = new SolidColorBrush(Color.Parse("#EF4444"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            ToastKind.Success => SuccessBrush,
            ToastKind.Warning => WarningBrush,
            ToastKind.Error   => ErrorBrush,
            _                 => InfoBrush,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
