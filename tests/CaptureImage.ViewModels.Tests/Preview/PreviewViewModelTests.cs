using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using CaptureImage.Core.Abstractions;
using CaptureImage.ViewModels.Preview;
using FluentAssertions;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Preview;

/// <summary>
/// Regression guard for the v1.1.2 M1 fix: <see cref="PreviewViewModel"/> implements
/// <see cref="IDisposable"/> and wires a named <c>OnLocalizationChanged</c> handler that
/// forwards <c>"Item[]"</c> and <c>CurrentCulture</c> notifications to the VM's own
/// <c>Localization</c> property so bindings in <c>PreviewWindow.axaml</c> retranslate.
/// </summary>
public class PreviewViewModelTests
{
    [Fact]
    public void ItemIndexerChange_RaisesLocalizationPropertyChanged()
    {
        var localization = new FakeLocalizationService();
        using var vm = new PreviewViewModel(localization);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        localization.RaiseItemIndexer();

        raised.Should().Contain(nameof(PreviewViewModel.Localization));
    }

    [Fact]
    public void CurrentCultureChange_RaisesLocalizationPropertyChanged()
    {
        var localization = new FakeLocalizationService();
        using var vm = new PreviewViewModel(localization);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        localization.RaiseCurrentCulture();

        raised.Should().Contain(nameof(PreviewViewModel.Localization));
    }

    [Fact]
    public void UnrelatedPropertyChange_DoesNotRaiseLocalization()
    {
        var localization = new FakeLocalizationService();
        using var vm = new PreviewViewModel(localization);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        localization.RaiseOther(nameof(ILocalizationService.CurrentFlowDirection));

        raised.Should().NotContain(nameof(PreviewViewModel.Localization));
    }

    [Fact]
    public void Dispose_DetachesLocalizationHandler()
    {
        var localization = new FakeLocalizationService();
        var vm = new PreviewViewModel(localization);
        var raised = 0;
        vm.PropertyChanged += (_, _) => raised++;

        vm.Dispose();
        localization.RaiseItemIndexer();

        raised.Should().Be(0, "Dispose must detach the subscription so the singleton service "
            + "doesn't leak one handler per preview modal");
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("en-US");
        public TextFlowDirection CurrentFlowDirection => TextFlowDirection.LeftToRight;
        public IReadOnlyList<CultureInfo> SupportedCultures { get; } = new[] { CultureInfo.GetCultureInfo("en-US") };
        public string this[string key] => $"[{key}]";

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CultureChanged;

        public void SetCulture(CultureInfo culture)
        {
            CurrentCulture = culture;
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseItemIndexer() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));

        public void RaiseCurrentCulture() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));

        public void RaiseOther(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
