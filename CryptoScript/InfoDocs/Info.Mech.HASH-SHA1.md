# MECHANISM HASH-SHA1

HASH-SHA1 calculates an unkeyed message digest using the SHA-1 hash function. It supports hashing rather than message authentication, encryption or decryption.

---

## Key Features

- **Hash Function**: SHA-1 produces a 160-bit digest and is specified in FIPS 180-4.
- **Security Note**: SHA-1 is not recommended for new applications. Migration to a SHA-2 or SHA-3 mechanism is recommended, while CryptoScript continues to support HASH-SHA1.
- **Input Data**: Hash accepts hexadecimal data, Base64 data, UTF-8 string data, or data stored in a VAR. Empty input is accepted.
- **Digest Length**: Hash always returns the complete 20-byte SHA-1 digest. The output length is not configurable.
- **Return Value**: Hash returns a VAR containing hexadecimal digest bytes in 0x(...) format.
- **Keys**: HASH-SHA1 is unkeyed. GenerateKey is not supported.

## Functions
The following functions are available for HASH-SHA1 mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| Parameters | N/A | #MECH | PARAM variable | N/A |
| Hash | #MECH | parameters, data | VAR digest | N/A |

## Parameters
These parameters are used with the HASH-SHA1 mechanism:
- **#MECH**: Specifies HASH-SHA1 in PARAM.
- **Additional Parameters**: Parameters rejects additional parameters.
- **#IV**: Not supported.
- **#PAD**: Not supported.
- **#MACLEN**: Not supported.
- **Salt and Output Length**: Not supported. The result always contains the complete 20-byte digest.

---
## Example Usage
### HASH-SHA1 Digest Calculation
PARAM p0       = Parameters(HASH-SHA1)  
VAR cleartext  = "This is a secret message."  
VAR digest     = Hash(p0, cleartext)  
### HASH-SHA1 Empty Input
VAR emptyhash  = Hash(p0, "")  

---
