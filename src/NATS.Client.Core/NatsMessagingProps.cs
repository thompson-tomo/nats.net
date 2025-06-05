namespace NATS.Client.Core;

public record NatsMessagingProps : NatsOperationProps
{
    internal NatsMessagingProps(NatsSubject subject)
        : base(subject)
    {
    }

    internal NatsMessagingProps(string subject)
        : base(subject)
    {
    }

    public NatsSubject? ReplyTo { get; private set; } = null;

    internal int PayloadLength => TotalMessageLength - HeaderLength;

    internal int HeaderLength { get; set; }

    internal int TotalMessageLength { get; set; }

    internal int FramingLength { get; set; }

    internal int TotalEnvelopeLength => TotalMessageLength + FramingLength;

    public void SetReplyTo(NatsSubject replyTo)
    {
        ReplyTo = replyTo;
    }

    public void SetReplyTo(string? replyTo)
    {
        if (replyTo != null)
        {
            SetReplyTo(new NatsSubject()
            {
                Path = replyTo,
                Type = "USER",
            });
        }
    }
}
