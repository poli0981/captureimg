using CaptureImage.UI.Converters;
using FluentAssertions;
using Microsoft.UI.Xaml;
using Xunit;

namespace CaptureImage.UI.Tests.Converters;

public class NotNullToVisibilityConverterTests
{
    private static readonly NotNullToVisibilityConverter Converter = NotNullToVisibilityConverter.Instance;

    [Fact]
    public void Convert_Null_ReturnsCollapsed()
    {
        Converter.Convert(null, typeof(Visibility), null!, "en-US").Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_String_ReturnsVisible()
    {
        Converter.Convert("hello", typeof(Visibility), null!, "en-US").Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_EmptyString_ReturnsVisible()
    {
        // Empty-but-non-null is still "not null" semantically — the converter only
        // distinguishes against the null reference, not against truthy/falsey values.
        Converter.Convert(string.Empty, typeof(Visibility), null!, "en-US").Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_Object_ReturnsVisible()
    {
        Converter.Convert(new object(), typeof(Visibility), null!, "en-US").Should().Be(Visibility.Visible);
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        var act = () => Converter.ConvertBack(Visibility.Visible, typeof(object), null!, "en-US");
        act.Should().Throw<NotSupportedException>();
    }
}
