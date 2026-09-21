# Mechnisms

List of all cryptograhic algorithms implemented in CRYPTO-SCRIPT.   

---
Every mechanism is specifying a certain cryptographic algorithm. Detailed information about each mechanism can be obtained through Info(mechanism).

- RSA-PSS : Asymmetric Signature algorithm based on the RSA concept.
- RSA-OAEP : Asymmetric Encryption algorithm based on the RSA concept.
- ECDSA : Asymmetric Elliptic Curve Digital Signature Algorithm.
- AES-CBC : Symmetric Advanced Encryption Standard in Cipher Block Chaining mode.
- AES-CTR : Symmetric Advanced Encryption Standard in Counter mode.
- AES-GCM : Symmetric Advanced Encryption Standard in Galois/Counter mode.
- AES-ECB : Symmetric Advanced Encryption Standard in Electronic Codebook mode.
- AES-CMAC : Symmetric Advanced Encryption Standard in Cipher-based Message Authentication Code mode.
- AES-CCM : Symmetric Advanced Encryption Standard in Counter with CBC-MAC mode.
- AES-GMAC : Symmetric Advanced Encryption Standard in Galois/Counter mode.
- DES3-ECB : Symmetric Triple Data Encryption Standard in Electronic Codebook mode.
- DES3-CBC : Symmetric Triple Data Encryption Standard in Cipher Block Chaining mode.
- DES3-RETAIL : Symmetric Triple Data Encryption Standard in Retail mode.
- DES3-CMAC : Symmetric Triple Data Encryption Standard in Cipher-based Message Authentication Code mode.
- HMAC-SHA1 : Keyed-Hash Message Authentication Code using SHA-1.
- HMAC-SHA224 : Keyed-Hash Message Authentication Code using SHA-224.
- HMAC-SHA256 : Keyed-Hash Message Authentication Code using SHA-256.
- HMAC-SHA384 : Keyed-Hash Message Authentication Code using SHA-384.
- HMAC-SHA512 : Keyed-Hash Message Authentication Code using SHA-512.
- HMAC-SHA512-224 : Keyed-Hash Message Authentication Code using SHA-512/224.
- HMAC-SHA512-256 : Keyed-Hash Message Authentication Code using SHA-512/256.
- HMAC-SHA3-224 : Keyed-Hash Message Authentication Code using SHA3-224.
- HMAC-SHA3-256 : Keyed-Hash Message Authentication Code using SHA3-256.
- HMAC-SHA3-384 : Keyed-Hash Message Authentication Code using SHA3-384.
- HMAC-SHA3-512 : Keyed-Hash Message Authentication Code using SHA3-512.
- HASH-SHA1 : Unkeyed message digest using SHA-1.
- HASH-SHA224 : Unkeyed message digest using SHA-224.
- HASH-SHA256 : Unkeyed message digest using SHA-256.
- HASH-SHA384 : Unkeyed message digest using SHA-384.
- HASH-SHA512 : Unkeyed message digest using SHA-512.
- HASH-SHA512-224 : Unkeyed message digest using SHA-512/224.
- HASH-SHA512-256 : Unkeyed message digest using SHA-512/256.
- HASH-SHA3-224 : Unkeyed message digest using SHA3-224.
- HASH-SHA3-256 : Unkeyed message digest using SHA3-256.
- HASH-SHA3-384 : Unkeyed message digest using SHA3-384.
- HASH-SHA3-512 : Unkeyed message digest using SHA3-512.
- KDF-HKDF : HMAC-based Extract-and-Expand Key Derivation Function specified in RFC 5869.
- HKDF-EXTRACT : RFC 5869 HKDF Extract operation only; returns the pseudorandom key (PRK).
- HKDF-EXPAND : RFC 5869 HKDF Expand operation only; derives output keying material from an existing PRK.
- DUKPT-AES-INITIAL-KEY : ANSI X9.24-3-2017 derivation of an AES DUKPT Initial Key from an AES BDK and 64-bit IKID.
- KDF-EP2-SESSION : ep2 8.11 Extract-and-Expand derivation of a selected Session Key Variant.
- KDF-EP2-PAN-RECEIPT-TRX : ep2 8.12 direct Expand using SHA-256(DOL) as info and returning the leftmost 16 of 32 bytes.
- KDF-EP2-PAN-RECEIPT-TRM : ep2 8.13 Extract-and-Expand using SHA-256(Terminal Properties) as info and returning the leftmost 16 of 32 bytes.
- KDF-EP2-PAN-SURROGATE-TRX : ep2 8.14 direct Expand using raw DOL as info and returning all 32 bytes.
- WRAP-AES-TR31 : TR-31 Version D key wrapping with AES Key Derivation Binding.
- WRAP-DES3-TR31 : TR-31 Version A/B/C key wrapping with TDEA Variant or Derivation Binding.
- WRAP-AES : Symmetric Key wrapping algorithm using AES.
- WRAP-DES3 : Symmetric Key wrapping algorithm using DES3.

## ep2 KDF Comparison

| ep2 | Extract | Info | Result |
|-----|---------|------|--------|
| 8.11 | yes | variant constant | 16/32 Byte |
| 8.12 | no | SHA-256(DOL) | leftmost 16 of 32 Byte |
| 8.13 | yes | SHA-256(Terminal Properties) | leftmost 16 of 32 Byte |
| 8.14 | no | raw DOL | full 32 Byte |
