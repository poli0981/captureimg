using CaptureImage.Core.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CaptureImage.UI.Converters;

/// <summary>
/// Maps the portable <see cref="TextFlowDirection"/> enum (defined in Core so view models can
/// talk about direction without referencing WinUI 3) to <see cref="FlowDirection"/>.
/// </summary>
public sealed class FlowDirectionConverter : IValueConverter
{
    public static readonly FlowDirectionConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is TextFlowDirection.RightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
