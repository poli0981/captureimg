// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.UI.Pages;

/// <summary>
/// A page told whether it is on screen (navigated to and the window visible), so its view
/// model only refreshes while someone can see it.
/// </summary>
public interface IActivatablePage
{
    void SetActive(bool active);
}
