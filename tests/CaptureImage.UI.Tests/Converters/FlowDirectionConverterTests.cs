using CaptureImage.Core.Abstractions;
using CaptureImage.UI.Converters;
using FluentAssertions;
using Microsoft.UI.Xaml;
using Xunit;

namespace CaptureImage.UI.Tests.Converters;

public class FlowDirectionConverterTests
{
    private static readonly FlowDirectionConverter Converter = FlowDirectionConverter.Instance;

    [Fact]
    public void Convert_LeftToRight_ReturnsLeftToRight()
    {
        var result = Converter.Convert(TextFlowDirection.LeftToRight, typeof(FlowDirection), null!, "en-US");
        result.Should().Be(FlowDirection.LeftToRight);
    }

    [Fact]
    public void Convert_RightToLeft_ReturnsRightToLeft()
    {
        var result = Converter.Convert(TextFlowDirection.RightToLeft, typeof(FlowDirection), null!, "ar-SA");
        result.Should().Be(FlowDirection.RightToLeft);
    }

    [Fact]
    public void Convert_Null_DefaultsToLeftToRight()
    {
        // Defensive default — bindings can momentarily be null during construction.
        var result = Converter.Convert(null!, typeof(FlowDirection), null!, "en-US");
        result.Should().Be(FlowDirection.LeftToRight);
    }

    [Fact]
    public void Convert_UnexpectedType_DefaultsToLeftToRight()
    {
        var result = Converter.Convert("not an enum", typeof(FlowDirection), null!, "en-US");
        result.Should().Be(FlowDirection.LeftToRight);
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        var act = () => Converter.ConvertBack(FlowDirection.RightToLeft, typeof(TextFlowDirection), null!, "en-US");
        act.Should().Throw<NotSupportedException>();
    }
}
