using Deyxis.Core.Events;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class EventBusTests
{
    [Fact]
    public void Publish_delivers_a_message_to_a_matching_subscriber()
    {
        ActivityUpserted? observed = null;
        var bus = new EventBus();
        using var subscription = bus.Subscribe<ActivityUpserted>(message => observed = message);
        var message = new ActivityUpserted(TestActivity.Create());

        bus.Publish(message);

        Assert.Same(message, observed);
    }

    [Fact]
    public void Publish_delivers_messages_to_subscribers_in_subscription_order()
    {
        var deliveries = new List<string>();
        var bus = new EventBus();
        using var first = bus.Subscribe<ActivityUpserted>(_ => deliveries.Add("first"));
        using var second = bus.Subscribe<ActivityUpserted>(_ => deliveries.Add("second"));

        bus.Publish(new ActivityUpserted(TestActivity.Create()));

        Assert.Equal(["first", "second"], deliveries);
    }

    [Fact]
    public void Publish_does_not_deliver_a_message_to_a_different_event_type()
    {
        var deliveryCount = 0;
        var bus = new EventBus();
        using var subscription = bus.Subscribe<ActivityRemoved>(_ => deliveryCount++);

        bus.Publish(new ActivityUpserted(TestActivity.Create()));

        Assert.Equal(0, deliveryCount);
    }

    [Fact]
    public void Publish_continues_delivery_when_a_subscriber_throws()
    {
        ActivityUpserted? observed = null;
        var bus = new EventBus();
        using var throwingSubscription = bus.Subscribe<ActivityUpserted>(_ => throw new InvalidOperationException());
        using var receivingSubscription = bus.Subscribe<ActivityUpserted>(message => observed = message);
        var message = new ActivityUpserted(TestActivity.Create());

        bus.Publish(message);

        Assert.Same(message, observed);
    }

    [Fact]
    public void Disposed_subscription_receives_no_later_messages()
    {
        var deliveryCount = 0;
        var bus = new EventBus();
        var subscription = bus.Subscribe<ActivityUpserted>(_ => deliveryCount++);

        subscription.Dispose();
        bus.Publish(new ActivityUpserted(TestActivity.Create()));

        Assert.Equal(0, deliveryCount);
    }

    [Fact]
    public void Disposing_one_of_duplicate_handlers_removes_only_its_registration()
    {
        var deliveryCount = 0;
        var bus = new EventBus();
        Action<ActivityUpserted> handler = _ => deliveryCount++;
        var firstSubscription = bus.Subscribe(handler);
        using var secondSubscription = bus.Subscribe(handler);

        firstSubscription.Dispose();
        bus.Publish(new ActivityUpserted(TestActivity.Create()));

        Assert.Equal(1, deliveryCount);
    }

    [Fact]
    public void Subscribers_added_while_publishing_receive_only_later_messages()
    {
        var deliveryCount = 0;
        var bus = new EventBus();
        IDisposable? laterSubscription = null;
        using var firstSubscription = bus.Subscribe<ActivityUpserted>(_ =>
        {
            laterSubscription ??= bus.Subscribe<ActivityUpserted>(_ => deliveryCount++);
        });

        bus.Publish(new ActivityUpserted(TestActivity.Create()));
        bus.Publish(new ActivityUpserted(TestActivity.Create()));

        laterSubscription!.Dispose();
        Assert.Equal(1, deliveryCount);
    }

    [Fact]
    public void Subscribe_rejects_a_null_handler()
    {
        var bus = new EventBus();

        Assert.Throws<ArgumentNullException>(() => bus.Subscribe<ActivityUpserted>(null!));
    }

    [Fact]
    public void Publish_rejects_a_null_message()
    {
        var bus = new EventBus();

        Assert.Throws<ArgumentNullException>(() => bus.Publish<ActivityUpserted>(null!));
    }
}
