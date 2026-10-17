# MECHANISM DES3-CBC

DES3-CBC is a mode of operation for symmetric key cryptographic block ciphers with the Triple DES algorithm. It uses a secret key and an initialization vector (IV) to encrypt data in fixed-size blocks. Each plaintext block is XORed with the previous ciphertext block before encryption. The same mechanism also supports CBC-MAC through Mac.

---

## Key Features

- **Block Cipher**: DES3 operates on fixed-size blocks (64 bits, 8 bytes) and supports keys of 16 or 24 bytes (128 or 192 bits including parity bits).
- **Key Requirements**: GenerateKey accepts 128 or 192 for a random, non-degenerate new key, or explicitly supplied hexadecimal key bytes of exactly 16 or 24 bytes. Explicit imports retain their original bytes, including parity errors and weak keys.
- **Workbench diagnostics**: General Encrypt, Decrypt and Mac operations report conflicting KeyType/KeyUsage metadata as non-blocking warnings. DES parity errors produce DesParityViolation; known weak or semi-weak DES components and effective K1=K2 or K2=K3 degeneration produce WeakDes3Key. Ordinary two-key TDEA (K1=K3) is not alone a single-DES degeneration. At most one warning per quality category is reported for each operation; no key bytes are printed.
- **Analysis scope**: Technically valid test keys remain usable without changing their bytes or metadata. These warnings do not establish suitability for production payment systems. Key format/length, permitted functions, IV, padding, data length and MAC length requirements remain binding. The import API has no diagnostic sink; quality warnings are emitted on subsequent general DES3 use when a sink is available. PIN-block operations also report key metadata and TDEA quality conflicts as non-blocking warnings while retaining technical PIN-block format validation. TR-31, DUKPT and EMV validation policies are unchanged.
- **IV Usage**: Encrypt and Decrypt require the same 8-byte IV. Mac always uses an all-zero IV.
- **Return Value**: Encrypt, Decrypt and Mac return a VAR containing hexadecimal bytes in 0x(...) format. Decrypt returns plaintext bytes, including when the original input was text.

### Key Generation and Import

GenerateKey(DES3-CBC, ...) supports random generation and explicit hexadecimal import. The registry fixes the resulting KeyType to Secret(Tdea) for both variants. Valid key lengths: 128 or 192 bits (16 or 24 bytes). Imported bytes are preserved exactly. The mechanism selects the key algorithm and does not restrict the resulting KEY to its original mode or HMAC digest; other compatible mechanisms of the same family may consume it. External weak keys and parity errors retain their original bytes. Random generation keeps the existing non-degenerate TripleDES generator.

## Functions
The following functions are available for DES3-CBC mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey| N/A               | DES3-CBC, Key Length or hexadecimal key | KEY variable | 128/192 bits |
| Parameters | N/A               | #MECH, #IV, #PAD, #MACLEN | PARAM variable | N/A |
| Encrypt | #MECH, #IV, #PAD | parameters, key, data | VAR ciphertext | 128/192 bits |
| Decrypt | #MECH, #IV, #PAD | parameters, key, encrypted data | VAR data | 128/192 bits |
| Mac | #MECH, #PAD, #MACLEN | parameters, key, data | VAR MAC | 128/192 bits |

## Parameters
These parameters are used with the DES3-CBC mechanism:
- **#MECH**: Specifies the DES3-CBC mechanism in PARAM. The operation requires technically valid TDEA key bytes of exactly 16 or 24 bytes; the declared KEY algorithm need not match. A conflicting declared KeyType produces a non-blocking KeyTypeConflict warning, and conflicting KeyUsage produces a non-blocking KeyUsageConflict warning, when a diagnostic sink is available. Neither warning changes the bytes or metadata. Unknown algorithm and Unspecified usage cause no corresponding conflict warning. Technical validation remains mandatory; PARAM selects the mechanism, not the KEY metadata.
- **#IV**: Initialization vector for encryption and decryption, exactly 8 bytes (64 bits). Parameters generates a random 8-byte IV when omitted. Mac ignores this parameter and always starts with an all-zero IV.
- **#PAD**: Padding scheme to ensure input data is a multiple of the 8-byte block size. Parameters supplies PKCS-7 when omitted.
    - NONE: No padding. Input must be a non-zero multiple of 8 bytes; empty and non-aligned input is rejected.
    - PKCS-7: Default padding scheme. Adds padding even when input is already block-aligned.
    - ANSI-X923: Adds zero bytes followed by the padding length.
    - ISO-10126: Adds random filler bytes followed by the padding length.
    - ISO-7816 and ISO-9797-M2: Add 0x80 followed by zero bytes, including an additional block for aligned input.
    - ISO-9797-M1: Adds zero bytes only when needed. Decrypt retains these bytes because the original length cannot be recovered from zero padding.
    - ISO-9797-M3: Zero-pads the data to a positive number of complete blocks, then prepends an 8-byte block containing the original bit length in the ISO-defined representation.
    - TLS-CBC: Adds bytes containing the padding length minus one.
- **Empty Input**: Encrypt rejects an empty string with NONE. ISO-9797-M1 and the other listed padding schemes produce at least one ciphertext block. Decrypt requires non-empty ciphertext whose length is a multiple of 8 bytes.
- **#MACLEN**: Output length for Mac, from 4 through 8 bytes; default 8. Use a quoted decimal value such as #MACLEN:"4". In Parameters, write #MACLEN immediately after the comma without a space. The result contains the leftmost bytes of the last ciphertext block. Encrypt and Decrypt do not use this parameter.
- **CBC-MAC**: Use Mac with #MECH:DES3-CBC. DES3-CBC-MAC is not a separate mechanism name. The listed paddings except ISO-10126 also apply to Mac; ISO-10126 is supported only by Encrypt and Decrypt and is explicitly rejected by CBC-MAC. Empty input is rejected with NONE because no block is produced; ISO-9797-M1 and the other supported padding schemes that produce a block accept empty input.

---
## Example Usage
### DES3-CBC Encryption and Decryption
KEY k0         = GenerateKey(DES3-CBC, 192)  
PARAM p0       = Parameters(#MECH:DES3-CBC, #IV:0x(0011223344556677), #PAD:PKCS-7)  
VAR cleartext  = "This is a secret message."  
VAR ciphertext = Encrypt(p0, k0, cleartext)  
VAR decrypted  = Decrypt(p0, k0, ciphertext)  
### DES3-CBC MAC Calculation with Reduced Output Length
KEY k1         = GenerateKey(DES3-CBC, 128)  
PARAM p1       = Parameters(#MECH:DES3-CBC, #PAD:ISO-9797-M2,#MACLEN:"4")  
VAR macshort   = Mac(p1, k1, 0x(0011223344556677))  

---
