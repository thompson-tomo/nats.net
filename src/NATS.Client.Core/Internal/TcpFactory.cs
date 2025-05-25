namespace NATS.Client.Core.Internal
{
    internal sealed class TcpFactory : INatsTransportScheme
    {
        public static INatsTransportScheme Default { get; } = new TcpFactory();

        public List<string> SupportedSchemes => new List<string>() { "tls", "nats" };

        public async ValueTask<INatsSocketConnection> ConnectAsync(Uri uri, NatsOpts opts, CancellationToken cancellationToken)
        {
            var conn = new TcpConnection(opts);
            await conn.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);

            return conn;
        }
    }
}
