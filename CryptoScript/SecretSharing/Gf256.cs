// Adapted from Bouncy Castle Java Polynomial.java, c314b9cdffa3958a0eff5344f8fdcdb0181ed830.
// Copyright (c) 2000-2026 The Legion of the Bouncy Castle Inc. https://www.bouncycastle.org
// Distributed under the Bouncy Castle licence; see BouncyCastle-LICENSE.html in this directory.
namespace CryptoScript.SecretSharing;

internal sealed class Gf256
{
    private readonly int polynomial;
    internal Gf256(int polynomial)
    {
        if (polynomial is not (0x11B or 0x11D)) throw new ArgumentException("Unsupported reduction polynomial.");
        this.polynomial = polynomial;
    }
    internal static byte Add(byte x, byte y) => (byte)(x ^ y);
    internal byte Multiply(byte first, byte second)
    {
        int x = first, y = second, result = 0;
        for (int i = 0; i < 8; i++)
        {
            result ^= x & -(y & 1);
            y >>= 1;
            int carry = (x >> 7) & 1;
            x = ((x << 1) ^ (polynomial & -carry)) & 255;
        }
        return (byte)result;
    }
    // Only public coordinate differences reach this method in production.
    internal byte Inverse(byte value)
    {
        if (value == 0) throw new ArgumentException("Zero has no multiplicative inverse.");
        byte result = 1, power = value;
        for (int i = 0; i < 8; i++)
        {
            if ((254 & (1 << i)) != 0) result = Multiply(result, power);
            power = Multiply(power, power);
        }
        return result;
    }
}
