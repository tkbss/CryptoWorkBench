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
            return new SymmetricCryptoAlgorithm();
        }
    }
}
