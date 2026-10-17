# MECHANISM HMAC-SHA3-384

HMAC-SHA3-384 calculates a keyed message authentication code using the HMAC construction and the SHA3-384 hash function. It supports MAC calculation rather than encryption or decryption.

---

## Key Features

- **Hash Function**: SHA3-384 belongs to the SHA-3 family, produces a 384-bit digest and is specified in FIPS 202. HMAC is specified in RFC 2104 and FIPS 198-1; RFC 9688 defines HMAC-SHA3 conventions for CMS.
- **Key Requirements**: GenerateKey accepts any positive key length that is a multiple of 8 bits, or a non-empty hexadecimal key. CryptoScript does not impose AES- or DES3-style key-size restrictions.
- **Input Data**: Mac accepts hexadecimal data, Base64 data, UTF-8 string data, or data stored in a VAR. Empty input is accepted.
- **MAC Length**: Mac always returns the complete 48-byte HMAC-SHA3-384 tag.
- **Return Value**: Mac returns a VAR containing hexadecimal MAC bytes in 0x(...) format.

### Semantic diagnostics

Expected KeyType metadata: Hmac. Expected KeyUsage: `MacGenerate`.
Declarative conflicts emit non-blocking `KeyTypeConflict` then `KeyUsageConflict` warnings where a fixed expected algorithm exists. Correct metadata emits no conflict warning; Unknown KeyType and Unspecified usage impose no diagnostic restriction. These checks do not mutate KEY or PARAM and work without a diagnostic sink. The selected HMAC mechanism fixes the hash. Structured calls diagnose the original KEY object even when another KEY has identical bytes. Legacy value-only calls keep their existing compatibility behavior.

Malformed key bytes, unsupported key lengths, invalid parameters and mandatory protocol or authentication checks remain blocking. Warnings do not establish PCI or EP2 conformity; the EP2 security matrix remains open.

### Key Generation and Import

GenerateKey(HMAC-SHA3-384, ...) supports random generation and explicit hexadecimal import. The registry fixes the resulting KeyType to Secret(Hmac) for both variants. Valid key lengths: any positive multiple of 8 bits; imported hexadecimal material must not be empty. Imported bytes are preserved exactly. The mechanism selects the key algorithm and does not restrict the resulting KEY to its original mode or HMAC digest; other compatible mechanisms of the same family may consume it.

## Functions
The following functions are available for HMAC-SHA3-384 mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey | N/A | HMAC-SHA3-384, positive byte-aligned key length or hexadecimal key | KEY variable | Positive multiple of 8 bits |
| Parameters | N/A | #MECH | PARAM variable | N/A |
| Mac | #MECH | parameters, key, data | VAR MAC | Any non-empty key |

## Parameters
These parameters are used with the HMAC-SHA3-384 mechanism:
- **#MECH**: Specifies HMAC-SHA3-384 in PARAM.
- **Additional Parameters**: Parameters rejects additional parameters.
- **#IV**: Not supported.
- **#PAD**: Not supported. HMAC performs its own internal processing and does not use external message padding.
- **#MACLEN**: Not supported. The result is not truncated and always contains the complete 48-byte tag.
- **Operations**: Encrypt and Decrypt are not supported for this mechanism.

---
## Example Usage
### HMAC-SHA3-384 MAC Calculation
KEY k0         = GenerateKey(HMAC-SHA3-384, 384)  
PARAM p0       = Parameters(HMAC-SHA3-384)  
VAR cleartext  = "This is a secret message."  
VAR mac        = Mac(p0, k0, cleartext)  
### HMAC-SHA3-384 Empty Input
VAR emptymac   = Mac(p0, k0, "")  

---
