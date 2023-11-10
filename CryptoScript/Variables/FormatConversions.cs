using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Variables
{
    public class FormatConversions
    {
        static public readonly string HEX = "HEX_STRING";
        static public readonly string B64 = "BASE64_STRING";
        public static string ByteArrayToHexString(byte[] byteArray)
        {
            StringBuilder hex = new StringBuilder(byteArray.Length * 2);
            foreach (byte b in byteArray)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
        public static string ParseString(string input) 
        {
            if (input.StartsWith("0x("))
                return HEX;
            else
                return B64;
        }
    }
}
