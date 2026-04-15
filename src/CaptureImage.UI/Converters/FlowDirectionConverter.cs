using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CaptureImage.Core.Abstractions;

namespace CaptureImage.UI.Converters;

/// <summary>
/// Maps the portable <see cref="TextFlowDirection"/> enum (defined in Core so view models can
/// talk about direction without referencing Avalonia) to Avalonia's
/// <see cref="Avalonia.Media.FlowDirection"/>.
/// </summary>
public sealed class FlowDirectionConverter : IValueConverter
{
    public static readonly FlowDirectionConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TextFlowDirection.RightToLeft
            ? Avalonia.Media.FlowDirection.RightToLeft
            : Avalonia.Media.FlowDirection.LeftToRight;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
