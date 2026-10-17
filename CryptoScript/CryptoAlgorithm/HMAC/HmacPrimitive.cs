using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.CryptoAlgorithm.HMAC;

internal static class HmacPrimitive
{
    internal static byte[] Compute(string mechanism, byte[] key, byte[] input)
    {
        var mac = new HMac(DigestFactory.Create(mechanism));
        mac.Init(new KeyParameter(key));
        mac.BlockUpdate(input, 0, input.Length);
        byte[] result = new byte[mac.GetMacSize()];
        mac.DoFinal(result, 0);
        return result;
    }
}
