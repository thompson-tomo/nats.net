namespace NATS.Client.Core;

internal class NatsConnectionFactoryResolver
{
    private INatsSocketConnectionFactory? _defaultTransport = null;
    private List<INatsTransportScheme> _transportSchemes = [];

    internal void SetTransportScheme(INatsSocketConnectionFactory factory)
    {
        _defaultTransport = factory;
    }

    internal void AddTransportScheme(INatsTransportScheme factory)
    {
        _transportSchemes.Add(factory);
    }

    internal INatsSocketConnectionFactory Resolve(string scheme)
    {
        if (_defaultTransport != null)
        {
            return _defaultTransport;
        }

        return _transportSchemes.FirstOrDefault(x => x.SupportedSchemes.Contains(scheme)) ?? throw new NatsException($"Unable to find connection for scheme {scheme}");
    }
}
