using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CaptureImage.UI.Converters;

/// <summary>
/// Maps <see cref="bool"/> -> <see cref="Visibility"/> with inversion. <c>true</c> = Collapsed.
/// Replaces Avalonia's <c>IsVisible="{Binding !X}"</c> negation pattern.
/// </summary>
public sealed class InvertedBoolToVisibilityConverter : IValueConverter
{
    public static readonly InvertedBoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility v && v == Visibility.Collapsed;
}
