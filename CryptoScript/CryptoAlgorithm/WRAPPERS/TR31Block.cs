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

        /// <summary>Returns the authentication value length in bytes, before hex encoding.</summary>
        public static int GetAuthenticationValueLength(char version) => version switch
        {
            'A' or 'C' => 4,
            'B' => 8,
            'D' => 16,
            // Preserve the parser's previous behavior for unrecognized versions.
            // Version validation is deliberately separate from this structural change.
            _ => 16
        };

        
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
        /// <summary>Parses a complete wire block. Header-only and truncated inputs are not key blocks.</summary>
        public static TR31Block FromString(string tr31)
        {
            ArgumentNullException.ThrowIfNull(tr31);
            if (tr31.Length < 16 || tr31.Length > 9999 || tr31[0] is not ('A' or 'B' or 'C' or 'D') ||
                tr31.AsSpan(1, 4).IndexOfAnyExcept("0123456789") >= 0 ||
                tr31.AsSpan(12, 2).IndexOfAnyExcept("0123456789") >= 0 ||
                int.Parse(tr31.AsSpan(1, 4), System.Globalization.CultureInfo.InvariantCulture) != tr31.Length)
                throw new ArgumentException("TR-31 requires a supported version and an exact complete wire length.");

            var block = new TR31Block { Header = tr31[..16], OptionalBlocks = new List<OptionalBlock>() };
            int count = int.Parse(tr31.AsSpan(12, 2), System.Globalization.CultureInfo.InvariantCulture);
            int position = 16;
            for (int i = 0; i < count; i++)
            {
                if (position + 4 > tr31.Length)
                    throw new ArgumentException("Incomplete TR-31 Optional Block.");
                string id = tr31.Substring(position, 2);
                if (!id.All(char.IsAsciiLetterOrDigit))
                    throw new ArgumentException("Invalid TR-31 Optional Block identifier.");
                int length = ReadHexLength(tr31.AsSpan(position + 2, 2));
                int prefix = 4;
                if (length == 0)
                {
                    prefix = 10;
                    if (position + prefix > tr31.Length || tr31.Substring(position + 4, 2) != "04")
                        throw new ArgumentException("Invalid extended Optional Block length.");
                    length = ReadHexLength(tr31.AsSpan(position + 6, 4));
                }
                if (length < prefix || length > tr31.Length - position)
                    throw new ArgumentException("Optional Block length exceeds the complete wire block.");
                block.OptionalBlocks.Add(new OptionalBlock { ID = id, Data = tr31.Substring(position + prefix, length - prefix) });
                position += length;
            }
            string header = tr31[..position];
            if (header.Any(c => c < ' ' || c > '~'))
                throw new ArgumentException("TR-31 header must contain printable ASCII.");
            block.HeaderDataToMac = Encoding.ASCII.GetBytes(header);
            byte[] data = HexStringToByteArray(tr31[position..]);
            int macLength = GetAuthenticationValueLength(tr31[0]);
            int cipherLength = data.Length - macLength;
            int blockSize = tr31[0] == 'D' ? 16 : 8;
            if (cipherLength <= 0 || cipherLength % blockSize != 0)
                throw new ArgumentException("TR-31 requires complete aligned ciphertext and a version-sized MAC.");
            block.Cryptogram = data[..cipherLength];
            block.Mac = data[cipherLength..];
            return block;
        }

        private static int ReadHexLength(ReadOnlySpan<char> text)
        {
            if (text.IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
                throw new ArgumentException("Invalid hexadecimal Optional Block length.");
            return int.Parse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
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
            if (hex.Length % 2 != 0 || hex.AsSpan().IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
                throw new ArgumentException("TR-31 ciphertext and MAC require complete hexadecimal bytes.");
            return Convert.FromHexString(hex);
        }
    }
}

