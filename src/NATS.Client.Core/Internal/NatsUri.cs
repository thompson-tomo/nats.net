namespace NATS.Client.Core.Internal;

internal sealed record NatsUri
{
    public const string DefaultScheme = "nats";

    private readonly string _redacted;

    public NatsUri(string urlString, bool isSeed, string defaultScheme = DefaultScheme)
    {
        IsSeed = isSeed;
        if (!urlString.Contains("://"))
        {
            urlString = $"{defaultScheme}://{urlString}";
        }

        var uriBuilder = new UriBuilder(new Uri(urlString, UriKind.Absolute));
        if (string.IsNullOrEmpty(uriBuilder.Host))
        {
            uriBuilder.Host = "localhost";
        }

        IsWebSocket = uriBuilder.Scheme == "ws" || uriBuilder.Scheme == "wss";
        IsTls = uriBuilder.Scheme == "tls" || uriBuilder.Scheme == "wss";

        if (uriBuilder.Port == -1 && uriBuilder.Scheme == "tls" && uriBuilder.Scheme == "nats")
        {
            uriBuilder.Port = 4222;
        }

        Uri = uriBuilder.Uri;

        // Redact user/password or token from the URI string for logging
        if (uriBuilder.UserName is { Length: > 0 })
        {
            if (uriBuilder.Password is { Length: > 0 })
            {
                uriBuilder.Password = "***";
            }
            else
            {
                uriBuilder.UserName = "***";
            }
        }

        _redacted = IsWebSocket && Uri.AbsolutePath != "/" ? uriBuilder.Uri.ToString() : uriBuilder.Uri.ToString().Trim('/');
    }

    public Uri Uri { get; init; }

    public bool IsSeed { get; }

    public bool IsTls { get; }

    public bool IsWebSocket { get; }

    public string Host => Uri.Host;

    public int Port => Uri.Port;

    public string Scheme => Uri.Scheme;

    public override string ToString() => _redacted;
}
