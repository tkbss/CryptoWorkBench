using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;
namespace CryptoScript.CryptoAlgorithm
{
    public class AES_CMAC : EncryptionMode
    {
        public override StringVariableDeclaration ModeMac(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            byte[]? keyBytes = null;
            byte[]? input = null;
            byte[]? result = null;
            try
            {
                keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
                AES.AES_ECB.ValidateKeyLength(keyBytes);
                input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
                result = ComputeMac(keyBytes, input);
                string value = FormatConversions.ByteArrayToHexString(result);
                return new StringVariableDeclaration
                {
                    Value = value,
                    ValueFormat = FormatConversions.ParseString(value),
                    Type = new CryptoTypeVar()
                };
            }
            finally
            {
                if (keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
                if (input is not null) CryptographicOperations.ZeroMemory(input);
                if (result is not null) CryptographicOperations.ZeroMemory(result);
            }
        }

        // Caller retains its arrays; this boundary owns and clears its snapshots.
        // Bouncy Castle is not IDisposable; Reset does not promise key-schedule erasure.
        internal static byte[] ComputeMac(byte[] key, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(data);
            byte[]? keySnapshot = null;
            byte[]? dataSnapshot = null;
            byte[]? result = null;
            IMac? mac = null;
            bool returned = false;
            try
            {
                keySnapshot = (byte[])key.Clone();
                dataSnapshot = (byte[])data.Clone();
                AES.AES_ECB.ValidateKeyLength(keySnapshot);
                mac = new CMac(new AesEngine());
                mac.Init(new KeyParameter(keySnapshot));
                mac.BlockUpdate(dataSnapshot, 0, dataSnapshot.Length);
                result = new byte[mac.GetMacSize()];
                mac.DoFinal(result, 0);
                mac.Reset();
                returned = true;
                return result;
            }
            finally
            {
                if (keySnapshot is not null) CryptographicOperations.ZeroMemory(keySnapshot);
                if (dataSnapshot is not null) CryptographicOperations.ZeroMemory(dataSnapshot);
                if (!returned && result is not null) CryptographicOperations.ZeroMemory(result);
            }
        }
    }
}
