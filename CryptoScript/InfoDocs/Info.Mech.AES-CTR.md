# MECHANISM AES-CTR

AES-CTR encrypts a nonce and counter with AES, then XORs the resulting bytes with the input. Encrypt and Decrypt use the same operation.

---

## Key Features

- **Block Cipher**: AES operates on fixed-size blocks (128 bits, 16 bytes) and supports key sizes of 128, 192, or 256 bits.
- **Key Requirements**: GenerateKey accepts 128, 192 or 256 for a new key, or a hexadecimal key of exactly 16, 24 or 32 bytes.
- **Input Length**: Any byte length, including empty input, is accepted. Output has the same length as input.
- **Counter Processing**: Each call starts with the supplied counter. The PARAM variable is not advanced by encryption or decryption.
- **Return Value**: Encrypt and Decrypt return a VAR containing hexadecimal bytes in 0x(...) format. Decrypt returns plaintext bytes, including when the original input was text.

## Functions
The following functions are available for AES-CTR mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey | N/A | AES-CTR, Key Length or hexadecimal key | KEY variable | 128/192/256 bits |
| Parameters | N/A | #MECH, #NONCE, #COUNTER | PARAM variable | N/A |
| Encrypt | #MECH, #NONCE, #COUNTER | parameters, key, data | VAR ciphertext | 128/192/256 bits |
| Decrypt | #MECH, #NONCE, #COUNTER | parameters, key, encrypted data | VAR data | 128/192/256 bits |

## Parameters
These parameters are used with the AES-CTR mechanism:
- **#MECH**: Specifies AES-CTR in PARAM. The AES mode assigned to KEY need not match the mode in PARAM; valid AES key bytes are required.
- **#NONCE**: Parameters supplies 12 random bytes when omitted. Use 12 bytes with the 4-byte counter to form one 16-byte AES input block. The implementation does not enforce exactly 12 nonce bytes: shorter combined input fails for non-empty data, and bytes beyond the first 16 combined bytes are ignored.
- **#COUNTER**: Four bytes, default 0x(00000000). Incremented as an unsigned big-endian value after every processed block, including a partial final block. Overflow wraps to zero without an error. Counter length is checked during increment; empty input bypasses this check.
- **#IV**: Not used; the input block is formed from #NONCE followed by #COUNTER.
- **#PAD**: No padding is applied, regardless of this parameter. NONE does not impose block alignment.
- **Decryption**: Use the same key, nonce and starting counter as for encryption.

---
## Example Usage
### AES-CTR Encryption and Decryption
KEY k0         = GenerateKey(AES-CTR, 256)  
PARAM p0       = Parameters(#MECH:AES-CTR, #NONCE:0x(00112233445566778899AABB), #COUNTER:0x(00000000))  
VAR cleartext  = "This is a secret message."  
VAR ciphertext = Encrypt(p0, k0, cleartext)  
VAR decrypted  = Decrypt(p0, k0, ciphertext)  
### AES-CTR Empty Input
VAR emptycipher = Encrypt(p0, k0, "")  
VAR emptyplain  = Decrypt(p0, k0, emptycipher)  

---
