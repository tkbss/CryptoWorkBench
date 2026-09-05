# MECHANISM AES-ECB

AES-ECB encrypts each plaintext block independently with the AES algorithm and the same secret key.

---

## Key Features

- **Block Cipher**: AES operates on fixed-size blocks (128 bits, 16 bytes) and supports key sizes of 128, 192, or 256 bits.
- **Key Requirements**: GenerateKey accepts 128, 192 or 256 for a new key, or a hexadecimal key of exactly 16, 24 or 32 bytes.
- **Independent Blocks**: Identical plaintext blocks produce identical ciphertext blocks under the same key.
- **Input Length**: Encrypt and Decrypt require multiples of 16 bytes. Empty input is accepted and produces empty output.
- **Return Value**: Encrypt and Decrypt return a VAR containing hexadecimal bytes in 0x(...) format. Decrypt returns plaintext bytes, including when the original input was text.

## Functions
The following functions are available for AES-ECB mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey | N/A | AES-ECB, Key Length or hexadecimal key | KEY variable | 128/192/256 bits |
| Parameters | N/A | #MECH, #PAD | PARAM variable | N/A |
| Encrypt | #MECH, #PAD | parameters, key, data | VAR ciphertext | 128/192/256 bits |
| Decrypt | #MECH, #PAD | parameters, key, encrypted data | VAR data | 128/192/256 bits |

## Parameters
These parameters are used with the AES-ECB mechanism:
- **#MECH**: Specifies AES-ECB in PARAM. The AES mode assigned to KEY need not match the mode in PARAM; valid AES key bytes are required.
- **#IV**: Not used. Parameters clears a supplied IV; the cipher itself ignores it.
- **#PAD**: Always NONE. Parameters replaces a supplied padding with NONE, and Encrypt and Decrypt do not apply or remove padding even with directly declared parameters. Non-aligned input is rejected.

---
## Example Usage
### AES-ECB Encryption and Decryption
KEY k0         = GenerateKey(AES-ECB, 256)  
PARAM p0       = Parameters(AES-ECB)  
VAR cleartext  = 0x(00112233445566778899AABBCCDDEEFF)  
VAR ciphertext = Encrypt(p0, k0, cleartext)  
VAR decrypted  = Decrypt(p0, k0, ciphertext)  
### AES-ECB Empty Input
VAR emptycipher = Encrypt(p0, k0, "")  
VAR emptyplain  = Decrypt(p0, k0, emptycipher)  

---
