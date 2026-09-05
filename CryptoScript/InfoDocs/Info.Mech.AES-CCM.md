# MECHANISM AES-CCM

AES-CCM provides authenticated encryption with AES. It encrypts plaintext and authenticates both the ciphertext and optional associated data (AAD).

---

## Key Features

- **Block Cipher**: AES operates on fixed-size blocks (128 bits, 16 bytes) and supports key sizes of 128, 192, or 256 bits.
- **Key Requirements**: GenerateKey accepts 128, 192 or 256 for a new key, or a hexadecimal key of exactly 16, 24 or 32 bytes.
- **Input Length**: No block alignment is required. Empty plaintext is accepted and produces only the 16-byte tag.
- **Authentication**: Decrypt verifies the appended tag. A changed key, nonce, associated data or authenticated input causes authentication failure.
- **Return Value**: Encrypt and Decrypt return a VAR containing hexadecimal bytes in 0x(...) format. Decrypt returns plaintext bytes, including when the original input was text. Encrypt returns ciphertext followed by the tag; the tag is also stored in the result's GMAC property.

## Functions
The following functions are available for AES-CCM mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey | N/A | AES-CCM, Key Length or hexadecimal key | KEY variable | 128/192/256 bits |
| Parameters | N/A | #MECH, #NONCE, #ADATA | PARAM variable | N/A |
| Encrypt | #MECH, #NONCE, #ADATA | parameters, key, data | VAR ciphertext and tag | 128/192/256 bits |
| Decrypt | #MECH, #NONCE, #ADATA | parameters, key, encrypted data and tag | VAR data | 128/192/256 bits |

## Parameters
These parameters are used with the AES-CCM mechanism:
- **#MECH**: Specifies AES-CCM in PARAM. The AES mode assigned to KEY need not match the mode in PARAM; valid AES key bytes are required.
- **#NONCE**: Required and non-empty. Parameters(AES-CCM) generates 12 random bytes. With additional parameters, no nonce is generated; supply it explicitly. The underlying CCM implementation requires 7 through 13 bytes.
- **#ADATA**: Optional associated data, authenticated but not encrypted. Parameters(AES-CCM) supplies "DEFAULT_CCM_AUTHENTICATION_DATA". With additional parameters, omitted ADATA means no associated data. Decrypt must use the same associated data as Encrypt.
- **#IV**: Not used; supply #NONCE instead.
- **#PAD**: No external block padding is applied. NONE does not require block alignment; supplying another padding does not enable it.
- **Authentication Tag**: Fixed at 16 bytes (128 bits), appended to the ciphertext. #MACLEN does not change it. Pass the complete ciphertext and tag to Decrypt; input shorter than the tag or an invalid tag is rejected.
- **Message Length**: CCM uses a length field of 15 minus the nonce length in bytes. With a 13-byte nonce, plaintext must be shorter than 65,536 bytes; with 12 bytes, shorter than 16,777,216 bytes. Input is also limited by the in-memory byte arrays used by CryptoScript.

---
## Example Usage
### AES-CCM Encryption and Decryption
KEY k0         = GenerateKey(AES-CCM, 256)  
PARAM p0       = Parameters(#MECH:AES-CCM, #NONCE:0x(00112233445566778899AABB), #ADATA:"Example authentication data")  
VAR cleartext  = "This is a secret message."  
VAR ciphertext = Encrypt(p0, k0, cleartext)  
VAR decrypted  = Decrypt(p0, k0, ciphertext)  
### AES-CCM Empty Input
PARAM pempty    = Parameters(AES-CCM)  
VAR emptycipher = Encrypt(pempty, k0, "")  
VAR emptyplain  = Decrypt(pempty, k0, emptycipher)  

---
