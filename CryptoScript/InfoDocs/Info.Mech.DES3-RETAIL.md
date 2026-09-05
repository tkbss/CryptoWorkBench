# MECHANISM DES3-RETAIL

DES3-RETAIL calculates a Retail message authentication code (MAC). It processes padded data with DES-CBC under the first key component, starting with a zero chaining value. The final block is decrypted with the second component and encrypted with the third component, or the first component for a 16-byte key.

---

## Key Features

- **Block Cipher**: DES3 operates on fixed-size blocks (64 bits, 8 bytes) and supports keys of 16 or 24 bytes (128 or 192 bits including parity bits).
- **Key Requirements**: GenerateKey accepts 128 or 192 for a new key, or a hexadecimal key of exactly 16 or 24 bytes. Weak keys detected by the implementation, including keys that degenerate to single DES, are rejected.
- **Input Length**: Input not aligned to 8 bytes is padded using ISO-9797-M1 or ISO-9797-M2.
- **Return Value**: Mac returns a VAR containing 4 through 8 MAC bytes in hexadecimal 0x(...) format. Encrypt and Decrypt are rejected.

## Functions
The following functions are available for DES3-RETAIL mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey| N/A               | DES3-RETAIL, Key Length or hexadecimal key | KEY variable | 128/192 bits |
| Parameters | N/A               | #MECH, #PAD, #MACLEN | PARAM variable | N/A |
| Mac | #MECH, #PAD, #MACLEN | parameters, key, data | VAR MAC | 128/192 bits |

## Parameters
These parameters are used with the DES3-RETAIL mechanism:
- **#MECH**: Specifies the DES3-RETAIL mechanism in PARAM. A declared KEY must be a DES3 key; its DES3 mode does not have to match PARAM.
- **#IV**: Not used as an external parameter. A supplied IV is rejected; the internal chaining value always starts at zero.
- **#PAD**: Parameters supplies ISO-9797-M2 when omitted. Only these two paddings are supported:
    - ISO-9797-M1: Adds zero bytes only when needed to reach a multiple of 8 bytes. Aligned input receives no additional block. Empty input is rejected.
    - ISO-9797-M2: Adds 0x80 followed by zero bytes. Aligned input receives an additional block. Empty input is accepted.
    - NONE: Not supported and rejected, even for block-aligned input.
- **#MACLEN**: Output length from 4 through 8 bytes; default 8. Use a quoted decimal value such as #MACLEN:"4". In Parameters, write #MACLEN immediately after the comma without a space. The result contains the leftmost bytes of the full 8-byte MAC.

---
## Example Usage
### DES3-RETAIL MAC Calculation
KEY k0         = GenerateKey(DES3-RETAIL, 192)  
PARAM p0       = Parameters(DES3-RETAIL)  
VAR cleartext  = "This is a secret message."  
VAR mac        = Mac(p0, k0, cleartext)  
### DES3-RETAIL MAC Calculation with Reduced Output Length
KEY k1         = GenerateKey(DES3-RETAIL, 128)  
PARAM p1       = Parameters(#MECH:DES3-RETAIL, #PAD:ISO-9797-M2,#MACLEN:"4")  
VAR macshort   = Mac(p1, k1, 0x(0011223344556677))  

---
