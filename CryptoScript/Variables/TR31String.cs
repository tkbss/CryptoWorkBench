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
        private static readonly System.Text.RegularExpressions.Regex CompositePattern = new(
            "^\\s*(\".*\")0x\\(([0-9a-fA-F]+)\\)0x\\(([0-9a-fA-F]+)\\)\\s*$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        internal static bool IsComposite(string text) => CompositePattern.IsMatch(text);
        internal static string ReadWireString(string text, Action<string>? requireVersion = null) =>
            ResolveQuoted(text, candidate => CryptoAlgorithm.WRAPPERS.TR31Block.FromString(candidate), requireVersion);

        private static string ResolveQuoted(string text, Action<string> validate, Action<string>? requireVersion = null)
        {
            text = text.Trim();
            if (text.Length < 2 || text[0] != '"' || text[^1] != '"')
                throw new ArgumentException("Expected a quoted TR-31 representation.");
            requireVersion?.Invoke(text[1..^1]);
            // Two precisely defined encodings: historical literal bytes between quotes,
            // and JSON string encoding. Length/structure must select a unique byte sequence
            // before cryptography; never retry another interpretation after a MAC failure.
            var candidates = new HashSet<string>(StringComparer.Ordinal) { text[1..^1] };
            try
            {
                if (Newtonsoft.Json.JsonConvert.DeserializeObject<string>(text) is { } decoded)
                    candidates.Add(decoded);
            }
            catch (Newtonsoft.Json.JsonException) { /* A literal legacy backslash need not be a JSON escape. */ }
            var valid = new List<string>();
            foreach (string candidate in candidates)
            {
                try { validate(candidate); valid.Add(candidate); }
                catch (ArgumentException) { }
            }
            if (valid.Count != 1)
                throw new ArgumentException("TR-31 representation is incomplete, invalid or ambiguous.");
            return valid[0];
        }
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
            return Newtonsoft.Json.JsonConvert.SerializeObject(Block) +
                FormatConversions.ByteArrayToHexString(Cryptogram) + FormatConversions.ByteArrayToHexString(Mac);
        }
        public static TR31String FromString(string tr31) => FromString(tr31, null);

        internal static TR31String FromString(string tr31, Action<string>? requireVersion)
        {
            var match = CompositePattern.Match(tr31);
            if(!match.Success)
                throw new Exception("Invalid TR31 String");
            // Preserve mechanism/version diagnostics before interpreting version-specific
            // ciphertext/MAC structure. Both emitted encodings leave the version byte literal.
            string literalHeader = match.Groups[1].Value[1..^1];
            requireVersion?.Invoke(literalHeader);
            byte[] ciphertext, mac;
            try
            {
                ciphertext = Convert.FromHexString(match.Groups[2].Value);
                mac = Convert.FromHexString(match.Groups[3].Value);
            }
            catch (FormatException e) { throw new ArgumentException("Incomplete composite hexadecimal bytes.", e); }
            if (literalHeader.Length > 0 && literalHeader[0] is 'A' or 'B' or 'C' or 'D')
            {
                int expected = CryptoAlgorithm.WRAPPERS.TR31Block.GetAuthenticationValueLength(literalHeader[0]);
                if (mac.Length != expected)
                    throw new ArgumentException($"TR-31 version {literalHeader[0]} authentication value must contain exactly {expected} bytes.");
            }
            string header = ResolveQuoted(match.Groups[1].Value, candidate =>
            {
                var parsed = CryptoAlgorithm.WRAPPERS.TR31Block.FromString(
                    candidate + match.Groups[2].Value + match.Groups[3].Value);
                if (parsed.HeaderDataToMac!.Length != candidate.Length ||
                    parsed.Cryptogram!.Length != ciphertext.Length || parsed.Mac!.Length != mac.Length)
                    throw new ArgumentException("Composite boundaries do not match the TR-31 wire structure.");
            });
            return new TR31String(header, ciphertext, mac);
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
