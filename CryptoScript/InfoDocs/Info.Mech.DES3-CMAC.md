# MECHANISM DES3-CMAC

DES3-CMAC calculates a message authentication code (MAC) with the Triple DES algorithm. It uses a secret key and internal CMAC processing of the final block to authenticate data. It supports MAC calculation only.

---

## Key Features

- **Block Cipher**: DES3 operates on fixed-size blocks (64 bits, 8 bytes) and supports keys of 16 or 24 bytes (128 or 192 bits including parity bits).
- **Key Requirements**: GenerateKey accepts 128 or 192 for a new key, or a hexadecimal key of exactly 16 or 24 bytes. Weak keys detected by the implementation, including keys that degenerate to single DES, are rejected.
- **Input Length**: Empty input and input not aligned to 8 bytes are supported. CMAC handles the final block internally.
- **Return Value**: Mac returns a VAR containing the full 8-byte MAC in hexadecimal 0x(...) format. Encrypt and Decrypt are not implemented for this mechanism.

## Functions
The following functions are available for DES3-CMAC mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey| N/A               | DES3-CMAC, Key Length or hexadecimal key | KEY variable | 128/192 bits |
| Parameters | N/A               | #MECH, #PAD | PARAM variable | N/A |
| Mac | #MECH, #PAD | parameters, key, data | VAR MAC | 128/192 bits |

## Parameters
These parameters are used with the DES3-CMAC mechanism:
- **#MECH**: Specifies the DES3-CMAC mechanism in PARAM. A declared KEY must be a DES3 key; its DES3 mode does not have to match PARAM.
- **#IV**: Not used. Parameters rejects a supplied IV.
- **#PAD**: Parameters sets NONE, even if another padding is supplied. NONE means no external padding; CMAC still handles partial and empty final blocks internally, so input need not be block-aligned.
- **MAC Length**: Always 8 bytes. #MACLEN does not truncate the result and is not a configurable CMAC output length.

---
## Example Usage
### DES3-CMAC MAC Calculation
KEY k0         = GenerateKey(DES3-CMAC, 192)  
PARAM p0       = Parameters(DES3-CMAC)  
VAR cleartext  = "This is a secret message."  
VAR mac        = Mac(p0, k0, cleartext)  
### DES3-CMAC MAC Calculation with Empty Input
VAR emptymac   = Mac(p0, k0, "")  

---
