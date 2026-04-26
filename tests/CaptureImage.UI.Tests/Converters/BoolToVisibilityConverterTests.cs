using CaptureImage.UI.Converters;
using FluentAssertions;
using Microsoft.UI.Xaml;
using Xunit;

namespace CaptureImage.UI.Tests.Converters;

public class BoolToVisibilityConverterTests
{
    private static readonly BoolToVisibilityConverter Converter = BoolToVisibilityConverter.Instance;

    [Fact]
    public void Convert_True_ReturnsVisible()
    {
        Converter.Convert(true, typeof(Visibility), null!, "en-US").Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_False_ReturnsCollapsed()
    {
        Converter.Convert(false, typeof(Visibility), null!, "en-US").Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_Null_ReturnsCollapsed()
    {
        Converter.Convert(null!, typeof(Visibility), null!, "en-US").Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void ConvertBack_VisibleRoundtrip()
    {
        Converter.ConvertBack(Visibility.Visible, typeof(bool), null!, "en-US").Should().Be(true);
        Converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, "en-US").Should().Be(false);
    }
}
