namespace CryptoScript.CryptoAlgorithm.WRAPPERS
{
    public class OptionalBlock
    {
        public string? ID { get; set; }
        public int Length { get; set; }
        public string? Data { get; set; }
        public string Serialize()
        {
            if (ID is null || ID.Length != 2 || !ID.All(char.IsAsciiLetterOrDigit) ||
                Data is null || Data.Any(c => c < ' ' || c > '~'))
                throw new ArgumentException("Optional Block requires a two-character ID and printable ASCII data.");
            int length = checked(Data.Length + 4);
            if (length <= 255) return ID + length.ToString("X2") + Data;
            length = checked(Data.Length + 10);
            if (length > 9999) throw new ArgumentException("Optional Block exceeds TR-31 total length limit.");
            return ID + "0004" + length.ToString("X4") + Data;
        }
    }
}
