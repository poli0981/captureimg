using System.Globalization;
using CaptureImage.Core.Abstractions;
using CaptureImage.UI.Localization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.UI.Tests.Localization;

/// <summary>
/// Covers <see cref="ResxLocalizationService"/>. Snapshots thread culture in the ctor and
/// restores in <see cref="Dispose"/> so setting <c>CultureInfo.CurrentCulture</c> from one
/// test doesn't leak into the next one running on the same test runner thread.
/// </summary>
public class ResxLocalizationServiceTests : IDisposable
{
    private readonly CultureInfo _savedUICulture;
    private readonly CultureInfo _savedCulture;
    private readonly CultureInfo? _savedDefaultUICulture;
    private readonly CultureInfo? _savedDefaultCulture;

    public ResxLocalizationServiceTests()
    {
        _savedUICulture = CultureInfo.CurrentUICulture;
        _savedCulture = CultureInfo.CurrentCulture;
        _savedDefaultUICulture = CultureInfo.DefaultThreadCurrentUICulture;
        _savedDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _savedUICulture;
        CultureInfo.CurrentCulture = _savedCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _savedDefaultUICulture;
        CultureInfo.DefaultThreadCurrentCulture = _savedDefaultCulture;
    }

    [Fact]
    public void SetCulture_Vietnamese_UpdatesCurrentCultureAndThreadCulture()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);
        var vi = CultureInfo.GetCultureInfo("vi-VN");

        svc.SetCulture(vi);

        svc.CurrentCulture.Name.Should().Be("vi-VN");
        CultureInfo.CurrentUICulture.Name.Should().Be("vi-VN");
        CultureInfo.CurrentCulture.Name.Should().Be("vi-VN");
        CultureInfo.DefaultThreadCurrentUICulture?.Name.Should().Be("vi-VN");
        CultureInfo.DefaultThreadCurrentCulture?.Name.Should().Be("vi-VN");
    }

    [Fact]
    public void SetCulture_Arabic_FlipsFlowDirectionToRightToLeft()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);

        svc.SetCulture(CultureInfo.GetCultureInfo("ar-SA"));

        svc.CurrentFlowDirection.Should().Be(TextFlowDirection.RightToLeft);
    }

    [Fact]
    public void SetCulture_English_FlowDirectionLeftToRight()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);

        // Default is en-US; switch elsewhere first so SetCulture actually fires.
        svc.SetCulture(CultureInfo.GetCultureInfo("ar-SA"));
        svc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

        svc.CurrentFlowDirection.Should().Be(TextFlowDirection.LeftToRight);
    }

    [Fact]
    public void SetCulture_RaisesItemIndexer_CurrentCulture_AndFlowDirection_Once_Each()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);
        var names = new List<string?>();
        svc.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        svc.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));

        names.Should().Contain("Item[]");
        names.Should().Contain(nameof(ILocalizationService.CurrentCulture));
        names.Should().Contain(nameof(ILocalizationService.CurrentFlowDirection));
        names.Count(n => n == "Item[]").Should().Be(1);
    }

    [Fact]
    public void SetCulture_AlsoRaisesCultureChangedEvent()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);
        var cultureChanged = 0;
        svc.CultureChanged += (_, _) => cultureChanged++;

        svc.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));

        cultureChanged.Should().Be(1);
    }

    [Fact]
    public void SetCulture_SameAsCurrent_IsNoOp()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);
        // Constructor seeds with en-US.
        var events = 0;
        svc.PropertyChanged += (_, _) => events++;
        var cultureChanged = 0;
        svc.CultureChanged += (_, _) => cultureChanged++;

        svc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

        events.Should().Be(0);
        cultureChanged.Should().Be(0);
    }

    [Fact]
    public void SetCulture_Null_Throws()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);

        Action act = () => svc.SetCulture(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Indexer_MissingKey_ReturnsKeyInBrackets()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);

        var result = svc["NoSuchKey_DefinitelyMissing_1234"];

        result.Should().Be("[NoSuchKey_DefinitelyMissing_1234]");
    }

    [Fact]
    public void Indexer_EmptyKey_ReturnsEmptyString()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);

        svc[""].Should().BeEmpty();
    }

    [Fact]
    public void Indexer_KnownKey_ReturnsEnglishByDefault()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);

        svc["Nav_Dashboard"].Should().Be("Dashboard");
    }

    [Fact]
    public void Indexer_AfterSetCulture_ReturnsLocalizedValue()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);
        svc.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));

        svc["Nav_Dashboard"].Should().Be("Bảng điều khiển");
    }

    [Fact]
    public void SupportedCultures_IncludesShippedLanguages()
    {
        var svc = new ResxLocalizationService(NullLogger<ResxLocalizationService>.Instance);

        var names = svc.SupportedCultures.Select(c => c.Name).ToArray();

        names.Should().Contain("en-US");
        names.Should().Contain("vi-VN");
        names.Should().Contain("ar-SA");
    }
}
