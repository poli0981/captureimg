// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.ViewModels.Settings;

public sealed record ThemeOption(string Code, string DisplayLabel)
{
    public override string ToString() => DisplayLabel;
}
