# MECHANISM KDF-HKDF

KDF-HKDF derives keying material using the HMAC-based Extract-and-Expand Key Derivation Function specified in RFC 5869. It uses a configured hash function and returns a generic KEY rather than an AES-, DES3- or HMAC-specific key.

---

## Key Features

- **Extract**: HKDF calculates PRK = HMAC-Hash(salt, IKM), where the KEY argument supplies the input keying material (IKM).
- **Expand**: HKDF calculates T(i) = HMAC-Hash(PRK, T(i-1) || info || i) and returns the first requested output bytes. The DATA argument supplies info and may be empty.
- **Hash Function**: #HASH selects one of the eleven supported HASH-* mechanisms. HMAC is used internally, but #HASH does not accept HMAC-* mechanism names.
- **Output Length**: #OUTLEN is a bit length. It must be positive, divisible by 8 and no greater than 255 times HashLen times 8 bits.
- **Return Value**: Derive returns a generic KEY containing hexadecimal bytes in 0x(...) format. Its Mechanism is empty and its DerivationMechanism is KDF-HKDF.

## Functions
The following functions are available for KDF-HKDF mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| Parameters | N/A | #MECH, #HASH, optional #SALT, #OUTLEN | PARAM variable | N/A |
| Derive | #MECH, #HASH, optional #SALT, #OUTLEN | parameters, IKM key, info data | Generic KEY variable | #OUTLEN bits |

## Parameters
These parameters are used with the KDF-HKDF mechanism:
- **#MECH**: Required. Specifies KDF-HKDF in PARAM.
- **#HASH**: Required. Selects HASH-SHA1, HASH-SHA224, HASH-SHA256, HASH-SHA384, HASH-SHA512, HASH-SHA512-224, HASH-SHA512-256, HASH-SHA3-224, HASH-SHA3-256, HASH-SHA3-384 or HASH-SHA3-512. HMAC-* values are not accepted.
- **#SALT**: Optional. Accepts hexadecimal data, Base64 data, a UTF-8 string or data stored in a VAR. If omitted, HKDF uses HashLen zero bytes as specified by RFC 5869. #SALT:"" explicitly supplies a zero-length byte string.
- **#OUTLEN**: Required output length in bits. The value must be greater than zero, divisible by 8 and at most 255 times HashLen times 8.
- **DATA / info**: The third Derive argument is RFC 5869 info. It accepts hexadecimal data, Base64 data, a UTF-8 string or data stored in a VAR. An empty string ("") is valid. There is no #INFO parameter.

---
## Example Usage
### KDF-HKDF Key Derivation
KEY ikm   = GenerateKey(AES-CBC, 256)
PARAM hkdf = Parameters(KDF-HKDF, #HASH:HASH-SHA256, #SALT:0x(000102030405060708090A0B0C), #OUTLEN:256)
KEY okm   = Derive(hkdf, ikm, "CryptoScript HKDF")

---
