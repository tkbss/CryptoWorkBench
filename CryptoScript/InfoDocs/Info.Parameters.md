# Parameters

List of all parameters used in CRYPTO-SCRIPT. A parameter is defined by a '#' followed by the parameter name and a colon: '#'PARAMETER_NAME ':' PARAMETER_VALUE

---   
    
- MECH: Mechanism parameter specifying the cryptographic algorithm used. Information about each mechanism can be obtained through Info(mechanism).
    - MECHANISM VALUES:
        - RSA-PSS
        - RSA-OAEP
        - ECDSA
        - AES-CBC
        - AES-CTR
        - AES-GCM
        - AES-ECB
        - AES-CBC
        - AES-CTR
        - AES-CMAC
        - AES-CCM
        - AES-GMAC
        - DES3-ECB
        - DES3-CBC
        - DES3-RETAIL
        - DES3-CMAC
        - HMAC-SHA1
        - HMAC-SHA224
        - HMAC-SHA256
        - HMAC-SHA384
        - HMAC-SHA512
        - HMAC-SHA512-224
        - HMAC-SHA512-256
        - HMAC-SHA3-224
        - HMAC-SHA3-256
        - HMAC-SHA3-384
        - HMAC-SHA3-512
        - HASH-SHA1
        - HASH-SHA224
        - HASH-SHA256
        - HASH-SHA384
        - HASH-SHA512
        - HASH-SHA512-224
        - HASH-SHA512-256
        - HASH-SHA3-224
        - HASH-SHA3-256
        - HASH-SHA3-384
        - HASH-SHA3-512
        - KDF-HKDF
        - HKDF-EXTRACT
        - HKDF-EXPAND
        - KDF-SP800-108-COUNTER
        - DUKPT-AES-INITIAL-KEY
        - DUKPT-AES-WORKING-KEY
        - DUKPT-TDEA-INITIAL-KEY
        - KDF-EP2-SESSION
        - KDF-EP2-PAN-RECEIPT-TRX
        - KDF-EP2-PAN-RECEIPT-TRM
        - KDF-EP2-PAN-SURROGATE-TRX
        - WRAP-AES-TR31
        - WRAP-DES3-TR31
        - WRAP-AES
        - WRAP-DES3
- IV: Initialization vector for symmetric encryption.
- PAD: Padding scheme to be used in symmetric encryption.
    - PAD VALUES:
        - PKCS-7
        - ANSI-X923
        - ISO-7816
        - ISO-9797
        - NONE
- MACLEN: Output length in bytes for DES3-RETAIL; valid values are 4 through 8 (default 8).
- HMAC parameters contain only MECH. IV, PAD and MACLEN are not supported for HMAC mechanisms.
- HASH parameters contain only MECH. IV, PAD, MACLEN, Salt and output-length parameters are not supported for HASH mechanisms.
- Generic HKDF parameter contracts:

| Mechanism | HASH | SALT | OUTLEN |
|-----------|------|------|--------|
| KDF-HKDF | Required | Optional | Required |
| HKDF-EXTRACT | Required | Optional | Not supported |
| HKDF-EXPAND | Required | Not supported | Required |

- HASH: Hash function used by the generic HKDF mechanisms. It accepts HASH-SHA1, HASH-SHA224, HASH-SHA256, HASH-SHA384, HASH-SHA512, HASH-SHA512-224, HASH-SHA512-256, HASH-SHA3-224, HASH-SHA3-256, HASH-SHA3-384 and HASH-SHA3-512. HMAC-* mechanisms are not accepted.
- SALT: Optional for KDF-HKDF and HKDF-EXTRACT, and not supported by HKDF-EXPAND. It accepts hexadecimal data, Base64 data, a UTF-8 string or a VAR. If omitted, HKDF uses HashLen zero bytes; an empty string supplies an explicitly empty salt.
- OUTLEN: Required for KDF-HKDF and HKDF-EXPAND, and not supported by HKDF-EXTRACT. It is specified in bits and must be positive, divisible by 8 and at most 255 times HashLen times 8 bits.
- KDF-SP800-108-COUNTER parameter contract:

