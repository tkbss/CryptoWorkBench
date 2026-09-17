# MECHANISM HASH-SHA3-512

HASH-SHA3-512 calculates an unkeyed message digest using the SHA3-512 hash function. It supports hashing rather than message authentication, encryption or decryption.

---

## Key Features

- **Hash Function**: SHA3-512 belongs to the SHA-3 family, produces a 512-bit digest and is specified in FIPS 202.
- **Input Data**: Hash accepts hexadecimal data, Base64 data, UTF-8 string data, or data stored in a VAR. Empty input is accepted.
- **Digest Length**: Hash always returns the complete 64-byte SHA3-512 digest. The output length is not configurable.
- **Return Value**: Hash returns a VAR containing hexadecimal digest bytes in 0x(...) format.
- **Keys**: HASH-SHA3-512 is unkeyed. GenerateKey is not supported.

## Functions
The following functions are available for HASH-SHA3-512 mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| Parameters | N/A | #MECH | PARAM variable | N/A |
| Hash | #MECH | parameters, data | VAR digest | N/A |

## Parameters
These parameters are used with the HASH-SHA3-512 mechanism:
- **#MECH**: Specifies HASH-SHA3-512 in PARAM.
- **Additional Parameters**: Parameters rejects additional parameters.
- **#IV**: Not supported.
- **#PAD**: Not supported.
- **#MACLEN**: Not supported.
- **Salt and Output Length**: Not supported. The result always contains the complete 64-byte digest.

---
## Example Usage
### HASH-SHA3-512 Digest Calculation
PARAM p0       = Parameters(HASH-SHA3-512)  
VAR cleartext  = "This is a secret message."  
VAR digest     = Hash(p0, cleartext)  
### HASH-SHA3-512 Empty Input
VAR emptyhash  = Hash(p0, "")  

---
