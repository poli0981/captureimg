using Microsoft.UI.Xaml.Controls;

namespace CaptureImage.UI.Controls;

/// <summary>
/// Toast overlay. Inherits its <c>DataContext</c> from the host (MainWindow's root Grid =
/// MainWindowViewModel) and binds the inner ItemsControl to <c>{Binding Toasts}</c>. The
/// v1.2 custom ItemsSourceProperty is dropped — direct binding is cleaner and sufficient
/// for v1.3 (only one host).
/// </summary>
public sealed partial class ToastHost : UserControl
{
    public ToastHost()
    {
        InitializeComponent();
    }
}
