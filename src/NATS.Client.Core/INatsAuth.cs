namespace NATS.Client.Core;

public interface INatsAuth
{
    public Task<NatsAuthCred> GetAuthCredAsync(CancellationToken cancellationToken);
}
