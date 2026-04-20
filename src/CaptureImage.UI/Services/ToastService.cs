using System.Collections.ObjectModel;
using Avalonia.Threading;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;

namespace CaptureImage.UI.Services;

/// <summary>
/// In-app toast service. Keeps at most <see cref="MaxVisible"/> toasts on screen and auto-
/// dismisses each after its <see cref="ToastItem.EffectiveDuration"/> elapses.
/// </summary>
/// <remarks>
/// Lives in the UI project because it touches <see cref="Dispatcher.UIThread"/> directly,
/// which keeps the portable ViewModels assembly free of Avalonia.
/// </remarks>
public sealed class ToastService : IToastService
{
    /// <summary>Maximum number of simultaneously-visible toasts.</summary>
    public const int MaxVisible = 3;

    private readonly IUIThreadDispatcher _dispatcher;

    public ToastService(IUIThreadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public ObservableCollection<ToastItem> Visible { get; } = new();

    public void Show(ToastItem item)
    {
        _dispatcher.Post(() =>
        {
            while (Visible.Count >= MaxVisible)
            {
                Visible.RemoveAt(0);
            }
            Visible.Add(item);

            var duration = item.EffectiveDuration;
            if (duration > TimeSpan.Zero)
            {
                _ = ScheduleDismissAsync(item.Id, duration);
            }
        });
    }

    public void Dismiss(Guid id)
    {
        _dispatcher.Post(() =>
        {
            for (var i = 0; i < Visible.Count; i++)
            {
                if (Visible[i].Id == id)
                {
                    Visible.RemoveAt(i);
                    return;
                }
            }
        });
    }

    public void ShowSuccess(string title, string message) =>
        Show(new ToastItem(title, message, ToastKind.Success));

    public void ShowError(string title, string message) =>
        Show(new ToastItem(title, message, ToastKind.Error));

    public void ShowInfo(string title, string message) =>
        Show(new ToastItem(title, message, ToastKind.Info));

    public void ShowWarning(string title, string message) =>
        Show(new ToastItem(title, message, ToastKind.Warning));

    private async Task ScheduleDismissAsync(Guid id, TimeSpan duration)
    {
        try
        {
            await Task.Delay(duration).ConfigureAwait(false);
        }
        catch
        {
            // Ignore — scheduling failure just means no auto-dismiss.
        }
        Dismiss(id);
    }
}
