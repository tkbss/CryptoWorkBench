using CryptoScript.Variables;
using System.Text;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS
{
    //public class OptionalBlock
    //{
    //    public string? ID { get; set; }
    //    public string? Data { get; set; }
    //}
    public class TR31Block
    {
        public string? Header { get; set; }
        public List<OptionalBlock>? OptionalBlocks { get; set; }
        public byte[]? Cryptogram { get; set; }
        public byte[]? Mac { get; set; }
        public byte[]? HeaderDataToMac { get; set; } = null;

        
        public List<OptionalBlock> HeaderOptionalBlocks()
        {
            var hdr = new OptionalBlock()
            {
                ID = "HDR",
                Data = Header
            };
            var optionalBlocks = OptionalBlocks ?? new List<OptionalBlock>();
            optionalBlocks.Insert(0, hdr); // Add header as the first element
            return optionalBlocks;
        }
        public static TR31Block FromString(string tr31)
        {
            var block = new TR31Block { OptionalBlocks = new List<OptionalBlock>() };

            // Extract fixed-length Key Block Header (16 bytes)
            block.Header = tr31.Substring(0, 16);
            if(tr31.Length < 17)
            {
                block.HeaderDataToMac = FormatConversions.StringToByteArray(block.Header);
                return block;
            }

            // Parse the total length of the TR31 block from header (bytes 1-4)
            int totalLength = int.Parse(tr31.Substring(1, 4));

            // Parse number of optional blocks from header (bytes 12-13)
            int optionalBlockCount = int.Parse(tr31.Substring(12, 2));

            int currentPosition = 16;

            // Parse optional blocks
            for (int i = 0; i < optionalBlockCount; i++)
            {
                string id = tr31.Substring(currentPosition, 2);
                currentPosition += 2;

                int length = Convert.ToInt32(tr31.Substring(currentPosition, 2), 16);
                currentPosition += 2;
                int dataLength = length - 4; // Length minus ID and length fields
                if (length == 0) 
                {
                    length= Convert.ToInt32(tr31.Substring(currentPosition, 2), 16);
                    currentPosition += 2;
                    length= Convert.ToInt32(tr31.Substring(currentPosition, 4), 16);
                    currentPosition += 4;
                    dataLength = length-4-6 ; // Length minus ID and length fields
                }                
                string dataString = tr31.Substring(currentPosition, dataLength);
                currentPosition += dataLength;
                //byte[] data = DecodeOptionalBlockData(id, dataString);
                block.OptionalBlocks.Add(new OptionalBlock { ID = id, Data = dataString });
            }
            block.HeaderDataToMac = FormatConversions.StringToByteArray(tr31.Substring(0,currentPosition));
            // Extract Cryptogram + MAC
            int cryptogramAndMacLength = totalLength - currentPosition;
            string cryptogramAndMacHex = tr31.Substring(currentPosition, cryptogramAndMacLength);
            byte[] cryptogramAndMac = HexStringToByteArray(cryptogramAndMacHex);

            if (cryptogramAndMac.Length < 16) 
            {
                // Block contains only header
                return block;
            }               
            block.Mac = cryptogramAndMac.Skip(cryptogramAndMac.Length - 16).ToArray();
            block.Cryptogram = cryptogramAndMac.Take(cryptogramAndMac.Length - 16).ToArray();
            return block;
        }

        private static byte[] DecodeOptionalBlockData(string id, string data)
        {
            switch (id)
            {
                // Base64 encoded optional blocks according to TR31 specification:
                case "PC": // Public Key Certificate (Single Certificate)
                case "CC": // Public Key Certificate Chain
                case "CT": // Certificate (public key data)
                case "HM": // HMAC
                    return Convert.FromBase64String(data);

                // Hex-encoded optional blocks:
                case "KI": // Initial Key Identifier
                case "KCV": // Key Check Value
                case "BP": // Binding Pedigree
                case "PB": // Padding Block
                case "TS": // Timestamp
                case "TC": // Time of Creation
                case "KS": // Key Set Identifier
                case "WP": // Wrapping Pedigree
                    return HexStringToByteArray(data);

                // Plain ASCII or printable data:
                case "LB": // Label
                case "DA": // Derivation Allowed
                default:   // Default fallback, assume ASCII encoding
                    return Encoding.ASCII.GetBytes(data);
            }
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length / 2)
                             .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                             .ToArray();
        }
    }
}

