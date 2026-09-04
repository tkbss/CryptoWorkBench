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
            if (normalizedMechanism.Equals("DES3-CBC", StringComparison.OrdinalIgnoreCase) ||
                normalizedMechanism.Equals("DES3-ECB", StringComparison.OrdinalIgnoreCase) ||
                normalizedMechanism.Equals("DES3-CMAC", StringComparison.OrdinalIgnoreCase))
                return new DES3.DES3();
            return new SymmetricCryptoAlgorithm();
        }
    }
}
