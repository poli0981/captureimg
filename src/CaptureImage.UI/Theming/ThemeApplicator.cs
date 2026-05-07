using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace CaptureImage.UI.Theming;

/// <summary>
/// Single source of truth for mapping the persisted theme string
/// (<c>"Light"</c> / <c>"Dark"</c> / anything else → System) onto a window's
/// <see cref="FrameworkElement.RequestedTheme"/>. Both <c>MainWindow</c> and
/// secondary windows (Preview, pinned thumbnail) call into here so the
/// switch lives in one place and never drifts.
/// </summary>
public static class ThemeApplicator
{
    /// <summary>
    /// Apply the theme string to the given root element. Safe to call from any
    /// thread — the actual property assignment is hopped onto the dispatcher.
    /// A null root is a no-op (the window may not be loaded yet).
    /// </summary>
    public static void Apply(FrameworkElement? root, string theme)
    {
        if (root is null) return;

        root.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark"  => ElementTheme.Dark,
                _       => ElementTheme.Default,
            };
        });
    }
}
