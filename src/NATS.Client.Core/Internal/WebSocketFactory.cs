namespace NATS.Client.Core.Internal
{
    internal sealed class WebSocketFactory : INatsTransportScheme
    {
        public static INatsTransportScheme Default { get; } = new WebSocketFactory();

        public List<string> SupportedSchemes => new List<string>() { "ws", "wss" };

        public async ValueTask<INatsSocketConnection> ConnectAsync(Uri uri, NatsOpts opts, CancellationToken cancellationToken)
        {
            var conn = new WebSocketConnection(opts);
            await conn.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);

            return conn;
        }
    }
}
