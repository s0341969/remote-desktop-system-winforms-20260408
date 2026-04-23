using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;
}

namespace RemoteDesktop.Agent.Compatibility
{
    internal static class Net48Compat
    {
        public static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

        public static decimal Clamp(decimal value, decimal min, decimal max) => Math.Min(Math.Max(value, min), max);

        public static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);

        public static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            var delayTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            var completedTask = await Task.WhenAny(task, delayTask);
            if (completedTask == delayTask)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return await task;
        }

        public static async Task AppendAllTextAsync(string path, string content, CancellationToken cancellationToken)
        {
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(content);
                await writer.FlushAsync();
            }
        }

        public static Task DisposeAsyncCompat(IDisposable disposable)
        {
            disposable.Dispose();
            return Task.CompletedTask;
        }
    }

    internal sealed class PeriodicTimer : IDisposable
    {
        private readonly TimeSpan _interval;

        public PeriodicTimer(TimeSpan interval)
        {
            _interval = interval;
        }

        public async Task<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(_interval, cancellationToken);
            return !cancellationToken.IsCancellationRequested;
        }

        public void Dispose()
        {
        }
    }

    internal static class ProcessCompatibilityExtensions
    {
        public static Task WaitForExitAsyncCompat(this Process process, CancellationToken cancellationToken)
        {
            if (process.HasExited)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler handler = null;
            handler = (sender, args) =>
            {
                process.Exited -= handler;
                tcs.TrySetResult(null);
            };

            process.EnableRaisingEvents = true;
            process.Exited += handler;

            if (process.HasExited)
            {
                process.Exited -= handler;
                return Task.CompletedTask;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return tcs.Task;
            }

            var registration = cancellationToken.Register(() =>
            {
                process.Exited -= handler;
                tcs.TrySetCanceled();
            });

            return tcs.Task.ContinueWith(
                antecedent =>
                {
                    registration.Dispose();
                    return antecedent;
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();
        }
    }
}
