namespace NATS.Client.Core;

public record NatsOperationProps
{
    internal NatsOperationProps(string subject)
        : this(new NatsSubject(subject))
    {
    }

    internal NatsOperationProps(string subjectTemplate, string subjectId)
        : this(new NatsSubject(subjectTemplate, "SubjectId", subjectId))
    {
    }

    internal NatsOperationProps(string subjectTemplate, Dictionary<string, object> properties)
        : this(new NatsSubject(subjectTemplate, properties))
    {
    }

    internal NatsOperationProps(NatsSubject subject)
    {
        Subject = subject;
    }

    public NatsSubject Subject { get; private set; }
}
