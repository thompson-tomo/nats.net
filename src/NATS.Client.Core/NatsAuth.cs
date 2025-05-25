using NATS.Client.Core.Internal;

namespace NATS.Client.Core;

public class CallbackAuth(Func<Uri, CancellationToken, ValueTask<NatsAuthCred>> callback, Uri uri) : INatsAuth
{
    public Func<Uri, CancellationToken, ValueTask<NatsAuthCred>> AuthCredCallback { get; init; } = callback;

    public TimeSpan? Timeout { get; set; } = null;

    internal Uri Uri { get; set; } = uri;

    public async Task<NatsAuthCred> GetAuthCredAsync(CancellationToken cancellationToken = default)
    {
        var token = cancellationToken;

        if (Timeout != null)
        {
            using var cts = new CancellationTokenSource(Timeout.Value);
#if NETSTANDARD
            using var ctr = cancellationToken.Register(static state => ((CancellationTokenSource)state!).Cancel(), cts);
#else
            await using var ctr = cancellationToken.UnsafeRegister(static state => ((CancellationTokenSource)state!).Cancel(), cts);
            token = cts.Token;
#endif
        }

        var authCred = await AuthCredCallback(Uri, token).ConfigureAwait(false);
        if (authCred.Type == NatsAuthType.NkeyFile && !string.IsNullOrEmpty(authCred.Value))
        {
            authCred = await new NKeyFileAuth(authCred.Value).GetAuthCredAsync(token).ConfigureAwait(false);
        }
        else if (authCred.Type == NatsAuthType.CredsFile && !string.IsNullOrEmpty(authCred.Value))
        {
            authCred = await new JwtCredFileAuth(authCred.Value).GetAuthCredAsync(token).ConfigureAwait(false);
        }

        return authCred;
    }
}

public class BasicAuth(string username, string password) : INatsAuth
{
    public string Username { get; init; } = username;

    public string Password { get; init; } = password;

    public async Task<NatsAuthCred> GetAuthCredAsync(CancellationToken cancellationToken = default)
    {
        return NatsAuthCred.FromUserInfo(Username, Password);
    }
}

public class TokenAuth(string token) : INatsAuth
{
    public string Token { get; init; } = token;

    public async Task<NatsAuthCred> GetAuthCredAsync(CancellationToken cancellationToken = default)
    {
        return NatsAuthCred.FromToken(Token);
    }
}

public class JWTAuth(string jWT, string seed) : INatsAuth
{
    public string JWT { get; init; } = jWT;

    public string Seed { get; init; } = seed;

    public async Task<NatsAuthCred> GetAuthCredAsync(CancellationToken cancellationToken = default)
    {
        return NatsAuthCred.FromJwt(JWT, Seed);
    }
}

public class NKeyAuth(string seed) : INatsAuth
{
    public string Seed { get; init; } = seed;

    public async Task<NatsAuthCred> GetAuthCredAsync(CancellationToken cancellationToken = default)
    {
        return NatsAuthCred.FromNkey(NKeys.PublicKeyFromSeed(Seed), Seed);
    }
}

public class JwtCredFileAuth(string filePath) : INatsAuth
{
    public string FilePath { get; init; } = filePath;

    public async Task<NatsAuthCred> GetAuthCredAsync(CancellationToken cancellationToken = default)
    {
        (var jwt, var seed) = LoadCredsFile(FilePath);
        return NatsAuthCred.FromJwt(jwt, seed);
    }

    private (string, string) LoadCredsFile(string path)
    {
        string? jwt = null;
        string? seed = null;
        using var reader = new StreamReader(path);
        while (reader.ReadLine()?.Trim() is { } line)
        {
            if (line.StartsWith("-----BEGIN NATS USER JWT-----"))
            {
                jwt = reader.ReadLine();
                if (jwt == null)
                    break;
            }
            else if (line.StartsWith("-----BEGIN USER NKEY SEED-----"))
            {
                seed = reader.ReadLine();
                if (seed == null)
                    break;
            }
        }

        if (jwt == null)
            throw new NatsException($"Can't find JWT while loading creds file ${path}");
        if (seed == null)
            throw new NatsException($"Can't find NKEY seed while loading creds file ${path}");

        return (jwt, seed);
    }
}

public class NKeyFileAuth(string filePath) : INatsAuth
{
    public string FilePath { get; init; } = filePath;

    public async Task<NatsAuthCred> GetAuthCredAsync(CancellationToken cancellationToken = default)
    {
        (var seed, var key) = LoadNKeyFile(FilePath);
        return NatsAuthCred.FromNkey(key, seed);
    }

    private (string, string) LoadNKeyFile(string path)
    {
        string? seed = null;
        string? nkey = null;

        using var reader = new StreamReader(path);
        while (reader.ReadLine()?.Trim() is { } line)
        {
            if (line.StartsWith("SU"))
            {
                seed = line;
            }
            else if (line.StartsWith("U"))
            {
                nkey = line;
            }
        }

        if (seed == null)
            throw new NatsException($"Can't find seed while loading NKEY file ${path}");
        if (nkey == null)
            throw new NatsException($"Can't find public key while loading NKEY file ${path}");

        return (seed, nkey);
    }
}

internal static class NatsAuthResolver
{
    [Obsolete("This Has been implemented for backwards compatability and is not to be used for any other purposes")]
    internal static INatsAuth? GetAuthFromOptions(NatsAuthOpts? authOpts, NatsUri uri)
    {
        if (authOpts?.IsAnonymous != false)
        {
            return null;
        }

        if (authOpts.AuthCredCallback is not null)
        {
            return new CallbackAuth(authOpts.AuthCredCallback, uri.Uri);
        }
        else if (authOpts.NKeyFile != null)
        {
            return new NKeyFileAuth(authOpts.NKeyFile);
        }
        else if (authOpts.CredsFile != null)
        {
            return new JwtCredFileAuth(authOpts.CredsFile);
        }
        else if (authOpts.NKey != null)
        {
            return new NKeyAuth(authOpts.NKey);
        }
        else if (authOpts.Jwt != null && authOpts.Seed != null)
        {
            return new JWTAuth(authOpts.Jwt, authOpts.Seed);
        }
        else if (authOpts.Token != null)
        {
            return new TokenAuth(authOpts.Token);
        }
        else if (authOpts.Username != null && authOpts.Password != null)
        {
            return new BasicAuth(authOpts.Username, authOpts.Password);
        }

        return null;
    }
}
