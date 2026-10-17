using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.Variables;
using System.Security.Cryptography;
using Des3Algorithm = CryptoScript.CryptoAlgorithm.DES3.DES3;

namespace CryptoScript.CryptoAlgorithm.EMV;

/// <summary>
/// Host-side binary cryptography for the TDEA Algorithm-3 profile and AES CMAC.
/// EMV Contact Book 2 v4.4: 8.1.2 (p.87), 8.2.1 (p.88), 8.2.2 (p.89),
/// A1.2.1/2 (pp.134-136). Not a complete EMV/CCD transaction implementation.
/// Callers supply an already derived AC session key and assembled transaction bytes.
/// </summary>
internal static class EmvApplicationCryptogram
{
    internal static byte[] ComputeAc(KeyAlgorithm algorithm, byte[] sessionKey, byte[] transactionData)
        => ComputeAcCore(algorithm, sessionKey, transactionData, analysisKeys: false);

    internal static byte[] ComputeAcForAnalysis(KeyAlgorithm algorithm, byte[] sessionKey, byte[] transactionData)
        => ComputeAcCore(algorithm, sessionKey, transactionData, analysisKeys: true);

    private static byte[] ComputeAcCore(KeyAlgorithm algorithm, byte[] sessionKey, byte[] transactionData, bool analysisKeys)
    {
        ArgumentNullException.ThrowIfNull(sessionKey);
        ArgumentNullException.ThrowIfNull(transactionData);
        using var inputs = new InputSnapshots(sessionKey, transactionData);
        ValidateKey(algorithm, inputs[0], analysisKeys);
        // Empty data is a technical CMAC/Retail boundary, not a valid empty EMV transaction.
        return ComputeTruncatedMac(algorithm, inputs[0], inputs[1], 8, analysisKeys);
    }

    internal static bool VerifyAc(KeyAlgorithm algorithm, byte[] sessionKey, byte[] transactionData,
        byte[] expectedCryptogram)
        => VerifyAcCore(algorithm, sessionKey, transactionData, expectedCryptogram, analysisKeys: false);

    internal static bool VerifyAcForAnalysis(KeyAlgorithm algorithm, byte[] sessionKey, byte[] transactionData,
        byte[] expectedCryptogram)
        => VerifyAcCore(algorithm, sessionKey, transactionData, expectedCryptogram, analysisKeys: true);

    private static bool VerifyAcCore(KeyAlgorithm algorithm, byte[] sessionKey, byte[] transactionData,
        byte[] expectedCryptogram, bool analysisKeys)
    {
        ArgumentNullException.ThrowIfNull(sessionKey);
        ArgumentNullException.ThrowIfNull(transactionData);
        ArgumentNullException.ThrowIfNull(expectedCryptogram);
        using var inputs = new InputSnapshots(sessionKey, transactionData, expectedCryptogram);
        ValidateKey(algorithm, inputs[0], analysisKeys);
        RequireLength(inputs[2], 8, nameof(expectedCryptogram));
        byte[]? computed = null;
        try
        {
            computed = ComputeTruncatedMac(algorithm, inputs[0], inputs[1], 8, analysisKeys);
            return CryptographicOperations.FixedTimeEquals(computed, inputs[2]);
        }
        finally
        {
            if (computed is not null) CryptographicOperations.ZeroMemory(computed);
        }
    }

    /// <remarks>
    /// This function does not verify ARQC. Book 2 p.88 footnote 35 recommends returning
    /// ARPC only after successful verification; on failure, do not base a response on
    /// the received network ARQC. The caller owns that transaction policy.
    /// </remarks>
    internal static byte[] ComputeArpcMethod1(KeyAlgorithm algorithm, byte[] sessionKey,
        byte[] arqc, byte[] arc)
        => ComputeArpcMethod1Core(algorithm, sessionKey, arqc, arc, analysisKeys: false);

    internal static byte[] ComputeArpcMethod1ForAnalysis(KeyAlgorithm algorithm, byte[] sessionKey,
        byte[] arqc, byte[] arc)
        => ComputeArpcMethod1Core(algorithm, sessionKey, arqc, arc, analysisKeys: true);

