using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RemoteDesktop.Agent.Compatibility;

namespace RemoteDesktop.Agent.Services;

public sealed class ClipboardSyncService : IDisposable
{
    private const int ClipboardRetryCount = 8;
    private static readonly TimeSpan ClipboardRetryDelay = TimeSpan.FromMilliseconds(50);
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
        return InvokeAsync(() => ExecuteClipboardOperation(() =>
        {
            if (!Clipboard.ContainsText())
            {
                return string.Empty;
            }

            return Clipboard.GetText();
        }), cancellationToken);
    }

    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        return InvokeAsync(() => ExecuteClipboardOperation(() =>
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
        }), cancellationToken);
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

        return Net48Compat.WaitAsync(completionSource.Task, cancellationToken);
    }

    private static T ExecuteClipboardOperation<T>(Func<T> workItem)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < ClipboardRetryCount; attempt++)
        {
            try
            {
                return workItem();
            }
            catch (ExternalException exception)
            {
                lastException = exception;
                Thread.Sleep(ClipboardRetryDelay);
            }
        }

        throw new InvalidOperationException("無法存取 Windows 剪貼簿。 / Failed to access the Windows clipboard.", lastException);
    }
}
