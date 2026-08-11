namespace Deyxis.Core.Events;

public interface IEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;

    void Publish<TEvent>(TEvent message) where TEvent : notnull;
}
