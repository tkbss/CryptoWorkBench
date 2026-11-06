using CryptoScript.Variables;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Modes;
using CryptoScript.Model;
using Microsoft.VisualBasic;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS
{
    public class WrapAESTR31 : CryptoAlgorithm
    {
        /// <summary>
        /// Unwraps a TR-31 block, returning the unwrapped key as a KeyVariableDeclaration.
        /// </summary>
        /// <param name="parameters">
        /// parameters[0]: name/id for a ParameterVariableDeclaration
        /// parameters[1]: the name of the "protection key" variable
        /// parameters[2]: the name of the string variable containing the TR-31 block
        /// </param>
        /// <returns>The unwrapped key in a KeyVariableDeclaration</returns>
        public override KeyVariableDeclaration Unwrap(string[] parameters) =>
            Unwrap(AlgorithmCallArguments.FromValues(parameters));

        public override KeyVariableDeclaration Unwrap(AlgorithmCallArguments parameters)
        {
            // 1) Set up ParameterVariableDeclaration from parameters[0], as in Wrap
            ParameterVariableDeclaration p =
                AlgorithmArgumentResolver.ResolveParameter(parameters.Arguments[0]);

            KeyVariableDeclaration keyProtectionKey = AlgorithmArgumentResolver.ResolveTr31Key(parameters.Arguments[1]);
            KeyMetadataValidator.Validate(keyProtectionKey, KeyAlgorithm.Aes, KeyUsage.UnwrapKey,
                "Unwrap", "WRAP-AES-TR31", parameters.DiagnosticSink);
            string wrappedValue = parameters.Arguments[2].SourceVariable is StringVariableDeclaration source
                ? source.Value : parameters.Arguments[2].Value!;
            if (VariableDictionary.Instance().Get(wrappedValue) is StringVariableDeclaration declared)
                wrappedValue = declared.Value;
            
            byte[]? blockHeaderBytes =null;
            byte[]? encryptedKeyData = null;
            byte[]? blockMac = null;
            TR31Block? block =null;
            int wireLength = 0;
            // 3) Parse the TR-31 block into (header, wrappedKey, mac).
            //    This depends on how your TR31String is implemented.
            //    Example assumes a constructor that can parse a string:
            if (FormatConversions.ParseString(wrappedValue) == FormatConversions.TR31)
            {
                TR31String tr31 = TR31String.FromString(wrappedValue, RequireVersionD);
                RequireVersionD(tr31.Block);
                wireLength = checked(tr31.Block.Length + 2 * (tr31.Cryptogram.Length + tr31.Mac.Length));
                block = TR31Block.FromString(tr31.Block + Convert.ToHexString(tr31.Cryptogram) + Convert.ToHexString(tr31.Mac));
                blockHeaderBytes = FormatConversions.StringToByteArray(tr31.Block);
                block.Cryptogram=encryptedKeyData = tr31.Cryptogram;
                block.Mac=blockMac = tr31.Mac;
            }
            if (FormatConversions.ParseString(wrappedValue) == FormatConversions.STR)
            {
                string wire = TR31String.ReadWireString(wrappedValue, RequireVersionD);
                wireLength = wire.Length;
                RequireVersionD(wire);
                block =TR31Block.FromString(wire);
                blockHeaderBytes = block.HeaderDataToMac;
                encryptedKeyData = block.Cryptogram;
                blockMac = block.Mac;
            }
            // 4) Derive the encryption and MAC keys from the protection key
            byte[] masterKeyBytes = FormatConversions.HexStringToByteArray(keyProtectionKey.Value);
            int protKeySizeBits = Convert.ToInt32(keyProtectionKey.KeySize);

            byte[] macKey = DeriveKey(masterKeyBytes, MACDerivationData(protKeySizeBits), protKeySizeBits);
            byte[] encKey = DeriveKey(masterKeyBytes, EncryptionDerivationData(protKeySizeBits), protKeySizeBits);

            // 5) Decrypt using AES/CBC/no-padding with the derived encKey
            //    and the TR-31 MAC as IV. That recovers the raw key data block.
            byte[] recoveredKeyData = DecryptNoPadding(encKey, blockMac, encryptedKeyData);

            // 6) Verify the MAC: reconstruct dataToMAC = blockHeader + recoveredKeyData,
            //    compute MAC with macKey, and compare to blockMac.
            byte[] dataToMAC = new byte[blockHeaderBytes.Length + recoveredKeyData.Length];
            Array.Copy(blockHeaderBytes, 0, dataToMAC, 0, blockHeaderBytes.Length);
            Array.Copy(recoveredKeyData, 0, dataToMAC, blockHeaderBytes.Length, recoveredKeyData.Length);

            byte[] recomputedMac = ComputeMAC(dataToMAC, macKey);

            // Compare
            if (!AreEqual(recomputedMac, blockMac))
            {
                throw new Exception("TR-31 block MAC verification failed.");
            }
            Tr31HeaderBinding.CompareAfterAuthentication(p, Encoding.ASCII.GetString(blockHeaderBytes), wireLength);

            // Interpret the complete authenticated bit length through the shared strict extractor.
            byte[] recoveredKey = Tr31ConfidentialData.ExtractKey(recoveredKeyData);
            Tr31ConfidentialData.ValidateAlgorithmKeyLength(recoveredKey, block.Header![7]);
            string actualKeyHex = FormatConversions.ByteArrayToHexString(recoveredKey);
            int actualBitSize = checked(recoveredKey.Length * 8);

            // 8) Build a KeyVariableDeclaration to hold the result
            KeyVariableDeclaration unwrappedKey = new KeyVariableDeclaration()
            {
                KeySize = actualBitSize.ToString(),  // or store the bits as needed
                KeyType = KeyTypeFromHeader(block.Header),
                Value = actualKeyHex, // pick some name or pass in an extra param
                KeyAttributes=block.HeaderOptionalBlocks(),
                Type =new CryptoTypeKey()
            };

            // Optionally store it in the dictionary if you want it globally accessible:
            // VariableDictionary.Instance().AddOrReplace(unwrappedKey);

            // 9) Return the KeyVariableDeclaration
            return unwrappedKey;
        }

        /// <summary>
        /// Decrypt with AES/CBC/NoPadding. Mirrors EncryptNoPadding but for decryption.
        /// </summary>
        internal static byte[] DecryptNoPadding(byte[] key, byte[] iv, byte[] ciphertext)
        {
            var cipher = new BufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
            cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));
            byte[] outputBuffer = new byte[cipher.GetOutputSize(ciphertext.Length)];
            int outputLength = cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, outputBuffer, 0);
            cipher.DoFinal(outputBuffer, outputLength);
            return outputBuffer;
        }

        /// <summary>
        /// Compare two byte arrays in constant time. 
        /// </summary>
        private bool AreEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= (a[i] ^ b[i]);
            }
            return diff == 0;
        }

        public override StringVariableDeclaration Wrap(string[] parameters) =>
            Wrap(AlgorithmCallArguments.FromValues(parameters));

        public override StringVariableDeclaration Wrap(AlgorithmCallArguments parameters)
        {
            ParameterVariableDeclaration p =
                AlgorithmArgumentResolver.ResolveParameter(parameters.Arguments[0]);
            var definition = Tr31HeaderBinding.Definition(p);
            string header = definition is null ? FormatConversions.ToString(p.GetParameter("#BLKH")) : string.Empty;
            RequireVersionD(definition?.Version ?? header);
            KeyAlgorithm transportedAlgorithm = definition is null ? ResolveWrapAlgorithm(header) :
                definition.Algorithm == "A" ? KeyAlgorithm.Aes : KeyAlgorithm.Tdea;
            string rnd = p.GetParameter("#RND");
            KeyVariableDeclaration keyProtectionKey = AlgorithmArgumentResolver.ResolveTr31Key(parameters.Arguments[1]);
            KeyVariableDeclaration keyToWrap = AlgorithmArgumentResolver.ResolveTr31Key(parameters.Arguments[2]);
            KeyMetadataValidator.Validate(keyProtectionKey, KeyAlgorithm.Aes, KeyUsage.WrapKey,
                "Wrap", "WRAP-AES-TR31", parameters.DiagnosticSink);
            byte[] transportedBytes = DecodeTransportedKey(keyToWrap, transportedAlgorithm);
            // The same physical KEY may occupy both roles. An identical AES type
            // check has already been reported for the KBPK; transport adds no usage.
            if (!ReferenceEquals(keyProtectionKey, keyToWrap) || transportedAlgorithm != KeyAlgorithm.Aes)
                KeyMetadataValidator.Validate(keyToWrap, transportedAlgorithm, KeyUsage.None,
                    "Wrap", "WRAP-AES-TR31", parameters.DiagnosticSink);
            byte[] macKey=DeriveKey(FormatConversions.HexStringToByteArray(keyProtectionKey.Value),
                MACDerivationData(Convert.ToInt32(keyProtectionKey.KeySize)),
                Convert.ToInt32(keyProtectionKey.KeySize));
            byte[] encryptionKey = DeriveKey(FormatConversions.HexStringToByteArray(keyProtectionKey.Value),
                EncryptionDerivationData(Convert.ToInt32(keyProtectionKey.KeySize)),
                Convert.ToInt32(keyProtectionKey.KeySize));
            byte[]? random = string.IsNullOrEmpty(rnd) ? null : FormatConversions.HexStringToByteArray(rnd);
            int targetLength = transportedAlgorithm == KeyAlgorithm.Aes ? 32 : 24;
            byte[] keyData = Tr31ConfidentialData.Create(transportedBytes, blockSize: 16,
                obfuscationPaddingLength: targetLength - transportedBytes.Length, random: random).ToArray();
            header = Tr31HeaderBinding.Resolve(p, keyData.Length);
            byte[] dataToMAC = ConstructDataToMAC(header, keyData);
            byte[] macResult = ComputeMAC(dataToMAC, macKey);
            byte[] wrappedKey = EncryptNoPadding(encryptionKey, macResult, keyData);
            ValidateDeclaredBlockLength(header, wrappedKey.Length, macResult.Length);
            TR31String tr31 = new TR31String(header, wrappedKey, macResult);            
            StringVariableDeclaration block = new StringVariableDeclaration() { Value = tr31.ToString(), ValueFormat = FormatConversions.TR31 };
            return block;
        }        
        private static void RequireVersionD(string header)
        {
            if (string.IsNullOrEmpty(header) || header[0] != 'D')
                throw new NotSupportedException("WRAP-AES-TR31 supports only version D.");
        }

        internal static byte[] ComputeMAC(byte[] dataToMAC, byte[] key)
        {            
            IMac mac = new CMac(new AesEngine());
            mac.Init(new KeyParameter(key));
            byte[] result = new byte[mac.GetMacSize()];            
            mac.BlockUpdate(dataToMAC, 0, dataToMAC.Length);
            mac.DoFinal(result, 0);
            return result;
        }
        internal static byte[] EncryptNoPadding(byte[] key, byte[] iv, byte[] plaintext)
        {           

            // Create a BufferedBlockCipher with AES/CBC **without** a padding layer
            var cipher = new BufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
            // Process encryption
            byte[] outputBuffer = new byte[cipher.GetOutputSize(plaintext.Length)];
            int outputLength = cipher.ProcessBytes(plaintext, 0, plaintext.Length, outputBuffer, 0);
            // Finalize (should not add padding, so typically returns 0 additional bytes)
            cipher.DoFinal(outputBuffer, outputLength);            
            return outputBuffer;
        }
        private byte[] ConstructDataToMAC(string header, byte[]keyData)
        { 
            byte[] byteArray=Encoding.UTF8.GetBytes(header);
            byte[] dataToMAC = new byte[byteArray.Length + keyData.Length];
            Array.Copy(byteArray, 0, dataToMAC, 0, byteArray.Length);
            Array.Copy(keyData, 0, dataToMAC, byteArray.Length, keyData.Length);
            return dataToMAC;
        }
        private static byte[] DecodeTransportedKey(KeyVariableDeclaration key, KeyAlgorithm algorithm)
        {
            string hex = key.Value;
            if (hex.StartsWith("0x(", StringComparison.Ordinal))
            {
                if (!hex.EndsWith(')'))
                    throw new ArgumentException("TR-31 transported KEY requires complete hexadecimal key material.");
                hex = hex[3..^1];
            }
            if (hex.Length % 2 != 0 || hex.AsSpan().IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
                throw new ArgumentException("TR-31 transported KEY requires complete hexadecimal key material.");
            byte[] bytes = Convert.FromHexString(hex);
            bool valid = algorithm == KeyAlgorithm.Aes ? bytes.Length is 16 or 24 or 32 : bytes.Length is 16 or 24;
            if (!valid)
                throw new ArgumentException($"TR-31 header algorithm {algorithm} does not support {bytes.Length} key bytes.");
            if (key.KeySizeInBits.IsKnown && key.KeySizeInBits.Bits != (ulong)bytes.Length * 8)
                throw new ArgumentException("TR-31 transported KEY size metadata conflicts with its actual key material.");
            return bytes;
        }
        // Wrap-only structural validation. Unwrap keeps its existing authenticated
        // legacy reader; neither stored HDR attributes nor declared KeyType select this profile.
        private static KeyAlgorithm ResolveWrapAlgorithm(string header)
        {
            if (header.Length < 16 || header.Any(c => c < ' ' || c > '~') ||
                header.AsSpan(1, 4).IndexOfAnyExcept("0123456789") >= 0 ||
                header.AsSpan(12, 2).IndexOfAnyExcept("0123456789") >= 0)
                throw new ArgumentException("TR-31 wrap requires a complete ASCII header with decimal length and optional-block count.");
            int count = int.Parse(header.AsSpan(12, 2), System.Globalization.CultureInfo.InvariantCulture);
            int position = 16;
            for (int i = 0; i < count; i++)
            {
                if (position + 4 > header.Length ||
                    header.AsSpan(position + 2, 2).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0 ||
                    !int.TryParse(header.AsSpan(position + 2, 2), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out int length))
                    throw new ArgumentException("TR-31 optional block has an invalid length.");
                int prefixLength = 4;
                if (length == 0)
                {
                    // Preserve the reader's existing two-byte extended-length representation.
                    prefixLength = 10;
                    if (position + prefixLength > header.Length || header.AsSpan(position + 4, 2).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0 ||
                        header.AsSpan(position + 6, 4).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0 ||
                        !int.TryParse(header.AsSpan(position + 6, 4), System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out length))
                        throw new ArgumentException("TR-31 optional block has an invalid extended length.");
                }
                if (length < prefixLength || length > header.Length - position)
                    throw new ArgumentException("TR-31 optional block exceeds the supplied header.");
                position += length;
            }
            if (position != header.Length)
                throw new ArgumentException("TR-31 optional-block count does not match the supplied header.");
            return header[7] switch
            {
                'A' => KeyAlgorithm.Aes,
                'T' => KeyAlgorithm.Tdea,
                _ => throw new NotSupportedException("WRAP-AES-TR31 supports transported header algorithms A and T only.")
            };
        }
        private static KeyType KeyTypeFromHeader(string? header)
        {
            if (string.IsNullOrEmpty(header) || header.Length <= 7)
                return KeyType.Secret(KeyAlgorithm.Unknown);

            return char.ToUpperInvariant(header[7]) switch
            {
                'A' => KeyType.Secret(KeyAlgorithm.Aes),
                'T' => KeyType.Secret(KeyAlgorithm.Tdea),
                'H' => KeyType.Secret(KeyAlgorithm.Hmac),
                _ => KeyType.Secret(KeyAlgorithm.Unknown)
            };
        }
        private static void ValidateDeclaredBlockLength(string header, int ciphertextLength, int authenticationLength)
        {
            if (header.Length < 5 || !int.TryParse(header.AsSpan(1, 4), out int declaredLength))
                throw new ArgumentException("TR-31 header must contain a four-digit total length.");
            int actualLength = checked(header.Length + 2 * (ciphertextLength + authenticationLength));
            if (declaredLength != actualLength)
                throw new ArgumentException(
                    $"TR-31 header declares total length {declaredLength}, but wrap produces {actualLength} characters.");
        }
        internal static byte[] DeriveKey(byte[] key, byte[] data, int keySize)
        {
            IMac mac = new CMac(new AesEngine());
            byte[] derivedKey = new byte[32];
            byte[]? result = null;
            switch (keySize)
            {
                case 128:
                    result = new byte[16];
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data, 0, data.Length);                    
                    mac.DoFinal(result, 0);
                    break;
                case 192:
                    byte[] data_1 = new byte[data.Length / 2];
                    byte[] data_2 = new byte[data.Length / 2];
                    Array.Copy(data, 0, data_1, 0, data.Length / 2);
                    Array.Copy(data, data.Length / 2, data_2, 0, data.Length / 2);
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data_1, 0, data_1.Length);
                    mac.DoFinal(derivedKey, 0);
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data_2, 0, data_2.Length);
                    mac.DoFinal(derivedKey, 16);
                    result=new byte[192/8];
                    Array.Copy(derivedKey, 0, result, 0, 192 / 8);
                    break;
                case 256:
                    byte[] data_3 = new byte[data.Length / 2];
                    byte[] data_4 = new byte[data.Length / 2];
                    Array.Copy(data, 0, data_3, 0, data.Length / 2);
                    Array.Copy(data, data.Length / 2, data_4, 0, data.Length / 2);
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data_3, 0, data_3.Length);
                    mac.DoFinal(derivedKey, 0);
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data_4, 0, data_4.Length);
                    mac.DoFinal(derivedKey, 16);
                    result=derivedKey;
                    break;
                default:
                    result = new byte[16];
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data, 0, data.Length);
                    mac.DoFinal(result, 0);
                    break;
            }  
            return result;
        }
        internal static byte[] MACDerivationData(int BPkeySize)
        {
            switch(BPkeySize)
            {
                case 128:
                    return new byte[] { 0x01,0x00,0x01,0x00,0x00,0x02,0x00,0x80 };
                case 192:
                    return new byte[] { 0x01, 0x00, 0x01, 0x00, 0x00, 0x03, 0x00, 0xc0, 0x02, 0x00, 0x01, 0x00, 0x00, 0x03, 0x00, 0xc0 };
                case 256:
                    return new byte[] { 0x01, 0x00, 0x01, 0x00, 0x00, 0x04, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x04, 0x01, 0x00 };
                default:
                    return new byte[] { 0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x00, 0x80 };
            }
            
        }
        internal static byte[] EncryptionDerivationData(int BPkeySize)
        {
            switch (BPkeySize)
            {
                case 128:
                    return new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x80 };
                case 192:
                    return new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0xC0, 0x02, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0xC0 };
                case 256:
                    return new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x04, 0x01, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x04, 0x01, 0x00 };
                default:
                    return new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x80 };
            }
        }   
    }
}
