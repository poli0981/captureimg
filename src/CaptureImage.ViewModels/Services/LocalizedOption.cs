// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CaptureImage.ViewModels.Services;

/// <summary>
/// A combo-box choice whose label follows the UI language. The item stays the same object
/// across language switches (only <see cref="Label"/> changes), so selections survive.
/// </summary>
/// <typeparam name="T">Value written to settings.</typeparam>
public sealed class LocalizedOption<T> : ObservableObject
{
    private readonly Func<string> _label;

    public LocalizedOption(T value, Func<string> label)
    {
        ArgumentNullException.ThrowIfNull(label);
        Value = value;
        _label = label;
    }

    public T Value { get; }

    public string Label => _label();

    public override string ToString() => Label;

    /// <summary>Re-read the label (after a language switch).</summary>
    public void Refresh() => OnPropertyChanged(nameof(Label));
}

public static class LocalizedOption
{
    /// <summary>An option labelled by a resx key.</summary>
    public static LocalizedOption<T> Create<T>(T value, ILocalizationService localization, string key) =>
        new(value, () => localization[key]);

    /// <summary>An option with a label that never changes (format names, language names).</summary>
    public static LocalizedOption<T> Fixed<T>(T value, string label) => new(value, () => label);

    public static void RefreshAll<T>(IEnumerable<LocalizedOption<T>> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        foreach (var option in options)
        {
            option.Refresh();
        }
    }
}
