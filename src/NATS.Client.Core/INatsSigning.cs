namespace NATS.Client.Core;

public interface INatsSigning
{
    string? Sign(string? nonce, string? seed = null);
}
