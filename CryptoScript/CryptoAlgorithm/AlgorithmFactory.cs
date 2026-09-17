namespace CryptoScript.CryptoAlgorithm
{
    public class AlgorithmFactory
    {
        public AlgorithmFactory() { }
        public static CryptoAlgorithm Create(string mechanism) 
        {
            if ((mechanism.ToUpper().StartsWith("BLOCKHEADER")))
            {
                if (mechanism.ToUpper().Contains("AES-TR31"))
                    return new WRAPPERS.Tr31BlockHeader();
            }
            if (mechanism.StartsWith("WRAP")) 
            {
                if (mechanism.Contains("AES-TR31"))
                    return new WRAPPERS.WrapAESTR31();                
            }
            if (mechanism.Contains("AES"))
                return new AES.AES();
            string normalizedMechanism = mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
                ? mechanism["#MECH:".Length..]
                : mechanism;
            if (normalizedMechanism.StartsWith("HMAC-", StringComparison.OrdinalIgnoreCase))
                return new HMAC.HMAC();
            if (normalizedMechanism.Equals("WRAP-DES3-TR31", StringComparison.OrdinalIgnoreCase))
                return new WRAPPERS.WrapDES3TR31();
            if (normalizedMechanism.Equals("DES3-CBC", StringComparison.OrdinalIgnoreCase) ||
                normalizedMechanism.Equals("DES3-ECB", StringComparison.OrdinalIgnoreCase) ||
                normalizedMechanism.Equals("DES3-RETAIL", StringComparison.OrdinalIgnoreCase) ||
                normalizedMechanism.Equals("DES3-CMAC", StringComparison.OrdinalIgnoreCase))
                return new DES3.DES3();
            return new SymmetricCryptoAlgorithm();
        }
    }
}
