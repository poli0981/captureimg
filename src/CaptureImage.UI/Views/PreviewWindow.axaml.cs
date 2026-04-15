using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CaptureImage.UI.Views;

public partial class PreviewWindow : Window
{
    private readonly TaskCompletionSource<bool> _result = new();

    public PreviewWindow()
    {
        InitializeComponent();

        var saveButton = this.FindControl<Button>("SaveButton");
        var discardButton = this.FindControl<Button>("DiscardButton");

        if (saveButton is not null)
        {
            saveButton.Click += OnSaveClick;
        }
        if (discardButton is not null)
        {
            discardButton.Click += OnDiscardClick;
        }
        Closed += OnClosed;
    }

    /// <summary>Task that completes when the user accepts (true) or discards (false).</summary>
    public Task<bool> ResultAsync => _result.Task;

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _result.TrySetResult(true);
        Close();
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        _result.TrySetResult(false);
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Closing via the X button counts as discard.
        _result.TrySetResult(false);
    }
}
