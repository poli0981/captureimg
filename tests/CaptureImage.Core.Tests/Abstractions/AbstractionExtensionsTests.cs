// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using NSubstitute;
using Xunit;

namespace CaptureImage.Core.Tests.Abstractions;

public class AbstractionExtensionsTests
{
    private static ILocalizationService Localization(params string[] supportedCultures)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.SupportedCultures.Returns([.. supportedCultures.Select(CultureInfo.GetCultureInfo)]);
        return localization;
    }

    // ---- LocalizationServiceExtensions.Format ------------------------------------------

    [Fact]
    public void Format_UsesTheTranslatedTemplateAndTheUiCulture()
    {
        var localization = Localization("en-US", "de-DE");
        localization.CurrentCulture.Returns(CultureInfo.GetCultureInfo("de-DE"));
        localization["Toast_Size"].Returns("{0:N1} MB in {1}");

        localization.Format("Toast_Size", 1234.5, "Pictures").Should().Be("1.234,5 MB in Pictures");
    }

    [Fact]
    public void Format_NullService_Throws()
    {
        var act = () => LocalizationServiceExtensions.Format(null!, "key");

        act.Should().Throw<ArgumentNullException>();
    }

    // ---- LocalizationServiceExtensions.MatchSupportedCulture ---------------------------

    [Theory]
    [InlineData("vi-VN", "vi-VN")] // exact
    [InlineData("vi", "vi-VN")]    // same language
    [InlineData("zh-CN", "zh-Hans")] // parent culture
    [InlineData("de-AT", "de-DE")] // same language, other region
    [InlineData("en-GB", "en-US")]
    [InlineData("fr-FR", "en-US")] // not shipped: the first supported culture
    public void MatchSupportedCulture_PicksTheClosestShippedCulture(string requested, string expected)
    {
        var localization = Localization("en-US", "vi-VN", "zh-Hans", "de-DE");

        localization.MatchSupportedCulture(CultureInfo.GetCultureInfo(requested)).Name.Should().Be(expected);
    }

    [Fact]
    public void MatchSupportedCulture_NullOrInvariant_FallsBackToTheFirstSupportedCulture()
    {
        var localization = Localization("vi-VN", "en-US");

        localization.MatchSupportedCulture(null).Name.Should().Be("vi-VN");
        localization.MatchSupportedCulture(CultureInfo.InvariantCulture).Name.Should().Be("vi-VN");
    }

    [Fact]
    public void MatchSupportedCulture_WithNothingShipped_FallsBackToEnglish()
    {
        Localization().MatchSupportedCulture(CultureInfo.GetCultureInfo("ja-JP")).Name.Should().Be("en-US");
    }

    // ---- ToastServiceExtensions -----------------------------------------------------------

    [Fact]
    public void ToastShortcuts_ShowTheMatchingKindWithTheDefaultDuration()
    {
        var toasts = Substitute.For<IToastService>();

        toasts.ShowSuccess("s-title", "s-message");
        toasts.ShowError("e-title", "e-message");
        toasts.ShowInfo("i-title", "i-message");
        toasts.ShowWarning("w-title", "w-message");

        Received.InOrder(() =>
        {
            toasts.Show(ToastKind.Success, "s-title", "s-message", null);
            toasts.Show(ToastKind.Error, "e-title", "e-message", null);
            toasts.Show(ToastKind.Info, "i-title", "i-message", null);
            toasts.Show(ToastKind.Warning, "w-title", "w-message", null);
        });
    }
}
