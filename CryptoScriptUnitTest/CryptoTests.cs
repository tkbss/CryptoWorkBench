using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CryptoScriptUnitTest
{
    public class CryptoTests
    {
        [Test]
        public void AESEncryptionTest() 
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
    }
}
