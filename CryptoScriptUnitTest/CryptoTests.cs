
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest
{
    // References for the .NET and BouncyCastle cryptography APIs, not CryptoWorkBench tests.
    // Failures here do not automatically indicate a CryptoWorkBench defect.
    [Category("ExternalCrypto")]
    public class CryptoTests
    {
        private static T[] ConcatenateArrays<T>(T[] array1, T[] array2)
        {
            if (array1 == null || array2 == null)
            {
                throw new ArgumentNullException(array1 == null ? nameof(array1) : nameof(array2));
            }

            T[] result = new T[array1.Length + array2.Length];
            Array.Copy(array1, result, array1.Length);
            Array.Copy(array2, 0, result, array1.Length, array2.Length);

            return result;
        }
        private static void IncrementCounter(byte[] counter)
        {
            if (counter.Length != 4)
                throw new ArgumentException("Counter must be exactly 4 bytes long.");

            // Increment the counter
            for (int i = 3; i >= 0; i--)
            {
                // If the counter overflows, it wraps around to 0 automatically due to byte overflow.
                if (++counter[i] != 0)
                    break; // No carry, exit the loop
            }

        }
        [Test]
        public void DotNet_AesCbc_CryptoStream_Pkcs7PaddingAndRoundtrip()
        {
            byte[] encrypted;
            using (Aes aesAlg = Aes.Create())
            {
                // CBC with PKCS7: 18 input bytes require two 16-byte ciphertext blocks.
                aesAlg.Mode = CipherMode.CBC;
                //set key
                byte[] keyBytes =  {0x0,0x1,0x2,0x3,0x4,0x5,0x6,0x7,0x8,0x9,0xa,0xb,0xc,0xd,0xe,0xf };
                aesAlg.Key = keyBytes;
                byte[] iv = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7,0x8,0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
                //set iv
                aesAlg.IV = iv;
                byte[] input = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5 };
                ClassicAssert.IsTrue(input.Length == 18);                
                aesAlg.Padding = PaddingMode.PKCS7;
                // Create an encryptor to perform the stream transform.
                using (ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV))
                {
                    // Create the streams used for encryption.                
                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(input, 0, input.Length);
                            csEncrypt.FlushFinalBlock();

                            
                        }                        
                        encrypted = msEncrypt.ToArray();
                        using var decryptor = aesAlg.CreateDecryptor();
                        Assert.That(decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length),
                            Is.EqualTo(input));
                    }
                }
                
            }
            ClassicAssert.IsTrue(encrypted.Length == 32);
        }
        [Test]
        public void DotNet_AesEcbBasedCtr_Encryption_MatchesExistingVector()
        {
            byte[] keyBytes = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
            byte[] input = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5 };
            byte[] output = new byte[input.Length];       
            using (Aes aesAlg = Aes.Create())
            {
                // ECB is important here 
                aesAlg.Mode = CipherMode.ECB;
                // No padding
                aesAlg.Padding = PaddingMode.None;
                aesAlg.Key = keyBytes;
                byte[] buffer = new byte[16];
                int blockCount = (input.Length + 15) / 16;
                byte[] nonce = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb };
                ClassicAssert.IsTrue(nonce.Length == 12);
                byte[] counter = { 0x0, 0x0, 0x0, 0xff };
                ClassicAssert.IsTrue(counter.Length == 4);
                byte[] iv = ConcatenateArrays(nonce, counter);
                for (int i = 0; i < blockCount; i++)
                {
                    // Copy the next part of the IV or counter into the buffer
                    Array.Copy(iv, 0, buffer, 0, 16);
                    // Encrypt the buffer (the IV/counter)
                    byte[] encryptedCounter = aesAlg.CreateEncryptor().TransformFinalBlock(buffer, 0, 16);

                    // XOR the encrypted counter with the input and store in the output
                    for (int j = 0; j < 16 && (i * 16 + j) < input.Length; j++)
                    {
                        output[i * 16 + j] = (byte)(input[i * 16 + j] ^ encryptedCounter[j]);
                    }
                    IncrementCounter(counter);
                    iv = ConcatenateArrays(nonce, counter);

                }
            }
            // Fixed ciphertext already used by the decryption example below.
            Assert.That(output, Is.EqualTo(Convert.FromHexString("DDFEC9A455E7C25DCD3CD3478484B9BDFD8D")));
        }
        [Test]
        public void DotNet_AesEcbBasedCtr_Decryption_MatchesExistingVector()
        {
            byte[] keyBytes = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
            byte[] input = { 0xdd,0xfe,0xc9,0xa4,0x55,0xe7,0xc2,0x5d,0xcd,0x3c,0xd3,0x47,0x84,0x84,0xb9,0xbd,0xfd,0x8d};
            byte[] cleartext = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5 };
            ClassicAssert.IsTrue(input.Length == 18);
            byte[] output = new byte[input.Length];
            using (Aes aesAlg = Aes.Create())
            {
                // ECB is important here 
                aesAlg.Mode = CipherMode.ECB;
                // No padding
                aesAlg.Padding = PaddingMode.None;
                aesAlg.Key = keyBytes;
                byte[] buffer = new byte[16];
                int blockCount = (input.Length + 15) / 16;
                byte[] nonce = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb };
                ClassicAssert.IsTrue(nonce.Length == 12);
                byte[] counter = { 0x0, 0x0, 0x0, 0xff };
                ClassicAssert.IsTrue(counter.Length == 4);
                byte[] iv = ConcatenateArrays(nonce, counter);
                for (int i = 0; i < blockCount; i++)
                {
                    // Copy the next part of the IV or counter into the buffer
                    Array.Copy(iv, 0, buffer, 0, 16);
                    // Encrypt the buffer (the IV/counter)
                    byte[] encryptedCounter = aesAlg.CreateEncryptor().TransformFinalBlock(buffer, 0, 16);

                    // XOR the encrypted counter with the input and store in the output
                    for (int j = 0; j < 16 && (i * 16 + j) < input.Length; j++)
                    {
                        output[i * 16 + j] = (byte)(input[i * 16 + j] ^ encryptedCounter[j]);
                    }
                    IncrementCounter(counter);
                    iv = ConcatenateArrays(nonce, counter);                  

                }
            }
            ClassicAssert.That(output, Is.EqualTo(cleartext));
        }
        [Test]
        public void DotNet_AesEcb_NoPadding_Roundtrip()
        {
            byte[] keyBytes = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
            byte[] input = { 0x00,0x11,0x22,0x33,0x44,0x55,0x66,0x77,0x88,0x99,0xAA,0xBB,0xCC,0xDD,0xEE,0xFF };
            ClassicAssert.IsTrue(input.Length == 16);
            byte[] encrypted,cleartext;
            using (Aes aesAlg = Aes.Create())
            {
                // ECB mode
                aesAlg.Mode = CipherMode.ECB;
                // No padding
                aesAlg.Padding = PaddingMode.None;
                aesAlg.Key = keyBytes;
                encrypted = aesAlg.CreateEncryptor().TransformFinalBlock(input, 0, input.Length);              

            }
            using (Aes aesAlg = Aes.Create())
            {
                // ECB mode
                aesAlg.Mode = CipherMode.ECB;
                aesAlg.Key = keyBytes;
                // No padding
                aesAlg.Padding = PaddingMode.None;
                cleartext = aesAlg.CreateDecryptor().TransformFinalBlock(encrypted, 0, encrypted.Length);

            }
            ClassicAssert.That(input, Is.EqualTo(cleartext));

        }
        [Test]
        public void BouncyCastle_Gmac_MatchesFixedTag()
        {
            // Fixed inputs retain the original authenticated message for a repeatable reference.
            byte[] key = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");

            // 96-bit nonce
            byte[] nonce = Convert.FromHexString("000102030405060708090A0B");

            // Data to authenticate
            byte[] dataToAuthenticate = System.Text.Encoding.UTF8.GetBytes("Hello GMAC world!");

            // Configure GMAC
            // "GMac" class takes a GcmBlockCipher internally, but you configure it for MAC only
            var gMac = new GMac(new GcmBlockCipher(new AesEngine()));

            // GMac accepts the AES key and nonce via ParametersWithIV.
            // This constructor uses a 128-bit tag.
            var parameters = new ParametersWithIV(new KeyParameter(key), nonce);
            gMac.Init(parameters);

            // Update with data
            gMac.BlockUpdate(dataToAuthenticate, 0, dataToAuthenticate.Length);

            // Output the MAC
            byte[] mac = new byte[16]; // 128 bits
            Assert.That(gMac.DoFinal(mac, 0), Is.EqualTo(16));
            // Independently calculated with .NET AesGcm: empty plaintext, the message
            // above as associated data, and the same fixed key and 96-bit nonce.
            Assert.That(mac, Is.EqualTo(Convert.FromHexString("0C14CE9B6406EC11CB62C602B02C7B98")));
        }
        [Test]
        public void DotNet_AesCbc_NoPadding_ConfigurationSmokeTest()
        {
            byte[] keyBytes = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
            using (Aes aesAlg = Aes.Create())
            {
                // Configuration only: CBC is not CMAC; no MAC is calculated here.
                aesAlg.Mode = CipherMode.CBC;
                // No padding
                aesAlg.Padding = PaddingMode.None;
                aesAlg.Key = keyBytes;
                Assert.That(aesAlg.Mode, Is.EqualTo(CipherMode.CBC));
                Assert.That(aesAlg.Padding, Is.EqualTo(PaddingMode.None));
                Assert.That(aesAlg.Key, Is.EqualTo(keyBytes));

            }
        }
    }
}
