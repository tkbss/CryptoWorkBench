# MECHANISM HMAC-SHA1

HMAC-SHA1 calculates a keyed message authentication code using the HMAC construction and the SHA-1 hash function. It supports MAC calculation rather than encryption or decryption.

---

## Key Features

- **Hash Function**: SHA-1 produces a 160-bit digest and is specified in FIPS 180-4. HMAC is specified in RFC 2104 and FIPS 198-1.
- **Security Note**: SHA-1 is not recommended for new applications. Migration to a SHA-2 or SHA-3 mechanism is recommended, while CryptoScript continues to support HMAC-SHA1.
- **Key Requirements**: GenerateKey accepts any positive key length that is a multiple of 8 bits, or a non-empty hexadecimal key. CryptoScript does not impose AES- or DES3-style key-size restrictions.
- **Input Data**: Mac accepts hexadecimal data, Base64 data, UTF-8 string data, or data stored in a VAR. Empty input is accepted.
- **MAC Length**: Mac always returns the complete 20-byte HMAC-SHA1 tag.
- **Return Value**: Mac returns a VAR containing hexadecimal MAC bytes in 0x(...) format.

## Functions
The following functions are available for HMAC-SHA1 mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey | N/A | HMAC-SHA1, positive byte-aligned key length or hexadecimal key | KEY variable | Positive multiple of 8 bits |
| Parameters | N/A | #MECH | PARAM variable | N/A |
| Mac | #MECH | parameters, key, data | VAR MAC | Any non-empty key |

## Parameters
These parameters are used with the HMAC-SHA1 mechanism:
- **#MECH**: Specifies HMAC-SHA1 in PARAM.
- **Additional Parameters**: Parameters rejects additional parameters.
- **#IV**: Not supported.
- **#PAD**: Not supported. HMAC performs its own internal processing and does not use external message padding.
- **#MACLEN**: Not supported. The result is not truncated and always contains the complete 20-byte tag.
- **Operations**: Encrypt and Decrypt are not supported for this mechanism.

---
## Example Usage
### HMAC-SHA1 MAC Calculation
KEY k0         = GenerateKey(HMAC-SHA1, 160)  
PARAM p0       = Parameters(HMAC-SHA1)  
VAR cleartext  = "This is a secret message."  
VAR mac        = Mac(p0, k0, cleartext)  
### HMAC-SHA1 Empty Input
VAR emptymac   = Mac(p0, k0, "")  

---
