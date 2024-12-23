namespace CryptoScript.CryptoAlgorithm
{
    public class AlgorithmFactory
    {
        public AlgorithmFactory() { }
        public static CryptoAlgorithm Create(string mechanism) 
        {
            if (mechanism.Contains("AES"))
                return new AES.AES();
            return new SymmetricCryptoAlgorithm();
        }
    }
}
