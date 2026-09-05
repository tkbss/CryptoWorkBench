# MECHANISM AES-GMAC

AES-GMAC calculates a message authentication code (MAC) using AES-GCM with a nonce. The data argument is authenticated without being encrypted.

---

## Key Features

- **Block Cipher**: AES operates on fixed-size blocks (128 bits, 16 bytes) and supports key sizes of 128, 192, or 256 bits.
- **Key Requirements**: GenerateKey accepts 128, 192 or 256 for a new key, or a hexadecimal key of exactly 16, 24 or 32 bytes.
- **Input Length**: Any byte length, including empty input, is accepted.
- **MAC Length**: Mac returns a full 16-byte authentication tag.
- **Return Value**: Mac returns a VAR containing hexadecimal MAC bytes in 0x(...) format.

## Functions
The following functions are available for AES-GMAC mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey | N/A | AES-GMAC, Key Length or hexadecimal key | KEY variable | 128/192/256 bits |
| Parameters | N/A | #MECH, #NONCE | PARAM variable | N/A |
| Mac | #MECH, #NONCE | parameters, key, data | VAR MAC | 128/192/256 bits |

## Parameters
These parameters are used with the AES-GMAC mechanism:
- **#MECH**: Specifies AES-GMAC in PARAM. The AES mode assigned to KEY need not match the mode in PARAM; valid AES key bytes are required.
- **#NONCE**: Required and non-empty. Parameters(AES-GMAC) generates 12 random bytes. When additional parameters are supplied, no nonce default is added; supply #NONCE explicitly. The underlying GCM implementation also accepts other non-empty nonce lengths.
- **#IV**: Not used as a separate parameter; GMAC uses #NONCE.
- **#PAD**: No external padding is applied. The default NONE does not require block alignment.
- **#ADATA**: Not processed separately. Pass the data to authenticate as the third argument of Mac.
- **#MACLEN**: Does not change the fixed 16-byte result.
- **Operations**: Encrypt and Decrypt reject this mechanism.

---
## Example Usage
### AES-GMAC MAC Calculation
KEY k0         = GenerateKey(AES-GMAC, 256)  
PARAM p0       = Parameters(#MECH:AES-GMAC, #NONCE:0x(00112233445566778899AABB))  
VAR cleartext  = "This is a secret message."  
VAR mac        = Mac(p0, k0, cleartext)  
### AES-GMAC Empty Input
PARAM pempty   = Parameters(AES-GMAC)  
VAR emptymac   = Mac(pempty, k0, "")  

---
