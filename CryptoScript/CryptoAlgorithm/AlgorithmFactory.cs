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
            if (HASH.HASH.IsSupportedMechanism(normalizedMechanism))
                return new HASH.HASH();
            if (normalizedMechanism.Equals("KDF-HKDF", StringComparison.OrdinalIgnoreCase) ||
                normalizedMechanism.Equals("HKDF-EXTRACT", StringComparison.OrdinalIgnoreCase) ||
                normalizedMechanism.Equals("HKDF-EXPAND", StringComparison.OrdinalIgnoreCase))
                return new KDF.KDF_HKDF();
            if (normalizedMechanism.Equals("KDF-SP800-108-COUNTER", StringComparison.OrdinalIgnoreCase))
                return new KDF.KDF_SP800_108_COUNTER();
            if (normalizedMechanism.Equals("KDF-EP2-SESSION", StringComparison.OrdinalIgnoreCase))
                return new KDF.KDF_EP2_SESSION();
            if (normalizedMechanism.Equals("KDF-EP2-PAN-SURROGATE-TRX", StringComparison.OrdinalIgnoreCase))
                return new KDF.KDF_EP2_PAN_SURROGATE_TRX();
            if (normalizedMechanism.Equals("KDF-EP2-PAN-RECEIPT-TRX", StringComparison.OrdinalIgnoreCase))
                return new KDF.KDF_EP2_PAN_RECEIPT_TRX();
            if (normalizedMechanism.Equals("KDF-EP2-PAN-RECEIPT-TRM", StringComparison.OrdinalIgnoreCase))
                return new KDF.KDF_EP2_PAN_RECEIPT_TRM();
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
