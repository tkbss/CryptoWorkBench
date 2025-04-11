using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Variables
{
    public class TR31String
    {
        public string Block { get; set; }
        public byte[] Cryptogram { get; set; }
        public byte[] Mac { get; set; }
        public string[] OptionalBlocks { get; set; }
        public TR31String(string block, byte[] cryptogram, byte[] mac)
        {
            Block = FormatConversions.ToString(block);
            Cryptogram = cryptogram;
            Mac = mac;
            OptionalBlocks = Array.Empty<string>();
        }
        public TR31String(string block, byte[] cryptogram, byte[] mac, string[]optionalBlocks)
        {
            Block = FormatConversions.ToString(block);
            Cryptogram = cryptogram;
            Mac = mac;
            OptionalBlocks = optionalBlocks;
        }
        public override bool Equals(object? obj)
        {
            if (obj == null)
                return false;
            if (obj == this) 
                return true;
            TR31String other = (TR31String)obj;
            if (FormatConversions.ToString(Block) != FormatConversions.ToString(other.Block))
            {
                return false;
            }
            if (!Cryptogram.SequenceEqual(other.Cryptogram)) 
            { 
                return false; 
            }
            if (!Mac.SequenceEqual(other.Mac)) 
            { 
                return false; 
            }
            return true;
        }
        public override string ToString()
        {
            return FormatConversions.ToTR31String(Block, Cryptogram, Mac);
        }
        public static TR31String FromString(string tr31)
        {
            if(FormatConversions.ParseString(tr31)!=FormatConversions.TR31)
                throw new Exception("Invalid TR31 String");
            string block = tr31.Substring(0, tr31.IndexOf("0x("));
            block = FormatConversions.ToString(block);
            string cryptogram = tr31.Substring(tr31.IndexOf("0x("), tr31.LastIndexOf("0x(") - tr31.IndexOf("0x("));
            string mac = tr31.Substring(tr31.LastIndexOf("0x("));
            return new TR31String(block, FormatConversions.HexStringToByteArray(cryptogram), FormatConversions.HexStringToByteArray(mac));
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (Block != null ? Block.GetHashCode() : 0);
            hash = hash * 31 + (Cryptogram != null ? Cryptogram.GetHashCode() : 0);
            hash = hash * 31 + (Mac != null ? Mac.GetHashCode() : 0);
            return hash;
        }
    }
}
