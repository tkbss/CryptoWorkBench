using CryptoScript.Model;
using Org.BouncyCastle.Tls;
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
        static public readonly string JSO = "JSON_STRING";
        static public readonly string PAR = "PARAM_STRING";
        static public readonly string STR = "NORMAL_STRING";
        static public readonly string TR31 = "TR31_STRING";
        public static string ByteArrayToHexString(byte[] byteArray)
        {
            StringBuilder hex = new StringBuilder(byteArray.Length * 2);
            foreach (byte b in byteArray)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            string hexStr = "0x(" + hex.ToString() + ")";
            return hexStr;
        }
        public static string ToTR31String(string block,byte[] cryptogram,byte[] mac)
        {
            string tr31=ToScriptString(block) + FormatConversions.ByteArrayToHexString(cryptogram) + FormatConversions.ByteArrayToHexString(mac);
            return tr31;
        }
        public static byte[] ToByteArray(string input,string format)
        {
               if(format == HEX)
                return HexStringToByteArray(input);
            if(format == B64)
                return Convert.FromBase64String(input);
            if(format == STR)
                return StringToByteArray(input);
            return new byte[0];
        }
        public static string ByteArrayToString(byte[] byteArray,string format)
        {
            if(format==HEX)
                return ByteArrayToHexString(byteArray);
            if (format==B64)            
                return Convert.ToBase64String(byteArray);            
            return string.Empty;
        }
        public static string ToScriptString(string NormalString)
        {
            return "\""+NormalString+"\"";
        }
        public static string ToString(string ScriptString)
        {
            if (ScriptString.StartsWith("\"") && ScriptString.EndsWith("\""))
            {
                if (string.IsNullOrEmpty(ScriptString) || ScriptString.Length <= 2)
                {
                    // Return an empty string if the input is null, empty, or has less than 3 characters
                    return string.Empty;
                }

                ScriptString = ScriptString.Substring(1, ScriptString.Length - 2); 
            }
            return ScriptString;
        }
        public static byte[] StringToByteArray(string normalString)
        {
            if (normalString.StartsWith("\""))
                normalString = normalString.Substring(1, normalString.Length - 2);
            var byteArray = Encoding.UTF8.GetBytes(normalString);
            return byteArray;
        }
        public static byte[] HexStringToByteArray(string hexString)
        {
            if (hexString.StartsWith("0x("))
                hexString = hexString.Substring(3, hexString.Length - 4);
            
            byte[] b = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
                b[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            return b;
        }
        public static string HexStringToString(string hexString)
        {
            if (hexString.StartsWith("0x("))
                hexString = hexString.Substring(3, hexString.Length - 4);

            return hexString;
        }
        public static string ParseString(string input) 
        {
            if (input.StartsWith("\"") && input.Contains("0x("))
            {
                return TR31;
            }
            if (input.StartsWith("0x("))
                return HEX;
            if (input.StartsWith("b64("))
                return B64;
            if (input.StartsWith("\""))
                return STR;
            if (input.Contains("#"))
            {
                return PAR;
            }
            
            if (MechanismList.Instance.Mechanisms.Contains(input))
            {
                return PAR;
            }
            input = input.Trim();
            if ((input.StartsWith("{") && input.EndsWith("}")) || //For object
                (input.StartsWith("[") && input.EndsWith("]"))) //For array
            {
                return JSO;
            }
            return string.Empty;
        }
        public static bool ContainsNumber(string input)
        {
            return input.All(char.IsDigit);
        }
    }
}
