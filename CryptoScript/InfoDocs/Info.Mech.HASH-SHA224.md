# MECHANISM HASH-SHA224

HASH-SHA224 calculates an unkeyed message digest using the SHA-224 hash function. It supports hashing rather than message authentication, encryption or decryption.

---

## Key Features

- **Hash Function**: SHA-224 belongs to the SHA-2 family, produces a 224-bit digest and is specified in FIPS 180-4.
- **Input Data**: Hash accepts hexadecimal data, Base64 data, UTF-8 string data, or data stored in a VAR. Empty input is accepted.
- **Digest Length**: Hash always returns the complete 28-byte SHA-224 digest. The output length is not configurable.
- **Return Value**: Hash returns a VAR containing hexadecimal digest bytes in 0x(...) format.
- **Keys**: HASH-SHA224 is unkeyed. GenerateKey is not supported.

## Functions
The following functions are available for HASH-SHA224 mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| Parameters | N/A | #MECH | PARAM variable | N/A |
| Hash | #MECH | parameters, data | VAR digest | N/A |

## Parameters
These parameters are used with the HASH-SHA224 mechanism:
- **#MECH**: Specifies HASH-SHA224 in PARAM.
- **Additional Parameters**: Parameters rejects additional parameters.
- **#IV**: Not supported.
- **#PAD**: Not supported.
- **#MACLEN**: Not supported.
- **Salt and Output Length**: Not supported. The result always contains the complete 28-byte digest.

---
## Example Usage
### HASH-SHA224 Digest Calculation
PARAM p0       = Parameters(HASH-SHA224)  
VAR cleartext  = "This is a secret message."  
VAR digest     = Hash(p0, cleartext)  
### HASH-SHA224 Empty Input
VAR emptyhash  = Hash(p0, "")  

---
