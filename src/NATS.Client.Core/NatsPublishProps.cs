namespace NATS.Client.Core;

public record NatsPublishProps : NatsMessagingProps
{
    public NatsPublishProps(NatsSubject subject)
        : base(subject)
    {
    }

    public NatsPublishProps(string subject)
        : base(subject)
    {
    }
}
