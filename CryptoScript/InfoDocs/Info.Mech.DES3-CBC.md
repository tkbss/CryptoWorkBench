# MECHANISM DES3-CBC

DES3-CBC is a mode of operation for symmetric key cryptographic block ciphers with the Triple DES algorithm. It uses a secret key and an initialization vector (IV) to encrypt data in fixed-size blocks. Each plaintext block is XORed with the previous ciphertext block before encryption. The same mechanism also supports CBC-MAC through Mac.

---

## Key Features

- **Block Cipher**: DES3 operates on fixed-size blocks (64 bits, 8 bytes) and supports keys of 16 or 24 bytes (128 or 192 bits including parity bits).
- **Key Requirements**: GenerateKey accepts 128 or 192 for a new key, or a hexadecimal key of exactly 16 or 24 bytes. Weak keys detected by the implementation, including keys that degenerate to single DES, are rejected.
- **IV Usage**: Encrypt and Decrypt require the same 8-byte IV. Mac always uses an all-zero IV.
- **Return Value**: Encrypt, Decrypt and Mac return a VAR containing hexadecimal bytes in 0x(...) format. Decrypt returns plaintext bytes, including when the original input was text.

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
- **#MECH**: Specifies the DES3-CBC mechanism in PARAM. A declared KEY must be a DES3 key; its DES3 mode does not have to match PARAM.
- **#IV**: Initialization vector for encryption and decryption, exactly 8 bytes (64 bits). Parameters generates a random 8-byte IV when omitted. Mac ignores this parameter and always starts with an all-zero IV.
- **#PAD**: Padding scheme to ensure input data is a multiple of the 8-byte block size. Parameters supplies PKCS-7 when omitted.
    - NONE: No padding. Input must be a multiple of 8 bytes; non-aligned input is rejected.
    - PKCS-7: Default padding scheme. Adds padding even when input is already block-aligned.
    - ANSI-X923: Adds zero bytes followed by the padding length.
    - ISO-7816 and ISO-9797-M2: Add 0x80 followed by zero bytes, including an additional block for aligned input.
    - ISO-9797-M1: Adds zero bytes only when needed. Decrypt retains these bytes because the original length cannot be recovered from zero padding.
    - ISO-9797-M3: Adds zero bytes and an 8-byte field containing the original bit length.
    - TLS-CBC: Adds bytes containing the padding length minus one.
- **Empty Input**: Encrypt accepts an empty string. NONE and ISO-9797-M1 produce empty ciphertext; the other listed paddings produce at least one block. Decrypt requires non-empty ciphertext whose length is a multiple of 8 bytes.
- **#MACLEN**: Output length for Mac, from 4 through 8 bytes; default 8. Use a quoted decimal value such as #MACLEN:"4". In Parameters, write #MACLEN immediately after the comma without a space. The result contains the leftmost bytes of the last ciphertext block. Encrypt and Decrypt do not use this parameter.
- **CBC-MAC**: Use Mac with #MECH:DES3-CBC. DES3-CBC-MAC is not a separate mechanism name. The listed paddings also apply to Mac. Empty input is rejected with NONE or ISO-9797-M1 because no block is produced; the other listed paddings accept empty input.

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
