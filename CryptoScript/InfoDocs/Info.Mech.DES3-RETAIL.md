# MECHANISM DES3-RETAIL

DES3-RETAIL calculates a Retail message authentication code (MAC). It processes padded data with DES-CBC under the first key component, starting with a zero chaining value. The final block is decrypted with the second component and encrypted with the third component, or the first component for a 16-byte key.

---

## Key Features

- **Block Cipher**: DES3 operates on fixed-size blocks (64 bits, 8 bytes) and supports keys of 16 or 24 bytes (128 or 192 bits including parity bits).
- **Key Requirements**: GenerateKey accepts 128 or 192 for a random, non-degenerate new key, or explicitly supplied hexadecimal key bytes of exactly 16 or 24 bytes. Explicit imports retain their original bytes, including parity errors and weak keys.
- **Workbench diagnostics**: General Encrypt, Decrypt and Mac operations report conflicting KeyType/KeyUsage metadata as non-blocking warnings. DES parity errors produce DesParityViolation; known weak or semi-weak DES components and effective K1=K2 or K2=K3 degeneration produce WeakDes3Key. Ordinary two-key TDEA (K1=K3) is not alone a single-DES degeneration. At most one warning per quality category is reported for each operation; no key bytes are printed.
- **Analysis scope**: Technically valid test keys remain usable without changing their bytes or metadata. These warnings do not establish suitability for production payment systems. Key format/length, permitted functions, IV, padding, data length and MAC length requirements remain binding. The import API has no diagnostic sink; quality warnings are emitted on subsequent general DES3 use when a sink is available. PIN-block operations also report key metadata and TDEA quality conflicts as non-blocking warnings while retaining technical PIN-block format validation. TR-31, DUKPT and EMV validation policies are unchanged.
- **Input Length**: Input not aligned to 8 bytes is padded using ISO-9797-M1 or ISO-9797-M2.
- **Return Value**: Mac returns a VAR containing 4 through 8 MAC bytes in hexadecimal 0x(...) format. Encrypt and Decrypt are rejected.

### Key Generation and Import

GenerateKey(DES3-RETAIL, ...) supports random generation and explicit hexadecimal import. The registry fixes the resulting KeyType to Secret(Tdea) for both variants. Valid key lengths: 128 or 192 bits (16 or 24 bytes). Imported bytes are preserved exactly. The mechanism selects the key algorithm and does not restrict the resulting KEY to its original mode or HMAC digest; other compatible mechanisms of the same family may consume it. External weak keys and parity errors retain their original bytes. Random generation keeps the existing non-degenerate TripleDES generator.

## Functions
The following functions are available for DES3-RETAIL mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey| N/A               | DES3-RETAIL, Key Length or hexadecimal key | KEY variable | 128/192 bits |
| Parameters | N/A               | #MECH, #PAD, #MACLEN | PARAM variable | N/A |
| Mac | #MECH, #PAD, #MACLEN | parameters, key, data | VAR MAC | 128/192 bits |

## Parameters
These parameters are used with the DES3-RETAIL mechanism:
- **#MECH**: Specifies the DES3-RETAIL mechanism in PARAM. The operation requires technically valid TDEA key bytes of exactly 16 or 24 bytes; the declared KEY algorithm need not match. A conflicting declared KeyType produces a non-blocking KeyTypeConflict warning, and conflicting KeyUsage produces a non-blocking KeyUsageConflict warning, when a diagnostic sink is available. Neither warning changes the bytes or metadata. Unknown algorithm and Unspecified usage cause no corresponding conflict warning. Technical validation remains mandatory; PARAM selects the mechanism, not the KEY metadata.
- **#IV**: Not used as an external parameter. A supplied IV is rejected; the internal chaining value always starts at zero.
- **#PAD**: Parameters supplies ISO-9797-M2 when omitted. Only these two paddings are supported:
    - ISO-9797-M1: Adds zero bytes only when needed to reach a multiple of 8 bytes. Aligned input receives no additional block. Empty input is accepted and becomes one 8-byte zero block before the Retail MAC calculation.
    - ISO-9797-M2: Adds 0x80 followed by zero bytes. Aligned input receives an additional block. Empty input is accepted and becomes 0x80 followed by seven zero bytes.
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

### Streaming Retail-MAC reference calculation

This example requires the repository binary fixture at the stated path.

```text
KEY k = GenerateKey(DES3-RETAIL,0x(000102030405060708090A0B0C0D0E0F))
PARAM p = Parameters(DES3-RETAIL,#PAD:ISO-9797-M2,#MACLEN:"8")
PATH source = "docs/F03.2-reference/records.bin"
VAR tag = Mac(p,k,source)
```

Mac(PATH) processes the same raw bytes as Mac(VAR), with no text conversion.
DES-CBC under K1 starts from zero. After the final padded block the state is
DES-decrypted with K2 and DES-encrypted with K3 (or K1 for a 16-byte key).
The leftmost 4–8 bytes are returned. There is no external IV. Only M1 and M2 are
accepted, with padding applied once for the complete stream: M1 maps empty input
to a zero block and adds no block to aligned nonempty data; M2 appends 0x80 and
zeroes, always adding a block when aligned. Key-quality checks remain active.

See `docs/F03.2-reference/` for full reproducible configurations, binary files and
independently computed expected tags. POS/HP-NonStop systems must provide the same
bytes including NUL, CR/LF and record framing. No ASCII/EBCDIC conversion, automatic
record extension or truncation occurs. Published keys are test material only.

Retail-MAC is supplied for legacy reference and interoperability work. Its DES
block size, key strength and truncated tags constrain security; it is not a
general recommendation for new protocols. Mac(PATH) returns VAR and writes no
files or containers. Future F04 MacPack/MacVerify are separate container operations
and are not implemented here.
