namespace CryptoScript.CryptoAlgorithm
{
    public class AlgorithmFactory
    {
        public AlgorithmFactory() { }
        public static CryptoAlgorithm Create(string mechanism) 
        {
            string normalizedMechanism = mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
                ? mechanism["#MECH:".Length..]
                : mechanism;
            string canonicalMechanism = normalizedMechanism.ToUpperInvariant();

            if (canonicalMechanism == "BLOCKHEADER-WRAP-AES-TR31")
                return new WRAPPERS.Tr31BlockHeader();
            if (canonicalMechanism == "WRAP-AES-TR31")
                return new WRAPPERS.WrapAESTR31();
            if (canonicalMechanism == "WRAP-AES-PINBLOCK-4")
                return new PINBLOCK.PinBlockAlgorithm(
                    canonicalMechanism, PINBLOCK.PinBlockCipherFamily.Aes);
            if (canonicalMechanism is "WRAP-DES3-PINBLOCK-0" or "WRAP-DES3-PINBLOCK-1" or
                "WRAP-DES3-PINBLOCK-2" or "WRAP-DES3-PINBLOCK-3")
            {
                return new PINBLOCK.PinBlockAlgorithm(
                    canonicalMechanism, PINBLOCK.PinBlockCipherFamily.Des3);
            }
            if (canonicalMechanism == "DUKPT-AES-INITIAL-KEY")
                return new KDF.DUKPT_AES_INITIAL_KEY();
            if (canonicalMechanism == "DUKPT-AES-WORKING-KEY")
                return new KDF.DUKPT_AES_WORKING_KEY();
            if (canonicalMechanism == "DUKPT-TDEA-INITIAL-KEY")
                return new KDF.DUKPT_TDEA_INITIAL_KEY();
            if (canonicalMechanism == "DUKPT-TDEA-WORKING-KEY")
                return new KDF.DUKPT_TDEA_WORKING_KEY();
            if (IsAesMechanism(canonicalMechanism))
                return new AES.AES();
            if (IsHmacMechanism(canonicalMechanism))
                return new HMAC.HMAC();
            if (HASH.HASH.IsSupportedMechanism(canonicalMechanism))
                return new HASH.HASH();
            if (canonicalMechanism is "KDF-HKDF" or "HKDF-EXTRACT" or "HKDF-EXPAND")
                return new KDF.KDF_HKDF();
            if (canonicalMechanism == "KDF-SP800-108-COUNTER")
                return new KDF.KDF_SP800_108_COUNTER();
            if (canonicalMechanism == "KDF-EMV-AC-SESSION")
                return new KDF.KDF_EMV_AC_SESSION();
            if (canonicalMechanism == "KDF-EMV-MASTER-A")
                return new KDF.KDF_EMV_MASTER_A();
            if (canonicalMechanism == "KDF-EP2-SESSION")
                return new KDF.KDF_EP2_SESSION();
            if (canonicalMechanism == "KDF-EP2-PAN-SURROGATE-TRX")
                return new KDF.KDF_EP2_PAN_SURROGATE_TRX();
            if (canonicalMechanism == "KDF-EP2-PAN-RECEIPT-TRX")
                return new KDF.KDF_EP2_PAN_RECEIPT_TRX();
            if (canonicalMechanism == "KDF-EP2-PAN-RECEIPT-TRM")
                return new KDF.KDF_EP2_PAN_RECEIPT_TRM();
            if (canonicalMechanism == "WRAP-DES3-TR31")
                return new WRAPPERS.WrapDES3TR31();
            if (canonicalMechanism is "DES3-CBC" or "DES3-ECB" or "DES3-RETAIL" or "DES3-CMAC")
                return new DES3.DES3();
            throw new NotSupportedException($"Unsupported mechanism: {normalizedMechanism}.");
        }

        private static bool IsAesMechanism(string mechanism) =>
            mechanism is "AES-CBC" or "AES-CBC-MAC" or "AES-CCM" or "AES-CMAC" or "AES-CTR" or
                "AES-ECB" or "AES-GCM" or "AES-GMAC";

        private static bool IsHmacMechanism(string mechanism) =>
            mechanism is "HMAC-SHA1" or "HMAC-SHA224" or "HMAC-SHA256" or
                "HMAC-SHA384" or "HMAC-SHA512" or "HMAC-SHA512-224" or
                "HMAC-SHA512-256" or "HMAC-SHA3-224" or "HMAC-SHA3-256" or
                "HMAC-SHA3-384" or "HMAC-SHA3-512";
    }
}
