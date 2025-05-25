namespace NATS.Client.Core;

public interface INatsTransportScheme : INatsSocketConnectionFactory
{
    public List<string> SupportedSchemes { get; }
}
