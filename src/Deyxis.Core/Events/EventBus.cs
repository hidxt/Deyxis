namespace Deyxis.Core.Events;

public sealed class EventBus : IEventBus
{
    private readonly object gate = new();
    private readonly Dictionary<Type, List<Subscription>> subscriptions = [];

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription<TEvent>(handler);
        lock (gate)
        {
            if (!subscriptions.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers = [];
                subscriptions.Add(typeof(TEvent), handlers);
            }

            handlers.Add(subscription);
        }

        return new SubscriptionHandle(this, subscription);
    }

    public void Publish<TEvent>(TEvent message) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        Subscription[] handlers;
        lock (gate)
        {
            handlers = subscriptions.TryGetValue(typeof(TEvent), out var registeredHandlers)
                ? [.. registeredHandlers]
                : [];
        }

        foreach (var handler in handlers)
        {
            try
            {
                ((Subscription<TEvent>)handler).Invoke(message);
            }
            catch (Exception)
            {
            }
        }
    }

    private void Unsubscribe(Subscription subscription)
    {
        lock (gate)
        {
            if (!subscriptions.TryGetValue(subscription.EventType, out var handlers))
            {
                return;
            }

            handlers.Remove(subscription);
            if (handlers.Count == 0)
            {
                subscriptions.Remove(subscription.EventType);
            }
        }
    }

    private abstract class Subscription(Type eventType)
    {
        public Type EventType { get; } = eventType;
    }

    private sealed class Subscription<TEvent>(Action<TEvent> handler) : Subscription(typeof(TEvent)) where TEvent : notnull
    {
        public void Invoke(TEvent message) => handler(message);
    }

    private sealed class SubscriptionHandle(EventBus bus, Subscription subscription) : IDisposable
    {
        private EventBus? bus = bus;

        public void Dispose()
        {
            var eventBus = Interlocked.Exchange(ref bus, null);
            eventBus?.Unsubscribe(subscription);
        }
    }
}
