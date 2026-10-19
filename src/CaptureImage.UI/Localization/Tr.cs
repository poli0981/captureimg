// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using CaptureImage.Core.Abstractions;

namespace CaptureImage.UI.Localization;

/// <summary>The localization service XAML binds to; set once at startup, before any window loads.</summary>
public static class LocalizationSource
{
    public static ILocalizationService? Service { get; set; }

    /// <summary>The flow direction of the current UI language.</summary>
    public static FlowDirection CurrentFlowDirection =>
        Service?.CurrentFlowDirection == TextFlowDirection.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>A one-way binding to a translated string (for menus built in code).</summary>
    public static Binding Bind(string key) => new($"[{key}]") { Source = Service, Mode = BindingMode.OneWay };

    /// <summary>
    /// Keep <paramref name="window"/>'s <see cref="FrameworkElement.FlowDirection"/> and
    /// <see cref="FrameworkElement.Language"/> in step with the UI language (Arabic is right-to-left).
    /// </summary>
    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var service = Service;
        if (service is null)
        {
            return;
        }

        Apply(window);
        EventHandler handler = (_, _) => window.Dispatcher.BeginInvoke(() => Apply(window));
        service.CultureChanged += handler;
        window.Closed += (_, _) => service.CultureChanged -= handler;
    }

    public static void Apply(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Service is { } service)
        {
            element.FlowDirection = CurrentFlowDirection;
            element.Language = XmlLanguage.GetLanguage(service.CurrentCulture.IetfLanguageTag);
            // Versions, hotkeys, sizes and file names read better with 0-9 in every language
            // (Arabic would otherwise mix Arabic-Indic digits into "8fde965").
            NumberSubstitution.SetSubstitution(element, NumberSubstitutionMethod.European);
        }
    }
}

/// <summary>
/// <c>{loc:Tr Key}</c> — a live binding to a translated string; it updates in place when the
/// language changes. Shows the key itself where no service exists (designer).
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        LocalizationSource.Service is null ? Key : LocalizationSource.Bind(Key).ProvideValue(serviceProvider);
}
