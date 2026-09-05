# MECHANISM DES3-ECB

DES3-ECB is a mode of operation for symmetric key cryptographic block ciphers with the Triple DES algorithm. It encrypts each plaintext block independently with the same secret key. It does not use an initialization vector (IV).

---

## Key Features

- **Block Cipher**: DES3 operates on fixed-size blocks (64 bits, 8 bytes) and supports keys of 16 or 24 bytes (128 or 192 bits including parity bits).
- **Key Requirements**: GenerateKey accepts 128 or 192 for a new key, or a hexadecimal key of exactly 16 or 24 bytes. Weak keys detected by the implementation, including keys that degenerate to single DES, are rejected.
- **Independent Blocks**: Identical plaintext blocks produce identical ciphertext blocks under the same key.
- **Return Value**: Encrypt and Decrypt return a VAR containing hexadecimal bytes in 0x(...) format. Decrypt returns plaintext bytes, including when the original input was text. Mac is not supported.

## Functions
The following functions are available for DES3-ECB mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey| N/A               | DES3-ECB, Key Length or hexadecimal key | KEY variable | 128/192 bits |
| Parameters | N/A               | #MECH, #PAD | PARAM variable | N/A |
| Encrypt | #MECH, #PAD | parameters, key, data | VAR ciphertext | 128/192 bits |
| Decrypt | #MECH, #PAD | parameters, key, encrypted data | VAR data | 128/192 bits |

## Parameters
These parameters are used with the DES3-ECB mechanism:
- **#MECH**: Specifies the DES3-ECB mechanism in PARAM. A declared KEY must be a DES3 key; its DES3 mode does not have to match PARAM.
- **#IV**: Not used. A supplied IV is rejected.
- **#PAD**: Padding scheme to ensure input data is a multiple of the 8-byte block size. Parameters supplies PKCS-7 when omitted.
    - NONE: No padding. Input must be a multiple of 8 bytes; non-aligned input is rejected.
    - PKCS-7: Default padding scheme. Adds padding even when input is already block-aligned.
    - ANSI-X923: Adds zero bytes followed by the padding length.
    - ISO-7816 and ISO-9797-M2: Add 0x80 followed by zero bytes, including an additional block for aligned input.
    - ISO-9797-M1: Adds zero bytes only when needed. Decrypt retains these bytes because the original length cannot be recovered from zero padding.
    - ISO-9797-M3: Adds zero bytes and an 8-byte field containing the original bit length.
    - TLS-CBC: Adds bytes containing the padding length minus one.
- **Empty Input**: Encrypt accepts an empty string. NONE and ISO-9797-M1 produce empty ciphertext; the other listed paddings produce at least one block. Decrypt requires non-empty ciphertext whose length is a multiple of 8 bytes.

---
## Example Usage
### DES3-ECB Encryption and Decryption
KEY k0         = GenerateKey(DES3-ECB, 192)  
PARAM p0       = Parameters(#MECH:DES3-ECB, #PAD:PKCS-7)  
VAR cleartext  = "This is a secret message."  
VAR ciphertext = Encrypt(p0, k0, cleartext)  
VAR decrypted  = Decrypt(p0, k0, ciphertext)  

---
