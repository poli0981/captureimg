using Avalonia;
using Avalonia.Controls;
using System.Collections;

namespace CaptureImage.UI.Controls;

public partial class ToastHost : UserControl
{
    /// <summary>
    /// Source collection for the toast stack. The MainWindow sets this to
    /// <c>IToastService.Visible</c>.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ToastHost, IEnumerable?>(nameof(ItemsSource));

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ToastHost()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ToastList.ItemsSource = change.NewValue as IEnumerable;
        }
    }
}