| Mechanism | PRF | OUTLEN | COUNTER | LABEL |
|-----------|-----|--------|---------|-------|
| KDF-SP800-108-COUNTER | Required: supported HMAC or AES-CMAC | Required: positive byte-aligned bit length, at most 4,294,967,288 bits and subject to the PRF/counter block limit | Optional: 8/16/24/32 bits, default 32 | Optional binary data, default empty |

- PRF: For KDF-SP800-108-COUNTER, selects HMAC-SHA1, HMAC-SHA224, HMAC-SHA256, HMAC-SHA384, HMAC-SHA512, HMAC-SHA512-224, HMAC-SHA512-256, HMAC-SHA3-224, HMAC-SHA3-256, HMAC-SHA3-384, HMAC-SHA3-512 or AES-CMAC. DES3-CMAC and KMAC are not supported.
- LABEL: Optional KDF-SP800-108-COUNTER binary Label in hexadecimal, Base64 or UTF-8 string form. Omission means an empty Label.
- For KDF-SP800-108-COUNTER, OUTLEN is measured in bits and parsed as an unsigned 32-bit integer. It must be greater than zero and divisible by 8, making 4,294,967,288 bits the largest value allowed by representation and byte alignment alone. The effective maximum can be lower because `ceil(OUTLEN / h)` must not exceed `2^COUNTER - 1` for the selected PRF. COUNTER denotes the counter width in bits, not a starting counter value.
- DUKPT-AES-INITIAL-KEY parameters contain only MECH. OUTLEN, PRF, LABEL and COUNTER are not supported; the BDK fixes the AES type and output length.
- DUKPT-AES-WORKING-KEY requires USAGE and KEYTYPE in addition to MECH. USAGE accepts PIN, MAC-GENERATE, MAC-VERIFY, MAC-BOTH, DATA-ENCRYPT, DATA-DECRYPT and DATA-BOTH. KEYTYPE accepts TDEA-2, TDEA-3, AES-128, AES-192, AES-256, HMAC-128, HMAC-192 and HMAC-256.
- USAGE: For DUKPT-AES-WORKING-KEY, selects the X9.24 Working-Key purpose and the restricted KeyUsagePolicy stored on the result. PIN maps only to PinEncrypt, not to generic Encrypt.
- KEYTYPE: For DUKPT-AES-WORKING-KEY, selects the Working-Key algorithm family and length. HMAC-128, HMAC-192 and HMAC-256 describe HMAC keying-material length, not a hash algorithm.
- DUKPT-TDEA-INITIAL-KEY parameters contain only MECH. OUTLEN, KEYTYPE, PRF and COUNTER are not supported; the mechanism requires a 16-byte double-length TDEA BDK and a complete 80-bit KSN.
- ep2 KDF parameter contracts:

| Mechanism | HASH | SALT | OUTLEN | VARIANT |
|-----------|------|------|--------|---------|
| KDF-EP2-SESSION | Not supported | Required, 32 Byte | Not supported | Required |
| KDF-EP2-PAN-RECEIPT-TRX | Not supported | Not supported | Not supported | Not supported |
| KDF-EP2-PAN-RECEIPT-TRM | Not supported | Required, 32 Byte | Not supported | Not supported |
| KDF-EP2-PAN-SURROGATE-TRX | Not supported | Not supported | Not supported | Not supported |

- VARIANT: Required only by KDF-EP2-SESSION. It accepts TC, MAC-SEND, MAC-RECEIVE, ENCRYPTION, PIN or KEY-ENCRYPTION. The selected variant fixes its HKDF info and output length.
- ep2 KDF mechanisms fix SHA-256 and their result lengths according to the respective ep2 profile. HASH and OUTLEN cannot be selected externally.
- NONCE: Unique nonce value for symmetric encryption used in certain modes.
- COUNTER: Counter value for symmetric encryption for certain modes.
- ADATA: Additional authenticated data used in certain modes.
- BLKH: TR-31 Key Block Header used by Wrap. The header is authenticated, and its declared total length must match the resulting key-block structure.
- RND: Optional random bytes used by TR-31 Wrap for key-length obfuscation and cipher-block padding. The required length depends on the wrapped-key algorithm and size; an omitted or wrong-sized value is replaced with generated random filler.
