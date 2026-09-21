# MECHANISM HKDF-EXPAND

HKDF-EXPAND performs only the Expand operation from RFC 5869. The KEY argument is already the pseudorandom key (PRK); no Extract operation is performed.

---

## Key Features

- **Expand Only**: The second Derive argument must be a PRK, not arbitrary input keying material (IKM).
- **PRK Length**: The PRK must be at least HashLen bytes for the selected hash function.
- **Info**: The DATA argument supplies info and may be empty.
- **Hash Function**: #HASH selects one of the eleven supported HASH-* mechanisms. HMAC-* mechanism names are not accepted.
- **Output Length**: #OUTLEN is specified in bits. It must be positive, divisible by 8 and no greater than 255 times HashLen times 8 bits.
- **Return Value**: Derive returns a generic KEY. Its Mechanism is empty and its DerivationMechanism is HKDF-EXPAND.

## Functions
The following functions are available for HKDF-EXPAND mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|------------|-------|--------|------------|
| Parameters | N/A | #MECH, #HASH, #OUTLEN | PARAM variable | N/A |
| Derive | #MECH, #HASH, #OUTLEN | parameters, PRK key, info data | Generic KEY variable | #OUTLEN bits |

## Parameters
These parameters are used with the HKDF-EXPAND mechanism:
- **#MECH**: Required. Specifies HKDF-EXPAND in PARAM.
- **#HASH**: Required. Selects HASH-SHA1, HASH-SHA224, HASH-SHA256, HASH-SHA384, HASH-SHA512, HASH-SHA512-224, HASH-SHA512-256, HASH-SHA3-224, HASH-SHA3-256, HASH-SHA3-384 or HASH-SHA3-512. HMAC-* values are not accepted.
- **#OUTLEN**: Required output length in bits. The value must be greater than zero, divisible by 8 and at most 255 times HashLen times 8 bits (255 * HashLen bytes).
- **#SALT**: Not supported. HKDF-EXPAND does not perform Extract.
- **DATA / info**: The third Derive argument is RFC 5869 info. It accepts hexadecimal data, Base64 data, a UTF-8 string or data stored in a VAR. An empty string (`""`) is valid.

---
## Example Usage
### HKDF Expand
KEY prk = GenerateKey(HMAC-SHA256, 0x(077709362C2E32DF0DDC3F0DC47BBA6390B6C73BB50F9C3122EC844AD7C2B3E5))
PARAM expand = Parameters(HKDF-EXPAND, #HASH:HASH-SHA256, #OUTLEN:336)
KEY okm = Derive(expand, prk, 0x(F0F1F2F3F4F5F6F7F8F9))

---
