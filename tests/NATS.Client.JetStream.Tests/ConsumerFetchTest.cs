using NATS.Client.Core.Tests;
using NATS.Client.Core2.Tests;

namespace NATS.Client.JetStream.Tests;

[Collection("nats-server")]
public class ConsumerFetchTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public ConsumerFetchTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Theory]
    [InlineData(NatsRequestReplyMode.Direct)]
    [InlineData(NatsRequestReplyMode.SharedInbox)]
    public async Task Fetch_test(NatsRequestReplyMode mode)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url, RequestReplyMode = mode });
        var prefix = _server.GetNextId();
        var js = new NatsJSContext(nats);
        await js.CreateStreamAsync($"{prefix}s1", new[] { $"{prefix}s1.*" }, cts.Token);
        await js.CreateOrUpdateConsumerAsync($"{prefix}s1", $"{prefix}c1", cancellationToken: cts.Token);

        for (var i = 0; i < 10; i++)
        {
            var ack = await js.PublishAsync($"{prefix}s1.foo", new TestData { Test = i }, serializer: TestDataJsonSerializer<TestData>.Default, cancellationToken: cts.Token);
            ack.EnsureSuccess();
        }

        var consumer = (NatsJSConsumer)await js.GetConsumerAsync($"{prefix}s1", $"{prefix}c1", cts.Token);
        var count = 0;
        await using var fc =
            await consumer.FetchInternalAsync<TestData>(serializer: TestDataJsonSerializer<TestData>.Default, opts: new NatsJSFetchOpts { MaxMsgs = 10 }, cancellationToken: cts.Token);
        await foreach (var msg in fc.Msgs.ReadAllAsync(cts.Token))
        {
            await msg.AckAsync(cancellationToken: cts.Token);
            Assert.Equal(count, msg.Data!.Test);
            count++;
        }

        Assert.Equal(10, count);
    }

    [Theory]
    [InlineData(NatsRequestReplyMode.Direct)]
    [InlineData(NatsRequestReplyMode.SharedInbox)]
    public async Task FetchNoWait_test(NatsRequestReplyMode mode)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url, RequestReplyMode = mode });
        var prefix = _server.GetNextId();
        var js = new NatsJSContext(nats);
        await js.CreateStreamAsync($"{prefix}s1", new[] { $"{prefix}s1.*" }, cts.Token);
        await js.CreateOrUpdateConsumerAsync($"{prefix}s1", $"{prefix}c1", cancellationToken: cts.Token);

        for (var i = 0; i < 10; i++)
        {
            var ack = await js.PublishAsync($"{prefix}s1.foo", new TestData { Test = i }, serializer: TestDataJsonSerializer<TestData>.Default, cancellationToken: cts.Token);
            ack.EnsureSuccess();
        }

        var consumer = (NatsJSConsumer)await js.GetConsumerAsync($"{prefix}s1", $"{prefix}c1", cts.Token);
        var count = 0;
        await foreach (var msg in consumer.FetchNoWaitAsync<TestData>(serializer: TestDataJsonSerializer<TestData>.Default, opts: new NatsJSFetchOpts { MaxMsgs = 10 }, cancellationToken: cts.Token))
        {
            await msg.AckAsync(cancellationToken: cts.Token);
            Assert.Equal(count, msg.Data!.Test);
            count++;
        }

        Assert.Equal(10, count);
    }

    [Theory]

    // TODO: Fix this test
    // [InlineData(NatsRequestReplyMode.Direct)]
    [InlineData(NatsRequestReplyMode.SharedInbox)]
    public async Task Fetch_dispose_test(NatsRequestReplyMode mode)
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url, RequestReplyMode = mode });
        var prefix = _server.GetNextId();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var js = new NatsJSContext(nats);
        var stream = await js.CreateStreamAsync($"{prefix}s1", new[] { $"{prefix}s1.*" }, cts.Token);
        var consumer = (NatsJSConsumer)await js.CreateOrUpdateConsumerAsync($"{prefix}s1", $"{prefix}c1", cancellationToken: cts.Token);

        var fetchOpts = new NatsJSFetchOpts
        {
            MaxMsgs = 10,
            IdleHeartbeat = TimeSpan.FromSeconds(5),
            Expires = TimeSpan.FromSeconds(10),
        };

        for (var i = 0; i < 100; i++)
        {
            var ack = await js.PublishAsync($"{prefix}s1.foo", new TestData { Test = i }, serializer: TestDataJsonSerializer<TestData>.Default, cancellationToken: cts.Token);
            ack.EnsureSuccess();
        }

        var fc = await consumer.FetchInternalAsync<TestData>(serializer: TestDataJsonSerializer<TestData>.Default, opts: fetchOpts, cancellationToken: cts.Token);

        var signal1 = new WaitSignal();
        var signal2 = new WaitSignal();
        var reader = Task.Run(async () =>
        {
            var x = 0;
            await foreach (var msg in fc.Msgs.ReadAllAsync(cts.Token))
            {
                _output.WriteLine($"rcv:{++x}");
                await msg.AckAsync(cancellationToken: cts.Token);
                signal1.Pulse();
                await signal2;
            }
        });

        await signal1;

        // Dispose waits for all the pending messages to be delivered to the loop
        // since the channel reader carries on reading the messages in its internal queue.
        await fc.DisposeAsync();

        // At this point we should only have ACKed one message
        await Retry.Until(
            "ack pending 9",
            async () =>
            {
                var c = await js.GetConsumerAsync($"{prefix}s1", $"{prefix}c1", cts.Token);
                _output.WriteLine($"pend1:{c.Info.NumAckPending}");
                return c.Info.NumAckPending == 9;
            },
            retryDelay: TimeSpan.FromSeconds(1),
            timeout: TimeSpan.FromSeconds(30));
        await consumer.RefreshAsync(cts.Token);
        Assert.Equal(9, consumer.Info.NumAckPending);

        signal2.Pulse();

        await reader;

        await Retry.Until(
            "ack pending 0",
            async () =>
            {
                var c = await js.GetConsumerAsync($"{prefix}s1", $"{prefix}c1", cts.Token);
                _output.WriteLine($"pend:{c.Info.NumAckPending}");
                return c.Info.NumAckPending == 0;
            },
            retryDelay: TimeSpan.FromSeconds(1),
            timeout: TimeSpan.FromSeconds(30));
        await consumer.RefreshAsync(cts.Token);
        Assert.Equal(0, consumer.Info.NumAckPending);
    }
}
