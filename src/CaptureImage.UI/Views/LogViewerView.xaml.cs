using Microsoft.UI.Xaml.Controls;

namespace CaptureImage.UI.Views;

/// <summary>
/// Slide-in log drawer. The animated show/hide is driven by <c>MainWindow</c> code-behind
/// against the host's <c>IsLogViewerVisible</c> property — keeping this UserControl
/// presentation-only means it stays composable in tests + the future toast/preview
/// surfaces can reuse the same Composition pattern.
/// </summary>
public sealed partial class LogViewerView : UserControl
{
    public LogViewerView()
    {
        InitializeComponent();
    }
}
