using CryptoScript.Variables;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CryptoScriptUnitTest
{
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
        public void AES_CBC_EncryptionTest() 
        {
            byte[] encrypted;
            using (Aes aesAlg = Aes.Create())
            {
                //cbc mode 
                aesAlg.Mode = CipherMode.CBC;
                //set key
                byte[] keyBytes =  {0x0,0x1,0x2,0x3,0x4,0x5,0x6,0x7,0x8,0x9,0xa,0xb,0xc,0xd,0xe,0xf };
                aesAlg.Key = keyBytes;
                byte[] iv = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7,0x8,0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
                //set iv
                aesAlg.IV = iv;
                byte[] input = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5 };
                Assert.IsTrue(input.Length == 18);                
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
                    }
                }
                
            }
            Assert.IsTrue(encrypted.Length == 32);
        }
        [Test]
        public void AES_CTR_ENC_Test()
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
                Assert.IsTrue(nonce.Length == 12);
                byte[] counter = { 0x0, 0x0, 0x0, 0xff };
                Assert.IsTrue(counter.Length == 4);
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

        }
        [Test]
        public void AES_CTR_DEC_Test()
        {
            byte[] keyBytes = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
            byte[] input = { 0xdd,0xfe,0xc9,0xa4,0x55,0xe7,0xc2,0x5d,0xcd,0x3c,0xd3,0x47,0x84,0x84,0xb9,0xbd,0xfd,0x8d};
            byte[] cleartext = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5 };
            Assert.IsTrue(input.Length == 18);
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
                Assert.IsTrue(nonce.Length == 12);
                byte[] counter = { 0x0, 0x0, 0x0, 0xff };
                Assert.IsTrue(counter.Length == 4);
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
            Assert.That(output, Is.EqualTo(cleartext));
        }
        [Test]
        public void AES_ECB_Test() 
        {
            byte[] keyBytes = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
            byte[] input = { 0x00,0x11,0x22,0x33,0x44,0x55,0x66,0x77,0x88,0x99,0xAA,0xBB,0xCC,0xDD,0xEE,0xFF };
            Assert.IsTrue(input.Length == 16);
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
            Assert.That(input, Is.EqualTo(cleartext));

        }
        [Test]
        public void AES_CMAC_Test() 
        {
            byte[] keyBytes = { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
            byte[] input = { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
            Assert.IsTrue(input.Length == 16);
            using (Aes aesAlg = Aes.Create())
            {
                //CMAC mode
                aesAlg.Mode = CipherMode.CBC;
                // No padding
                aesAlg.Padding = PaddingMode.None;
                aesAlg.Key = keyBytes;
                byte[] buffer = new byte[16];

            }
        }
    }
}
