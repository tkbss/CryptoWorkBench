# MECHANISM DES3-CMAC

DES3-CMAC calculates a message authentication code (MAC) with the Triple DES algorithm. It uses a secret key and internal CMAC processing of the final block to authenticate data. It supports MAC calculation only.

---

## Key Features

- **Block Cipher**: DES3 operates on fixed-size blocks (64 bits, 8 bytes) and supports keys of 16 or 24 bytes (128 or 192 bits including parity bits).
- **Key Requirements**: GenerateKey accepts 128 or 192 for a random, non-degenerate new key, or explicitly supplied hexadecimal key bytes of exactly 16 or 24 bytes. Explicit imports retain their original bytes, including parity errors and weak keys.
- **Workbench diagnostics**: General Encrypt, Decrypt and Mac operations report conflicting KeyType/KeyUsage metadata as non-blocking warnings. DES parity errors produce DesParityViolation; known weak or semi-weak DES components and effective K1=K2 or K2=K3 degeneration produce WeakDes3Key. Ordinary two-key TDEA (K1=K3) is not alone a single-DES degeneration. At most one warning per quality category is reported for each operation; no key bytes are printed.
- **Analysis scope**: Technically valid test keys remain usable without changing their bytes or metadata. These warnings do not establish suitability for production payment systems. Key format/length, permitted functions, IV, padding, data length and MAC length requirements remain binding. The import API has no diagnostic sink; quality warnings are emitted on subsequent general DES3 use when a sink is available. PIN-block operations also report key metadata and TDEA quality conflicts as non-blocking warnings while retaining technical PIN-block format validation. TR-31, DUKPT and EMV validation policies are unchanged.
- **Input Length**: Empty input and input not aligned to 8 bytes are supported. CMAC handles the final block internally.
- **Return Value**: Mac returns a VAR containing the full 8-byte MAC in hexadecimal 0x(...) format. Encrypt and Decrypt are not implemented for this mechanism.

### Key Generation and Import

GenerateKey(DES3-CMAC, ...) supports random generation and explicit hexadecimal import. The registry fixes the resulting KeyType to Secret(Tdea) for both variants. Valid key lengths: 128 or 192 bits (16 or 24 bytes). Imported bytes are preserved exactly. The mechanism selects the key algorithm and does not restrict the resulting KEY to its original mode or HMAC digest; other compatible mechanisms of the same family may consume it. External weak keys and parity errors retain their original bytes. Random generation keeps the existing non-degenerate TripleDES generator.

## Functions
The following functions are available for DES3-CMAC mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey| N/A               | DES3-CMAC, Key Length or hexadecimal key | KEY variable | 128/192 bits |
| Parameters | N/A               | #MECH, #PAD | PARAM variable | N/A |
| Mac | #MECH, #PAD | parameters, key, data | VAR MAC | 128/192 bits |

## Parameters
These parameters are used with the DES3-CMAC mechanism:
- **#MECH**: Specifies the DES3-CMAC mechanism in PARAM. The operation requires technically valid TDEA key bytes of exactly 16 or 24 bytes; the declared KEY algorithm need not match. A conflicting declared KeyType produces a non-blocking KeyTypeConflict warning, and conflicting KeyUsage produces a non-blocking KeyUsageConflict warning, when a diagnostic sink is available. Neither warning changes the bytes or metadata. Unknown algorithm and Unspecified usage cause no corresponding conflict warning. Technical validation remains mandatory; PARAM selects the mechanism, not the KEY metadata.
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
