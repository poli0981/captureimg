using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CaptureImage.UI.Converters;

/// <summary>
/// Maps <see cref="bool"/> -> <see cref="Visibility"/>. <c>true</c> = Visible.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility v && v == Visibility.Visible;
}
