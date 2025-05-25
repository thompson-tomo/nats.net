namespace NATS.Client.Core;

internal enum NatsAuthType
{
    None,
    UserInfo,
    Token,
    Jwt,
    Nkey,
    [Obsolete("Use jwt or token to directly return the information rather than file path")]
    CredsFile,
    [Obsolete("Use nkey to directly return the information rather than file path")]
    NkeyFile,
}

public readonly struct NatsAuthCred
{
    private NatsAuthCred(NatsAuthType type, string value, string secret)
    {
        Type = type;
        Value = value;
        Secret = secret;
    }

    private NatsAuthCred(NatsAuthType type, string value, string secret, string seed)
    {
        Type = type;
        Value = value;
        Secret = secret;
        Seed = seed;
    }

    internal NatsAuthType Type { get; }

    internal string? Value { get; }

    internal string? Secret { get; }

    internal string? Seed { get; }

    public static NatsAuthCred FromUserInfo(string username, string password)
        => new(NatsAuthType.UserInfo, $"{username}", $"{password}");

    public static NatsAuthCred FromToken(string token) => new(NatsAuthType.Token, token, string.Empty);

    public static NatsAuthCred FromJwt(string jwt, string seed) => new(NatsAuthType.Jwt, jwt, string.Empty, seed);

    [Obsolete("The key should already be generated see, use other otherload")]
    public static NatsAuthCred FromNkey(string seed) => new(NatsAuthType.Nkey, string.Empty, seed);

    public static NatsAuthCred FromNkey(string key, string seed) => new(NatsAuthType.Nkey, key, string.Empty, seed);

    [Obsolete("Handling of auth files is being phased out and instead the processed content should be returned")]
    public static NatsAuthCred FromCredsFile(string credFile) => new(NatsAuthType.CredsFile, credFile, string.Empty);

    [Obsolete("Handling of auth files is being phased out and instead the processed content should be returned")]
    public static NatsAuthCred FromNkeyFile(string nkeyFile) => new(NatsAuthType.NkeyFile, nkeyFile, string.Empty);
}

public record NatsAuthOpts
{
    public static readonly NatsAuthOpts Default = new();

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? Token { get; init; }

    public string? Jwt { get; init; }

    public string? NKey { get; init; }

    public string? Seed { get; init; }

    public string? CredsFile { get; init; }

    public string? NKeyFile { get; init; }

    /// <summary>
    /// Callback to provide NATS authentication credentials.
    /// When specified, value of <see cref="NatsAuthCred"/> will take precedence
    /// over other authentication options. Note that, <c>default</c> value of
    /// <see cref="NatsAuthCred"/> should not be returned as the behavior is not defined.
    /// </summary>
    public Func<Uri, CancellationToken, ValueTask<NatsAuthCred>>? AuthCredCallback { get; init; }

    public bool IsAnonymous => string.IsNullOrEmpty(Username)
                               && string.IsNullOrEmpty(Password)
                               && string.IsNullOrEmpty(Token)
                               && string.IsNullOrEmpty(Jwt)
                               && string.IsNullOrEmpty(NKey)
                               && string.IsNullOrEmpty(Seed)
                               && string.IsNullOrEmpty(CredsFile)
                               && string.IsNullOrEmpty(NKeyFile)
                               && AuthCredCallback == null;
}
