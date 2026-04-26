using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace CaptureImage.UI.Services;

/// <summary>
/// <see cref="IUIThreadDispatcher"/> implementation that marshals to the WinUI 3
/// <see cref="DispatcherQueue"/>. The queue must be captured on the UI thread, so callers
/// resolve this concretely from DI in <c>App.OnLaunched</c> and invoke <see cref="Bind"/>
/// before any background code starts posting work. Calls before binding fall back to
/// running inline with a warning so first-frame startup doesn't silently drop work.
/// </summary>
public sealed class WinUIThreadDispatcher : IUIThreadDispatcher
{
    private readonly ILogger<WinUIThreadDispatcher> _logger;
    private DispatcherQueue? _queue;

    public WinUIThreadDispatcher(ILogger<WinUIThreadDispatcher> logger)
    {
        _logger = logger;
    }

    public void Bind(DispatcherQueue queue)
    {
        _queue = queue;
    }

    public bool IsOnUIThread => _queue is not null && _queue.HasThreadAccess;

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var queue = _queue;
        if (queue is null)
        {
            _logger.LogWarning("Post called before WinUIThreadDispatcher was bound; running inline.");
            action();
            return;
        }
        queue.TryEnqueue(() => action());
    }

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var queue = _queue;
        if (queue is null)
        {
            _logger.LogWarning("InvokeAsync called before WinUIThreadDispatcher was bound; running inline.");
            action();
            return Task.CompletedTask;
        }
        if (queue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }
        var tcs = new TaskCompletionSource();
        var enqueued = queue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        if (!enqueued)
        {
            tcs.SetException(new InvalidOperationException("DispatcherQueue.TryEnqueue refused the work item."));
        }
        return tcs.Task;
    }
}
