// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using CaptureImage.UI.Imaging;
using Wpf.Ui.Controls;

namespace CaptureImage.UI.Converters;

/// <summary><c>true</c> → Visible; with <see cref="Invert"/>, <c>false</c> → Visible.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is Visibility.Visible) ^ Invert;
}

/// <summary>Visible when the value is non-null (and, for strings, not empty); <see cref="Invert"/> flips it.</summary>
public sealed class HasValueToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is string s ? s.Length > 0 : value is not null;
        return hasValue ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when a count is zero (empty-state messages).</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Button appearance from a flag (the hotkey button turns Primary while recording).</summary>
public sealed class BoolToAppearanceConverter : IValueConverter
{
    public ControlAppearance WhenTrue { get; set; } = ControlAppearance.Primary;

    public ControlAppearance WhenFalse { get; set; } = ControlAppearance.Secondary;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? WhenTrue : WhenFalse;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>PNG bytes (window icons) → image, cached per byte array.</summary>
public sealed class BytesToImageConverter : IValueConverter
{
    private static readonly ConditionalWeakTable<byte[], BitmapImage> Cache = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes)
        {
            return null;
        }

        if (Cache.TryGetValue(bytes, out var cached))
        {
            return cached;
        }

        var image = FrameBitmap.LoadBytes(bytes);
        if (image is not null)
        {
            Cache.AddOrUpdate(bytes, image);
        }
        return image;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Image file path → image decoded at <see cref="DecodeWidth"/> without locking the file.</summary>
public sealed class FileToImageConverter : IValueConverter
{
    public int DecodeWidth { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        FrameBitmap.LoadFile(value as string, DecodeWidth);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