    private static byte[] ComputeArpcMethod1Core(KeyAlgorithm algorithm, byte[] sessionKey,
        byte[] arqc, byte[] arc, bool analysisKeys)
    {
        ArgumentNullException.ThrowIfNull(sessionKey);
        ArgumentNullException.ThrowIfNull(arqc);
        ArgumentNullException.ThrowIfNull(arc);
        using var inputs = new InputSnapshots(sessionKey, arqc, arc);
        ValidateKey(algorithm, inputs[0], analysisKeys);
        RequireLength(inputs[1], 8, nameof(arqc));
        RequireLength(inputs[2], 2, nameof(arc));
        byte[]? block = null;
        byte[]? encrypted = null;
        try
        {
            block = new byte[algorithm == KeyAlgorithm.Tdea ? 8 : 16];
            inputs[1].CopyTo(block, 0);
            block[0] ^= inputs[2][0];
            block[1] ^= inputs[2][1];
            // Remaining six bytes of X and trailing eight AES bytes are zero.
            encrypted = algorithm == KeyAlgorithm.Tdea
                ? (analysisKeys ? DES3_ECB.EncryptNoPaddingForAnalysis(inputs[0], block)
                    : DES3_ECB.EncryptNoPadding(inputs[0], block))
                : AES_ECB.EncryptNoPadding(inputs[0], block);
            return encrypted.AsSpan(0, 8).ToArray();
        }
        finally
        {
            if (block is not null) CryptographicOperations.ZeroMemory(block);
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    /// <remarks>
    /// Same verified-ARQC recommendation as Method 1 (Book 2 p.89 footnote 36).
    /// CSU is opaque binary profile data; no card-state or CSU-bit interpretation.
    /// Null proprietary data denotes zero bytes.
    /// </remarks>
    internal static byte[] ComputeArpcMethod2(KeyAlgorithm algorithm, byte[] sessionKey,
        byte[] arqc, byte[] csu, byte[]? proprietaryData)
        => ComputeArpcMethod2Core(algorithm, sessionKey, arqc, csu, proprietaryData, analysisKeys: false);

    internal static byte[] ComputeArpcMethod2ForAnalysis(KeyAlgorithm algorithm, byte[] sessionKey,
        byte[] arqc, byte[] csu, byte[]? proprietaryData)
        => ComputeArpcMethod2Core(algorithm, sessionKey, arqc, csu, proprietaryData, analysisKeys: true);

    private static byte[] ComputeArpcMethod2Core(KeyAlgorithm algorithm, byte[] sessionKey,
        byte[] arqc, byte[] csu, byte[]? proprietaryData, bool analysisKeys)
    {
        ArgumentNullException.ThrowIfNull(sessionKey);
        ArgumentNullException.ThrowIfNull(arqc);
        ArgumentNullException.ThrowIfNull(csu);
        using var inputs = new InputSnapshots(sessionKey, arqc, csu, proprietaryData ?? []);
        ValidateKey(algorithm, inputs[0], analysisKeys);
        RequireLength(inputs[1], 8, nameof(arqc));
        RequireLength(inputs[2], 4, nameof(csu));
        ValidateProprietaryData(inputs[3]);
        byte[]? message = null;
        try
        {
            message = Concatenate(inputs[1], inputs[2], inputs[3]);
            return ComputeTruncatedMac(algorithm, inputs[0], message, 4, analysisKeys);
        }
        finally
        {
            if (message is not null) CryptographicOperations.ZeroMemory(message);
        }
    }

    // Returns the value of tag 91, not BER-TLV, and performs no cryptography.
    internal static byte[] BuildIssuerAuthenticationData(byte[] arpc, byte[] csu, byte[]? proprietaryData)
    {
        ArgumentNullException.ThrowIfNull(arpc);
        ArgumentNullException.ThrowIfNull(csu);
        using var inputs = new InputSnapshots(arpc, csu, proprietaryData ?? []);
        RequireLength(inputs[0], 4, nameof(arpc));
        RequireLength(inputs[1], 4, nameof(csu));
        ValidateProprietaryData(inputs[2]);
        return Concatenate(inputs[0], inputs[1], inputs[2]);
    }

    private static byte[] ComputeTruncatedMac(KeyAlgorithm algorithm, byte[] key, byte[] data, int length, bool analysisKeys)
    {
        byte[]? fullMac = null;
        try
        {
            fullMac = algorithm == KeyAlgorithm.Tdea
                ? (analysisKeys ? DES3_RETAIL.ComputeMacForAnalysis(key, data, "ISO-9797-M2", 8)
                    : DES3_RETAIL.ComputeMac(key, data, "ISO-9797-M2", 8))
                : AES_CMAC.ComputeMac(key, data);
            return fullMac.AsSpan(0, length).ToArray();
        }
        finally
        {
            if (fullMac is not null) CryptographicOperations.ZeroMemory(fullMac);
        }
    }

    private static void ValidateKey(KeyAlgorithm algorithm, byte[] key, bool analysisKeys)
    {
        switch (algorithm)
        {
            case KeyAlgorithm.Tdea:
                if (key.Length != 16)
                    throw new ArgumentException("EMV TDEA session key must contain exactly 16 bytes.", "sessionKey");
                Des3Algorithm.ValidateKeyLength(key);
                if (!analysisKeys) Des3Algorithm.ValidateUsableKey(key);
                break;
            case KeyAlgorithm.Aes:
                AES_ECB.ValidateKeyLength(key);
                break;
            default:
                throw new ArgumentException("EMV cryptograms support only TDEA and AES session keys.", nameof(algorithm));
        }
    }

    private static void RequireLength(byte[] value, int length, string parameterName)
    {
        if (value.Length != length)
            throw new ArgumentException($"EMV input must contain exactly {length} bytes.", parameterName);
    }

    private static void ValidateProprietaryData(byte[] value)
    {
        if (value.Length > 8)
            throw new ArgumentException("EMV proprietary authentication data must contain 0 to 8 bytes.", "proprietaryData");
    }

    private static byte[] Concatenate(byte[] first, byte[] second, byte[] third)
    {
        byte[] result = new byte[first.Length + second.Length + third.Length];
        bool returned = false;
        try
        {
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            third.CopyTo(result, first.Length + second.Length);
            returned = true;
            return result;
        }
        finally
        {
            if (!returned) CryptographicOperations.ZeroMemory(result);
        }
    }

    // One snapshot per input at the EMV boundary. Adapters additionally own their
    // independent boundary snapshots. No original array is read after cloning.
    // Array slots are allocated before cloning: failures clear all prior copies.
    private sealed class InputSnapshots : IDisposable
    {
        private readonly byte[][] buffers;

        internal InputSnapshots(params byte[][] inputs)
        {
            buffers = new byte[inputs.Length][];
            try
            {
                for (int i = 0; i < inputs.Length; i++)
                    buffers[i] = (byte[])inputs[i].Clone();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal byte[] this[int index] => buffers[index];

        public void Dispose()
        {
            foreach (byte[]? buffer in buffers)
                if (buffer is not null) CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
