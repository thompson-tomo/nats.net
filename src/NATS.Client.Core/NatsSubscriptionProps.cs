namespace NATS.Client.Core;

public record NatsSubscriptionProps : NatsOperationProps
{
    public NatsSubscriptionProps(string subject, string? queueGroup = default)
        : base(subject)
    {
        QueueGroup = queueGroup;
    }

    public NatsSubscriptionProps(int subscriptionId)
        : base(string.Empty)
    {
        SubscriptionId = subscriptionId;
    }

    public NatsSubscriptionProps(NatsSubject subject)
        : base(subject)
    {
    }

    public int SubscriptionId { get; set; }

    public string? QueueGroup { get; internal set; }

    public NatsRequestReplyMode? RequestReplyMode { get; internal set; }
}
