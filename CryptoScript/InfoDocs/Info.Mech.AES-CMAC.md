# MECHANISM AES-CMAC

AES-CMAC calculates a message authentication code (MAC) using AES and internal processing of the final block. It supports Mac rather than encryption or decryption.

---

## Key Features

- **Block Cipher**: AES operates on fixed-size blocks (128 bits, 16 bytes) and supports key sizes of 128, 192, or 256 bits.
- **Key Requirements**: GenerateKey accepts 128, 192 or 256 for a new key, or a hexadecimal key of exactly 16, 24 or 32 bytes.
- **Input Length**: Empty input and non-aligned input are accepted. CMAC handles the final block internally.
- **MAC Length**: Mac always returns the full 16-byte MAC.
- **Return Value**: Mac returns a VAR containing hexadecimal MAC bytes in 0x(...) format.

## Functions
The following functions are available for AES-CMAC mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey | N/A | AES-CMAC, Key Length or hexadecimal key | KEY variable | 128/192/256 bits |
| Parameters | N/A | #MECH, #PAD | PARAM variable | N/A |
| Mac | #MECH, #PAD | parameters, key, data | VAR MAC | 128/192/256 bits |

## Parameters
These parameters are used with the AES-CMAC mechanism:
- **#MECH**: Specifies AES-CMAC in PARAM. The AES mode assigned to KEY need not match the mode in PARAM; valid AES key bytes are required.
- **#IV**: Not used. Parameters clears a supplied IV.
- **#PAD**: Parameters sets NONE, even if another padding is supplied. NONE means no external padding; internal CMAC processing still handles empty and partial final blocks. Mac ignores external padding.
- **#MACLEN**: Does not change the output length. MAC truncation is not implemented.
- **Operations**: Encrypt and Decrypt reject this mechanism.

---
## Example Usage
### AES-CMAC MAC Calculation
KEY k0         = GenerateKey(AES-CMAC, 256)  
PARAM p0       = Parameters(AES-CMAC)  
VAR cleartext  = "This is a secret message."  
VAR mac        = Mac(p0, k0, cleartext)  
### AES-CMAC Empty Input
VAR emptymac   = Mac(p0, k0, "")  

---
