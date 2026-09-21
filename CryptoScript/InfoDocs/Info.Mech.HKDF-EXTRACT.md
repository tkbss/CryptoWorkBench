# MECHANISM HKDF-EXTRACT

HKDF-EXTRACT performs only the Extract operation from RFC 5869: PRK = HMAC-Hash(salt, IKM). It returns the pseudorandom key (PRK) as a generic KEY whose length is HashLen.

---

## Key Features

- **Extract Only**: The KEY argument supplies the input keying material (IKM). No Expand operation is performed.
- **Salt**: #SALT is optional. If omitted, HashLen zero bytes are used. An explicitly empty salt is valid.
- **Hash Function**: #HASH selects one of the eleven supported HASH-* mechanisms. HMAC-* mechanism names are not accepted.
- **Info Placeholder**: The third Derive argument is required by the common signature but must be empty. Non-empty data is an error.
- **Return Value**: Derive returns a generic KEY of HashLen bytes. Its Mechanism is empty and its DerivationMechanism is HKDF-EXTRACT.

## Functions
The following functions are available for HKDF-EXTRACT mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|------------|-------|--------|------------|
| Parameters | N/A | #MECH, #HASH, optional #SALT | PARAM variable | N/A |
| Derive | #MECH, #HASH, optional #SALT | parameters, IKM key, empty data | Generic KEY variable | HashLen |

## Parameters
These parameters are used with the HKDF-EXTRACT mechanism:
- **#MECH**: Required. Specifies HKDF-EXTRACT in PARAM.
- **#HASH**: Required. Selects HASH-SHA1, HASH-SHA224, HASH-SHA256, HASH-SHA384, HASH-SHA512, HASH-SHA512-224, HASH-SHA512-256, HASH-SHA3-224, HASH-SHA3-256, HASH-SHA3-384 or HASH-SHA3-512. HMAC-* values are not accepted.
- **#SALT**: Optional. Accepts hexadecimal data, Base64 data, a UTF-8 string or data stored in a VAR. If omitted, HKDF uses HashLen zero bytes. An explicitly empty salt is valid.
- **#OUTLEN**: Not supported. The PRK length is always HashLen.
- **DATA**: The third Derive argument must be present and empty (`""`). HKDF-EXTRACT does not use info data, and non-empty data is rejected.

---
## Example Usage
### HKDF Extract
KEY ikm = GenerateKey(HMAC-SHA256, 0x(0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B))
PARAM extract = Parameters(HKDF-EXTRACT, #HASH:HASH-SHA256, #SALT:0x(000102030405060708090A0B0C))
KEY prk = Derive(extract, ikm, "")

---
