using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CaptureImage.Core.Abstractions;

namespace CaptureImage.UI.Services;

/// <summary>
/// <see cref="IUIThreadDispatcher"/> implementation that marshals to
/// <see cref="Dispatcher.UIThread"/>. Kept in the UI project so the ViewModels project
/// never has to reference Avalonia.
/// </summary>
public sealed class AvaloniaUIDispatcher : IUIThreadDispatcher
{
    public bool IsOnUIThread => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();
}
