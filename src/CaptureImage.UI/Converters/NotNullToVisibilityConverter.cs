using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CaptureImage.UI.Converters;

/// <summary>
/// Maps an object reference -> <see cref="Visibility"/>. Non-null = Visible.
/// Replaces Avalonia's <c>IsVisible="{Binding X, Converter={x:Static ObjectConverters.IsNotNull}}"</c>.
/// </summary>
public sealed class NotNullToVisibilityConverter : IValueConverter
{
    public static readonly NotNullToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object parameter, string language) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
