# MECHANISM AES-GCM

AES-GCM provides authenticated encryption with AES. It encrypts plaintext and authenticates both the ciphertext and optional associated data (AAD).

---

## Key Features

- **Block Cipher**: AES operates on fixed-size blocks (128 bits, 16 bytes) and supports key sizes of 128, 192, or 256 bits.
- **Key Requirements**: GenerateKey accepts 128, 192 or 256 for a new key, or a hexadecimal key of exactly 16, 24 or 32 bytes.
- **Input Length**: No block alignment is required. Empty plaintext is accepted and produces only the 16-byte tag.
- **Authentication**: Decrypt verifies the appended tag. A changed key, nonce, associated data or authenticated input causes authentication failure.
- **Return Value**: Encrypt and Decrypt return a VAR containing hexadecimal bytes in 0x(...) format. Decrypt returns plaintext bytes, including when the original input was text. Encrypt returns ciphertext followed by the tag; the tag is also stored in the result's GMAC property.

## Functions
The following functions are available for AES-GCM mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey | N/A | AES-GCM, Key Length or hexadecimal key | KEY variable | 128/192/256 bits |
| Parameters | N/A | #MECH, #NONCE, #ADATA | PARAM variable | N/A |
| Encrypt | #MECH, #NONCE, #ADATA | parameters, key, data | VAR ciphertext and tag | 128/192/256 bits |
| Decrypt | #MECH, #NONCE, #ADATA | parameters, key, encrypted data and tag | VAR data | 128/192/256 bits |

## Parameters
These parameters are used with the AES-GCM mechanism:
- **#MECH**: Specifies AES-GCM in PARAM. The AES mode assigned to KEY need not match the mode in PARAM; valid AES key bytes are required.
- **#NONCE**: Required and non-empty. Parameters(AES-GCM) generates 12 random bytes. With additional parameters, an omitted nonce is also generated. Other non-empty nonce lengths are accepted by the underlying GCM implementation.
- **#ADATA**: Optional associated data, authenticated but not encrypted. Parameters(AES-GCM) supplies "DEFAULT_GCM_AUTHENTICATION_DATA". With additional parameters, omitted ADATA means no associated data. Decrypt must use the same associated data as Encrypt.
- **#IV**: Not used; supply #NONCE instead.
- **#PAD**: No external block padding is applied. NONE does not require block alignment; supplying another padding does not enable it.
- **Authentication Tag**: Fixed at 16 bytes (128 bits), appended to the ciphertext. #MACLEN does not change it. Pass the complete ciphertext and tag to Decrypt; input shorter than the tag or an invalid tag is rejected.

---
## Example Usage
### AES-GCM Encryption and Decryption
KEY k0         = GenerateKey(AES-GCM, 256)  
PARAM p0       = Parameters(#MECH:AES-GCM, #NONCE:0x(00112233445566778899AABB), #ADATA:"Example authentication data")  
VAR cleartext  = "This is a secret message."  
VAR ciphertext = Encrypt(p0, k0, cleartext)  
VAR decrypted  = Decrypt(p0, k0, ciphertext)  
### AES-GCM Empty Input
PARAM pempty    = Parameters(AES-GCM)  
VAR emptycipher = Encrypt(pempty, k0, "")  
VAR emptyplain  = Decrypt(pempty, k0, emptycipher)  

---
