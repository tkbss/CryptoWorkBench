# MECHANISM KDF-EP2-PAN-RECEIPT-TRM

KDF-EP2-PAN-RECEIPT-TRM derives Key PAN Receipt Trm as defined by ep2 Security Specification V8.3.0 section 8.13.

---

## Key Features

- **Complete HKDF**: The 16-byte Key PAN Receipt Acq and required 32-byte Salt PAN Receipt Acq are used directly by HKDF-Extract-SHA256.
- **Hashed Properties Info**: Only Terminal Properties are hashed. Info is SHA-256(Terminal Properties); the key and salt are not pre-hashed.
- **Truncation**: HKDF-Expand-SHA256 produces 32 bytes; the leftmost 16 bytes are returned and the rightmost 16 bytes are discarded.
- **Fixed Profile**: SHA-256 and the output lengths are fixed. #HASH, #OUTLEN and #VARIANT are not supported.

## Data Flow

HKDF-Extract-SHA256 derives a PRK from Salt PAN Receipt Acq and Key PAN Receipt Acq. Separately, SHA-256 hashes the complete Terminal Properties byte sequence. HKDF-Expand-SHA256 uses the PRK and this digest as info to produce 32 bytes, which are truncated to the leftmost 16 bytes.

## Functions

| Functions | Parameters | Input | Output | Key Length |
|-----------|------------|-------|--------|------------|
| Parameters | N/A | #MECH, #SALT | PARAM variable | N/A |
| Derive | #MECH, #SALT | parameters, Key PAN Receipt Acq, Terminal Properties | Generic KEY variable | 128 bits |

## Input Requirements

- Key PAN Receipt Acq must be exactly 16 bytes.
- #SALT must resolve to exactly 32 bytes.
- Terminal Properties are the third Derive argument. Their length is not structurally restricted, and an empty byte sequence is valid.
- `0x(...)` supplies raw hexadecimal bytes, `b64(...)` supplies decoded Base64 bytes, and `"..."` supplies UTF-8 bytes. A variable supplies its stored byte representation.
- A string that looks hexadecimal remains UTF-8 text. CryptoScript does not parse the Terminal Properties structure.

## Parameters

- **#MECH**: Required. Specifies KDF-EP2-PAN-RECEIPT-TRM.
- **#SALT**: Required and exactly 32 bytes.
- **#HASH**: Not supported; SHA-256 is fixed.
- **#OUTLEN**: Not supported; expansion and result lengths are fixed.
- **#VARIANT**: Not supported.

## Example Usage

KEY acquirerKey = GenerateKey(HMAC-SHA256, 0x(0123456789ABCDEF23456789ABCDEF01))
PARAM receiptTrmKdf = Parameters(KDF-EP2-PAN-RECEIPT-TRM, #SALT:0x(0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345))
KEY receiptTrm = Derive(receiptTrmKdf, acquirerKey, 0x(5445524D313233340345ABC56876FF1900BCDEF3FCD35914))

The example returns `F9B286738F4A3848C20C7CB12D3886E7`.

## ep2 Reference

ep2 Security Specification V8.3.0, section 8.13, Key Derivation Function for Key PAN Receipt Trm.

---
