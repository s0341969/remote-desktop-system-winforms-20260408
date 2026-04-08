using System.Collections.Concurrent;
using System.Windows.Forms;

namespace RemoteDesktop.Agent.Services;

public sealed class ClipboardSyncService : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _clipboardThread;
    private bool _disposed;

    public ClipboardSyncService()
    {
        _clipboardThread = new Thread(RunClipboardLoop)
        {
            IsBackground = true,
            Name = "RemoteDesktop.Agent.Clipboard"
        };
        _clipboardThread.SetApartmentState(ApartmentState.STA);
        _clipboardThread.Start();
    }

    public Task<string> GetTextAsync(CancellationToken cancellationToken)
    {
        return InvokeAsync(() =>
        {
            if (!Clipboard.ContainsText())
            {
                return string.Empty;
            }

            return Clipboard.GetText();
        }, cancellationToken);
    }

    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        return InvokeAsync(() =>
        {
            if (string.IsNullOrEmpty(text))
            {
                Clipboard.Clear();
            }
            else
            {
                Clipboard.SetText(text);
            }

            return true;
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();
    }

    private void RunClipboardLoop()
    {
        foreach (var workItem in _queue.GetConsumingEnumerable())
        {
            workItem();
        }
    }

    private Task<T> InvokeAsync<T>(Func<T> workItem, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClipboardSyncService));
        }

        var completionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                completionSource.TrySetResult(workItem());
            }
            catch (Exception exception)
            {
                completionSource.TrySetException(exception);
            }
        }, cancellationToken);

        return completionSource.Task.WaitAsync(cancellationToken);
    }
}
