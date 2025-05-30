using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NATS.Client.Core2.Tests;
using NATS.Client.Core2.Tests.ExtraUtils.FrameworkPolyfillExtensions;

namespace NATS.Client.Core.Tests;

[Collection("nats-server")]
public class RequestReplyTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public RequestReplyTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task Simple_request_reply_test()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        const string subject = "foo";
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = cts.Token;

        var sub = await nats.SubscribeCoreAsync<int>(subject, cancellationToken: cancellationToken);
        var reg = sub.Register(async msg =>
        {
            await msg.ReplyAsync(msg.Data * 2, cancellationToken: cancellationToken);
        });

        var natsSubOpts = new NatsSubOpts { Timeout = TimeSpan.FromSeconds(10) };

        for (var i = 0; i < 10; i++)
        {
            var rep = await nats.RequestAsync<int, int>(subject, i, replyOpts: natsSubOpts, cancellationToken: cancellationToken);

            Assert.Equal(i * 2, rep.Data);
        }

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Request_reply_command_timeout_test()
    {
        // Request timeout as default timeout
        {
            await using var nats = new NatsConnection(new NatsOpts
            {
                Url = _server.Url,
                RequestTimeout = TimeSpan.FromSeconds(1),
            });

            var sub = await nats.SubscribeCoreAsync<int>("foo");
            var reg = sub.Register(async msg =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            });
            await nats.PingAsync();

            await Assert.ThrowsAsync<NatsNoReplyException>(async () =>
                await nats.RequestAsync<int, int>("foo", 0));

            await sub.DisposeAsync();
            await reg;
        }

        // Cancellation token usage
        {
            await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var sub = await nats.SubscribeCoreAsync<int>("foo");
            var reg = sub.Register(async msg =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            });
            await nats.PingAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await nats.RequestAsync<int, int>("foo", 0, cancellationToken: cts.Token);
            });

            await sub.DisposeAsync();
            await reg;
        }
    }

    [Fact]
    public async Task Request_reply_no_responders_test()
    {
        // Enable no responders, and do not set a timeout. We should get a response with a 503-header code.
        {
            await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
            await Assert.ThrowsAsync<NatsNoRespondersException>(async () => await nats.RequestAsync<int, int>(Guid.NewGuid().ToString(), 0));
        }
    }

    [Fact]
    public async Task Request_reply_many_test()
    {
        const int msgs = 10;

        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        var sub = await nats.SubscribeCoreAsync<int>("foo");
        var reg = sub.Register(async msg =>
        {
            for (var i = 0; i < msgs; i++)
            {
                await msg.ReplyAsync(msg.Data * i);
            }

            await msg.ReplyAsync<int?>(null); // stop iteration with a sentinel
        });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        const int data = 2;
        var results = Enumerable.Range(0, msgs).Select(x => x * data).ToArray();
        var count = 0;
        await foreach (var msg in nats.RequestManyAsync<int, int?>("foo", data, cancellationToken: cts.Token))
        {
            Assert.Equal(results[count++], msg.Data);
        }

        Assert.Equal(results.Length, count);

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Request_reply_many_test_overall_timeout()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        var sub = await nats.SubscribeCoreAsync<int>("foo");
        var reg = sub.Register(async msg =>
        {
            await msg.ReplyAsync(msg.Data * 2);
            await msg.ReplyAsync(msg.Data * 3);
        });

        var results = new[] { 8, 12 };
        var count = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var opts = new NatsSubOpts { Timeout = TimeSpan.FromSeconds(4) };
        await using var rep =
            await nats.CreateRequestSubAsync<int, int>("foo", 4, replyOpts: opts, cancellationToken: cts.Token);
        await foreach (var msg in rep.Msgs.ReadAllAsync(cts.Token))
        {
            Assert.Equal(results[count++], msg.Data);
        }

        Assert.Equal(2, count);
        Assert.Equal(NatsSubEndReason.Timeout, ((NatsSubBase)rep).EndReason);

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Request_reply_many_test_idle_timeout()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        var sub = await nats.SubscribeCoreAsync<int>("foo");
        var reg = sub.Register(async msg =>
        {
            await msg.ReplyAsync(msg.Data * 2);
            await Task.Delay(100);
            await msg.ReplyAsync(msg.Data * 3);
            await Task.Delay(5_000);
            await msg.ReplyAsync(msg.Data * 4);
        });

        var results = new[] { 6, 9 };
        var count = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var opts = new NatsSubOpts { IdleTimeout = TimeSpan.FromSeconds(3) };
        await using var rep =
            await nats.CreateRequestSubAsync<int, int>("foo", 3, replyOpts: opts, cancellationToken: cts.Token);
        await foreach (var msg in rep.Msgs.ReadAllAsync(cts.Token))
        {
            Assert.Equal(results[count++], msg.Data);
        }

        Assert.Equal(2, count);
        Assert.Equal(NatsSubEndReason.IdleTimeout, ((NatsSubBase)rep).EndReason);

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Request_reply_many_test_start_up_timeout()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        var sub = await nats.SubscribeCoreAsync<int>("foo");
        var reg = sub.Register(async msg =>
        {
            await Task.Delay(2_000);
            await msg.ReplyAsync(msg.Data * 2);
        });

        var count = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var opts = new NatsSubOpts { StartUpTimeout = TimeSpan.FromSeconds(1) };
        await using var rep =
            await nats.CreateRequestSubAsync<int, int>("foo", 2, replyOpts: opts, cancellationToken: cts.Token);
        await foreach (var msg in rep.Msgs.ReadAllAsync(cts.Token))
        {
            count++;
        }

        Assert.Equal(0, count);
        Assert.Equal(NatsSubEndReason.StartUpTimeout, ((NatsSubBase)rep).EndReason);

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Request_reply_many_test_max_count()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        var sub = await nats.SubscribeCoreAsync<int>("foo");
        var reg = sub.Register(async msg =>
        {
            await msg.ReplyAsync(msg.Data * 2);
            await msg.ReplyAsync(msg.Data * 3);
            await msg.ReplyAsync(msg.Data * 4);
            await msg.ReplyAsync<int?>(null); // sentinel
        });

        var results = new[] { 2, 3, 4 };
        var count = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var opts = new NatsSubOpts { MaxMsgs = 2 };
        await using var rep =
            await nats.CreateRequestSubAsync<int, int>("foo", 1, replyOpts: opts, cancellationToken: cts.Token);
        await foreach (var msg in rep.Msgs.ReadAllAsync(cts.Token))
        {
            Assert.Equal(results[count++], msg.Data);
        }

        Assert.Equal(2, count);
        Assert.Equal(NatsSubEndReason.MaxMsgs, ((NatsSubBase)rep).EndReason);

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Request_reply_many_test_sentinel()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        var sub = await nats.SubscribeCoreAsync<int>("foo");
        var reg = sub.Register(async msg =>
        {
            await msg.ReplyAsync(msg.Data * 2);
            await msg.ReplyAsync(msg.Data * 3);
            await msg.ReplyAsync(msg.Data * 4);
            await msg.ReplyAsync<int?>(null); // sentinel
        });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var results = new[] { 2, 3, 4 };
        var count = 0;
        await foreach (var msg in nats.RequestManyAsync<int, int?>("foo", 1, cancellationToken: cts.Token))
        {
            Assert.Equal(results[count++], msg.Data);
        }

        Assert.Equal(3, count);

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Request_reply_binary_test()
    {
        static string ToStr(ReadOnlyMemory<byte> input)
        {
            return Encoding.ASCII.GetString(input.Span.ToArray());
        }

        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var sub = await nats.SubscribeCoreAsync<string>("foo", cancellationToken: cts.Token);
        var reg = sub.Register(async m =>
        {
            if (m.Data == "1")
            {
                await m.ReplyAsync("qw", cancellationToken: cts.Token);
                await m.ReplyAsync("er", cancellationToken: cts.Token);
                await m.ReplyAsync("ty", cancellationToken: cts.Token);
                await m.ReplyAsync(default(string), cancellationToken: cts.Token); // sentinel
            }
        });

        var writer = new NatsBufferWriter<byte>();
        await foreach (var msg in nats.RequestManyAsync<string, string>("foo", "1", cancellationToken: cts.Token))
        {
            writer.Write(Encoding.UTF8.GetBytes(msg.Data!));
        }

        var buffer = ToStr(writer.WrittenMemory);
        Assert.Equal("qwerty", buffer);

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Request_reply_many_multiple_with_timeout_test()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        // connect to avoid race to subscribe and publish
        await nats.ConnectAsync();

        const string subject = "foo";
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var cancellationToken = cts.Token;

        var sub = await nats.SubscribeCoreAsync<int>(subject, cancellationToken: cancellationToken);
        var reg = sub.Register(async msg =>
        {
            await msg.ReplyAsync(msg.Data * 2, cancellationToken: cancellationToken);
        });

        var opts = new NatsSubOpts { Timeout = TimeSpan.FromSeconds(4) };

        // Make sure timeout isn't affecting the real inbox subscription
        // by waiting double the timeout period (by calling RequestMany twice)
        // which should be enough
        for (var i = 1; i <= 2; i++)
        {
            var data = -1;
            await foreach (var msg in nats.RequestManyAsync<int, int>(subject, i * 100, replyOpts: opts, cancellationToken: cancellationToken))
            {
                data = msg.Data;
            }

            Assert.Equal(i * 200, data);
        }

        // Run a bunch more RequestMany calls with timeout for good measure
        List<Task<(int index, int data)>> tasks = new();

        for (var i = 0; i < 10; i++)
        {
            var index = i;

            tasks.Add(Task.Run(
                async () =>
                {
                    var data = -1;

                    await foreach (var msg in nats.RequestManyAsync<int, int>(subject, index, replyOpts: opts, cancellationToken: cancellationToken))
                    {
                        data = msg.Data;
                    }

                    return (index, data);
                },
                cancellationToken));
        }

        foreach (var task in tasks)
        {
            var (index, data) = await task;
            Assert.Equal(index * 2, data);
        }

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Simple_empty_request_reply_test()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });

        const string subject = "foo";
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = cts.Token;

        var sub = await nats.SubscribeCoreAsync<int>(subject, cancellationToken: cancellationToken);
        var reg = sub.Register(async msg =>
        {
            await msg.ReplyAsync(42, cancellationToken: cancellationToken);
        });

        var reply1 = await nats.RequestAsync<object, int>(subject, null, cancellationToken: cancellationToken);
        var reply2 = await nats.RequestAsync<int>(subject, cancellationToken: cancellationToken);

        Assert.Equal(42, reply1.Data);
        Assert.Equal(42, reply2.Data);

        await sub.DisposeAsync();
        await reg;
    }

    [Fact]
    public async Task Direct_request_reply_test()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url, RequestReplyMode = NatsRequestReplyMode.Direct });
        var reply = await nats.RequestAsync<string>("$JS.API.INFO", cancellationToken: default);

        // reply-to should be inbox with id
        // e.g. _INBOX.Hu5HPpWesrJhvQq2NG3YJ6.1
        reply.Subject.Length.Should().BeLessThan("_INBOX..".Length + (2 * 22));
        Assert.True(long.TryParse(reply.Subject.Split('.')[2], out var id));
        Assert.True(id > 0);

        // simple response check
        var json = JsonNode.Parse(reply.Data!)!;
        var type = json["type"]!.GetValue<string>();
        Assert.Equal("io.nats.jetstream.api.v1.account_info_response", type);
    }

    [Fact]
    public async Task Default_SharedInbox_request_reply_test()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var reply = await nats.RequestAsync<string>("$JS.API.INFO", cancellationToken: default);

        // reply-to should be inbox
        // e.g. _INBOX.Hu5HPpWesrJhvQq2NG3YJ6.Hu5HPpWesrJhvQq2NG3YLw
        Assert.Equal("_INBOX..".Length + (2 * 22), reply.Subject.Length);

        // simple response check
        var json = JsonNode.Parse(reply.Data!)!;
        var type = json["type"]!.GetValue<string>();
        Assert.Equal("io.nats.jetstream.api.v1.account_info_response", type);
    }
}
