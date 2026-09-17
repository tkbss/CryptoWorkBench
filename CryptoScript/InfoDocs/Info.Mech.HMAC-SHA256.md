# MECHANISM HMAC-SHA256

HMAC-SHA256 calculates a keyed message authentication code using the HMAC construction and the SHA-256 hash function. It supports MAC calculation rather than encryption or decryption.

---

## Key Features

- **Hash Function**: SHA-256 belongs to the SHA-2 family, produces a 256-bit digest and is specified in FIPS 180-4. HMAC is specified in RFC 2104 and FIPS 198-1; RFC 4231 provides HMAC-SHA256 test vectors.
- **Key Requirements**: GenerateKey accepts any positive key length that is a multiple of 8 bits, or a non-empty hexadecimal key. CryptoScript does not impose AES- or DES3-style key-size restrictions.
- **Input Data**: Mac accepts hexadecimal data, Base64 data, UTF-8 string data, or data stored in a VAR. Empty input is accepted.
- **MAC Length**: Mac always returns the complete 32-byte HMAC-SHA256 tag.
- **Return Value**: Mac returns a VAR containing hexadecimal MAC bytes in 0x(...) format.

## Functions
The following functions are available for HMAC-SHA256 mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey | N/A | HMAC-SHA256, positive byte-aligned key length or hexadecimal key | KEY variable | Positive multiple of 8 bits |
| Parameters | N/A | #MECH | PARAM variable | N/A |
| Mac | #MECH | parameters, key, data | VAR MAC | Any non-empty key |

## Parameters
These parameters are used with the HMAC-SHA256 mechanism:
- **#MECH**: Specifies HMAC-SHA256 in PARAM.
- **Additional Parameters**: Parameters rejects additional parameters.
- **#IV**: Not supported.
- **#PAD**: Not supported. HMAC performs its own internal processing and does not use external message padding.
- **#MACLEN**: Not supported. The result is not truncated and always contains the complete 32-byte tag.
- **Operations**: Encrypt and Decrypt are not supported for this mechanism.

---
## Example Usage
### HMAC-SHA256 MAC Calculation
KEY k0         = GenerateKey(HMAC-SHA256, 256)  
PARAM p0       = Parameters(HMAC-SHA256)  
VAR cleartext  = "This is a secret message."  
VAR mac        = Mac(p0, k0, cleartext)  
### HMAC-SHA256 Empty Input
VAR emptymac   = Mac(p0, k0, "")  

---
