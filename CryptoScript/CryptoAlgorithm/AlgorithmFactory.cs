namespace CryptoScript.CryptoAlgorithm
{
    public class AlgorithmFactory
    {
        public AlgorithmFactory() { }
        public static CryptoAlgorithm Create(string mechanism) 
        {
            if (mechanism.StartsWith("AES"))
                return new AES();
            return new SymmetricCryptoAlgorithm();
        }
    }
}
