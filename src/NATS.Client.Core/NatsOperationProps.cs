namespace NATS.Client.Core;

public record NatsOperationProps
{
    internal NatsOperationProps(string subject)
        : this(new NatsSubject() { Path = subject })
    {
    }

    internal NatsOperationProps(NatsSubject subject)
    {
        Subject = subject;
    }

    public NatsSubject Subject { get; private set; }
}
