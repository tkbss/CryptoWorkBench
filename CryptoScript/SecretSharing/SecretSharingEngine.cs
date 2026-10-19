// Shamir evaluation/interpolation adapted from Bouncy Castle Java ShamirSecretSplitter.resplit
// and ShamirSplitSecret.getSecret, commit c314b9cdffa3958a0eff5344f8fdcdb0181ed830.
// Copyright (c) 2000-2026 The Legion of the Bouncy Castle Inc. https://www.bouncycastle.org
// See BouncyCastle-LICENSE.html for the retained licence.
using System.Security.Cryptography;

namespace CryptoScript.SecretSharing;

public static class SecretSharingEngine
{
    public static SecretSet Share(SecretDescriptor descriptor, ReadOnlySpan<byte> key,
        string format = "X-Y", SecretKeyMetadata? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        SecretSet.ValidateFormat(format);
        if (key.Length != descriptor.KeySizeBits / 8) throw new ArgumentException("Key length does not match descriptor.");
        var shares = new List<SecretShare>();
        byte[] buffer = new byte[key.Length];
        byte[] coefficients = new byte[checked(key.Length * (descriptor.Mechanism == SecretDescriptor.Split ? 1 : descriptor.MinParts))];
        try
        {
            key.CopyTo(coefficients);
            if (descriptor.Mechanism == SecretDescriptor.Split)
            {
                for (int x = 1; x < descriptor.Parts; x++)
                {
                    RandomNumberGenerator.Fill(buffer);
                    shares.Add(new SecretShare(x, buffer));
                    for (int b = 0; b < key.Length; b++) coefficients[b] ^= buffer[b];
                }
                shares.Add(new SecretShare(descriptor.Parts, coefficients.AsSpan(0, key.Length)));
            }
            else
            {
                RandomNumberGenerator.Fill(coefficients.AsSpan(key.Length));
                var field = new Gf256(descriptor.ReductionPolynomial!.Value);
                for (int x = 1; x <= descriptor.Parts; x++)
                {
                    for (int b = 0; b < key.Length; b++)
                    {
                        byte y = coefficients[(descriptor.MinParts - 1) * key.Length + b];
                        for (int j = descriptor.MinParts - 2; j >= 0; j--)
                            y = (byte)(field.Multiply(y, (byte)x) ^ coefficients[j * key.Length + b]);
                        buffer[b] = y;
                    }
                    shares.Add(new SecretShare(x, buffer));
                }
            }
            return new SecretSet(descriptor, shares, format, metadata);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(coefficients);
        }
    }

    public static byte[] Restore(SecretSet secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var descriptor = secret.Descriptor;
        if (secret.Shares.Count < descriptor.MinParts) throw new ArgumentException("Not enough shares to restore the key.");
        byte[] result = new byte[descriptor.KeySizeBits / 8];
        try
        {
            if (descriptor.Mechanism == SecretDescriptor.Split)
            {
                foreach (var share in secret.Shares)
                    for (int b = 0; b < result.Length; b++) result[b] ^= share.Bytes[b];
            }
            else
            {
                var field = new Gf256(descriptor.ReductionPolynomial!.Value);
                var basis = secret.Shares.Take(descriptor.MinParts).ToArray();
                // Every extra point must lie on the polynomial of degree at most k-1.
                // Accumulate differences over all bytes/points before exposing success/failure.
                int difference = 0;
                byte[] check = new byte[result.Length];
                try
                {
                    foreach (var extra in secret.Shares.Skip(descriptor.MinParts))
                    {
                        Interpolate(field, basis, (byte)extra.X, check);
                        for (int b = 0; b < check.Length; b++) difference |= check[b] ^ extra.Bytes[b];
                    }
                    if (difference != 0) throw new ArgumentException("Additional shares are inconsistent with the threshold polynomial.");
                    Interpolate(field, basis, 0, result);
                }
                finally { CryptographicOperations.ZeroMemory(check); }
            }
            return result;
        }
        catch { CryptographicOperations.ZeroMemory(result); throw; }
    }

    private static void Interpolate(Gf256 field, SecretShare[] basis, byte x, Span<byte> output)
    {
        output.Clear();
        for (int i = 0; i < basis.Length; i++)
        {
            byte weight = 1;
            for (int j = 0; j < basis.Length; j++)
                if (i != j)
                    weight = field.Multiply(weight, field.Multiply((byte)(x ^ basis[j].X),
                        field.Inverse((byte)(basis[i].X ^ basis[j].X))));
            for (int b = 0; b < output.Length; b++) output[b] ^= field.Multiply(weight, basis[i].Bytes[b]);
        }
    }
}
