using NATS.Client.Core;
using NATS.Client.JetStream.Internal;
using NATS.Client.JetStream.Models;

namespace NATS.Client.JetStream;

/// <summary>
/// Represents a NATS JetStream stream.
/// </summary>
public class NatsJSStream : INatsJSStream
{
    private readonly NatsJSContext _context;
    private readonly string _name;
    private bool _deleted;

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
#endif
    internal NatsJSStream(NatsJSContext context, StreamInfo info)
    {
#if NETSTANDARD
        ArgumentNullExceptionEx.ThrowIfNull(info.Config.Name, nameof(info.Config.Name));
#else
        ArgumentNullException.ThrowIfNull(info.Config.Name, nameof(info.Config.Name));
#endif
        _context = context;
        Info = info;
        _name = info.Config.Name!;
    }

    /// <summary>
    /// Stream info object as retrieved from NATS JetStream server at the time this object was created, updated or refreshed.
    /// </summary>
    public StreamInfo Info { get; private set; }

    /// <summary>
    /// Delete this stream.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <returns>Whether delete was successful or not.</returns>
    /// <exception cref="NatsJSException">There is an error retrieving the response or this consumer object isn't valid anymore because it was deleted earlier.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    /// <remarks>After deletion this object can't be used anymore.</remarks>
    public async ValueTask<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        return _deleted = await _context.DeleteStreamAsync(_name, cancellationToken);
    }

    /// <summary>
    /// Purge data from this stream. Leaves the stream.
    /// </summary>
    /// <param name="request">Purge request.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <returns>Whether delete was successful or not.</returns>
    /// <exception cref="NatsJSException">There is an error retrieving the response or this consumer object isn't valid anymore because it was deleted earlier.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    /// <remarks>After deletion this object can't be used anymore.</remarks>
    public async ValueTask<StreamPurgeResponse> PurgeAsync(StreamPurgeRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        return await _context.PurgeStreamAsync(_name, request, cancellationToken);
    }

    /// <summary>
    /// Deletes a message from a stream.
    /// </summary>
    /// <param name="request">Delete message request.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <returns>Delete message response</returns>
    /// <exception cref="NatsJSException">There was an issue retrieving the response.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    public async ValueTask<StreamMsgDeleteResponse> DeleteMessageAsync(StreamMsgDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        return await _context.DeleteMessageAsync(_name, request, cancellationToken);
    }

    /// <summary>
    /// Update stream properties on the server.
    /// </summary>
    /// <param name="request">Stream update request to be sent to the server.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <exception cref="NatsJSException">There is an error retrieving the response or this consumer object isn't valid anymore because it was deleted earlier.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    public async ValueTask UpdateAsync(
        StreamConfig request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        var response = await _context.UpdateStreamAsync(request, cancellationToken);
        Info = response.Info;
    }

    public ValueTask<INatsJSConsumer> CreateOrderedConsumerAsync(NatsJSOrderedConsumerOpts? opts = default, CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        return _context.CreateOrderedConsumerAsync(_name, opts, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<INatsJSConsumer> CreateOrUpdateConsumerAsync(ConsumerConfig config, CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        return _context.CreateOrUpdateConsumerAsync(_name, config, cancellationToken);
    }

    /// <summary>
    /// Gets consumer information from the server and creates a NATS JetStream consumer <see cref="NatsJSConsumer"/>.
    /// </summary>
    /// <param name="consumer">Consumer name.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <returns>The NATS JetStream consumer object which can be used retrieving data from the stream.</returns>
    /// <exception cref="NatsJSException">There is an error retrieving the response or this consumer object isn't valid anymore because it was deleted earlier.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    public ValueTask<INatsJSConsumer> GetConsumerAsync(string consumer, CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        return _context.GetConsumerAsync(_name, consumer, cancellationToken);
    }

    /// <summary>
    /// Enumerates through consumers that belong to this stream.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <returns>Async enumerable of consumer objects. Can be used in a <c>await foreach</c> loop.</returns>
    /// <exception cref="NatsJSException">There is an error retrieving the response or this consumer object isn't valid anymore because it was deleted earlier.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    public IAsyncEnumerable<INatsJSConsumer> ListConsumersAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        return _context.ListConsumersAsync(_name, cancellationToken);
    }

    /// <summary>
    /// Enumerates through consumer names that belong to this stream.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <returns>Async enumerable of consumer names. Can be used in a <c>await foreach</c> loop.</returns>
    /// <exception cref="NatsJSException">There is an error retrieving the response or this consumer object isn't valid anymore because it was deleted earlier.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    public IAsyncEnumerable<string> ListConsumerNamesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        return _context.ListConsumerNamesAsync(_name, cancellationToken);
    }

    /// <summary>
    /// Delete a consumer from this stream.
    /// </summary>
    /// <param name="consumer">Consumer name to be deleted.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <returns>Whether the deletion was successful.</returns>
    /// <exception cref="NatsJSException">There is an error retrieving the response or this consumer object isn't valid anymore because it was deleted earlier.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    public ValueTask<bool> DeleteConsumerAsync(string consumer, CancellationToken cancellationToken = default)
    {
        ThrowIfDeleted();
        return _context.DeleteConsumerAsync(_name, consumer, cancellationToken);
    }

    /// <summary>
    /// Retrieve the stream info from the server and update this stream.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <exception cref="NatsJSException">There was an issue retrieving the response.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        var prop = new NatsPublishProps(new NatsSubject(
            "{prefix}.{entity}.{action}.{id}",
            new Dictionary<string, object>()
            {
                { "prefix", _context.Opts.Prefix },
                { "entity", "STREAM" },
                { "action", "INFO" },
                { "id", _name },
            }));
        var response = await _context.JSRequestAsync<object, StreamInfoResponse>(prop, null, cancellationToken);
        response.EnsureSuccess();
        Info = response.Response!;
    }

    public ValueTask<NatsMsg<T>> GetDirectAsync<T>(StreamMsgGetRequest request, INatsDeserialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var prop = new NatsPublishProps(new NatsSubject(
            "{prefix}.{entity}.{action}.{id}",
            new Dictionary<string, object>()
            {
                { "prefix", _context.Opts.Prefix },
                { "entity", "DIRECT" },
                { "action", "GET" },
                { "id", _name },
            }));
        return _context.Connection.RequestAsync<StreamMsgGetRequest, T>(
            subject: string.Empty,
            data: request,
            requestOpts: new NatsPubOpts() { Props = prop },
            requestSerializer: NatsJSJsonSerializer<StreamMsgGetRequest>.Default,
            replySerializer: serializer,
            cancellationToken: cancellationToken);
    }

    public ValueTask<StreamMsgGetResponse> GetAsync(StreamMsgGetRequest request, CancellationToken cancellationToken = default)
    {
        var prop = new NatsPublishProps(new NatsSubject(
            "{prefix}.{entity}.{subentity}.{action}.{id}",
            new Dictionary<string, object>()
            {
                { "prefix", _context.Opts.Prefix },
                { "entity", "STREAM" },
                { "subentity", "MSG" },
                { "action", "GET" },
                { "id", _name },
            }));
        return JSRequestResponseAsync<StreamMsgGetRequest, StreamMsgGetResponse>(prop, request, cancellationToken);
    }

    private async ValueTask<TResponse> JSRequestResponseAsync<TRequest, TResponse>(
        NatsPublishProps prop,
        TRequest? request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        var response = await _context.JSRequestAsync<TRequest, TResponse>(prop, request, cancellationToken);
        response.EnsureSuccess();
        return response.Response!;
    }

    private void ThrowIfDeleted()
    {
        if (_deleted)
            throw new NatsJSException($"Stream '{_name}' is deleted");
    }
}
