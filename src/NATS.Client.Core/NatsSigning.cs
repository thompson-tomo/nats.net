using System.Text;
using NATS.Client.Core.NaCl;

namespace NATS.Client.Core;

public class NKeySigning : INatsSigning
{
    public string? Sign(string? nonce, string? seed = null)
    {
        if (seed == null || nonce == null)
            return null;

        using var kp = NKeys.FromSeed(seed);
        var bytes = kp.Sign(Encoding.ASCII.GetBytes(nonce));
        var sig = CryptoBytes.ToBase64String(bytes);

        return sig;
    }
}
