namespace NATS.Client.Core;

public struct NatsSubject
{
    public string? Template { get; internal set; }

    public string Path { get; internal set; }

    public string SanitisedPath
    {
        get
        { // to avoid long span names and low cardinality, only take the first two tokens
            var tokens = Path.Split('.');
            return tokens.Length < 2 ? Path : $"{tokens[0]}.{tokens[1]}";
        }
    }

    internal string Type { get; set; }

    internal bool IsInbox(string inboxPrefix) => !string.IsNullOrEmpty(inboxPrefix)
        && inboxPrefix != null
        && (Template?.StartsWith(inboxPrefix, StringComparison.Ordinal) == true ||
            Path.StartsWith(inboxPrefix, StringComparison.Ordinal));
}
