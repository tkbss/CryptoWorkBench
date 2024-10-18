using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.AES
{
    public class AES_CTR : EncryptionMode
    {
        public override StringVariableDeclaration ModeDecryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return CTRKernel(parameter, key, data);
        }

        public override StringVariableDeclaration ModeEncryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return CTRKernel(parameter, key, data);
        }
        private StringVariableDeclaration CTRKernel(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            byte[] output = new byte[input.Length];
            using (Aes aesAlg = Aes.Create())
            {
                // ECB is important here 
                aesAlg.Mode = CipherMode.ECB;
                // No padding
                aesAlg.Padding = PaddingMode.None;
                aesAlg.Key = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
                byte[] buffer = new byte[16];
                int blockCount = (input.Length + 15) / 16;
                string n = parameter.GetParameter("NONCE");
                byte[] nonce = FormatConversions.ToByteArray(n, FormatConversions.ParseString(n));
                string c = parameter.GetParameter("COUNTER");
                byte[] counter = FormatConversions.ToByteArray(c, FormatConversions.ParseString(c));
                byte[] iv = ConcatenateArrays(nonce, counter);
                for (int i = 0; i < blockCount; i++)
                {
                    // Copy the next part of the IV or counter into the buffer
                    Array.Copy(iv, 0, buffer, 0, 16);
                    // Encrypt the buffer (the IV/counter)
                    byte[] encryptedCounter = aesAlg.CreateEncryptor().TransformFinalBlock(buffer, 0, 16);

                    // XOR the encrypted counter with the input and store in the output
                    for (int j = 0; j < 16 && i * 16 + j < input.Length; j++)
                    {
                        output[i * 16 + j] = (byte)(input[i * 16 + j] ^ encryptedCounter[j]);
                    }
                    IncrementCounter(counter);
                    iv = ConcatenateArrays(nonce, counter);

                }
            }
            StringVariableDeclaration cyphertext = new StringVariableDeclaration();
            cyphertext.Value = FormatConversions.ByteArrayToHexString(output);
            cyphertext.ValueFormat = FormatConversions.ParseString(cyphertext.Value);
            cyphertext.Type = new CryptoTypeVar();
            return cyphertext;
        }
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
    }
}
