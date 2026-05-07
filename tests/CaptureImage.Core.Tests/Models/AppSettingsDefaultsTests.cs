using CaptureImage.Core.Models;
using FluentAssertions;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

/// <summary>
/// Regression guards for the v1.5 settings additions. Defaults are part of the
/// schema contract — when v1.4 settings.json files load on a v1.5 build, the new
/// fields must come back with these values so users don't get surprise behaviour.
/// </summary>
public class AppSettingsDefaultsTests
{
    [Fact]
    public void CaptureSettings_NewFieldsHaveSafeDefaults()
    {
        var s = new CaptureSettings();
        s.CountdownSeconds.Should().Be(0, "v1.5 must default to no countdown so existing users see no behaviour change");
        s.ClipboardMode.Should().Be("None", "default clipboard mode must keep file-only behaviour");
        s.Mode.Should().Be("Window", "default capture mode must remain the v1.4 window-capture flow");
    }

    [Fact]
    public void UiSettings_NewFieldsHaveSafeDefaults()
    {
        var s = new UiSettings();
        s.OpenFolderAfterSave.Should().BeFalse("opening Explorer on every capture would be intrusive by default");
        s.AutoPinAfterCapture.Should().BeFalse("auto-pin must be opt-in — surprise floating windows would annoy gamers");
    }

    [Fact]
    public void AppSettings_DefaultsAreInternallyConsistent()
    {
        var settings = new AppSettings();
        settings.Capture.Should().NotBeNull();
        settings.UI.Should().NotBeNull();
        // The full default record round-trips through `with` cleanly — guards against
        // the STJ source-gen init-default gotcha noted in the v1.2 cycle ledger.
        var roundTripped = settings with { Theme = "Dark" };
        roundTripped.Capture.Mode.Should().Be("Window");
        roundTripped.UI.AutoPinAfterCapture.Should().BeFalse();
    }
}
