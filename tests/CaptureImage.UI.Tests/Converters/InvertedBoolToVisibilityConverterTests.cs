using CaptureImage.UI.Converters;
using FluentAssertions;
using Microsoft.UI.Xaml;
using Xunit;

namespace CaptureImage.UI.Tests.Converters;

public class InvertedBoolToVisibilityConverterTests
{
    private static readonly InvertedBoolToVisibilityConverter Converter = InvertedBoolToVisibilityConverter.Instance;

    [Fact]
    public void Convert_True_ReturnsCollapsed()
    {
        Converter.Convert(true, typeof(Visibility), null!, "en-US").Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_False_ReturnsVisible()
    {
        Converter.Convert(false, typeof(Visibility), null!, "en-US").Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_Null_ReturnsVisible()
    {
        // Inverted: null is "not true" so it maps to Visible (the default-show case).
        Converter.Convert(null!, typeof(Visibility), null!, "en-US").Should().Be(Visibility.Visible);
    }

    [Fact]
    public void ConvertBack_CollapsedRoundtripToTrue()
    {
        Converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, "en-US").Should().Be(true);
        Converter.ConvertBack(Visibility.Visible, typeof(bool), null!, "en-US").Should().Be(false);
    }
}
