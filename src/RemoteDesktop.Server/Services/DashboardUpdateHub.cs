using System.Collections.Concurrent;
using System.Threading.Channels;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services;

public sealed class DashboardUpdateHub
{
    private readonly ConcurrentDictionary<Guid, Channel<DashboardUpdateEnvelope>> _subscribers = new();

    public DashboardUpdateSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<DashboardUpdateEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers[id] = channel;
        return new DashboardUpdateSubscription(id, channel.Reader, this);
    }

    public void Publish(string reason, string? deviceId = null)
    {
        var envelope = new DashboardUpdateEnvelope
        {
            Type = "dashboard-changed",
            Reason = reason,
            DeviceId = deviceId,
            OccurredAt = DateTimeOffset.UtcNow
        };

        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(envelope);
        }
    }

    private void Unsubscribe(Guid subscriptionId)
    {
        if (_subscribers.TryRemove(subscriptionId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public sealed class DashboardUpdateSubscription : IDisposable
    {
        private readonly Guid _subscriptionId;
        private readonly DashboardUpdateHub _owner;
        private int _disposed;

        internal DashboardUpdateSubscription(Guid subscriptionId, ChannelReader<DashboardUpdateEnvelope> reader, DashboardUpdateHub owner)
        {
            _subscriptionId = subscriptionId;
            Reader = reader;
            _owner = owner;
        }

        public ChannelReader<DashboardUpdateEnvelope> Reader { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _owner.Unsubscribe(_subscriptionId);
        }
    }
}
