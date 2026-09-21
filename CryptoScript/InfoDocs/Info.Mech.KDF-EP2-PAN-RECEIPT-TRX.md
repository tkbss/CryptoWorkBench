# MECHANISM KDF-EP2-PAN-RECEIPT-TRX

KDF-EP2-PAN-RECEIPT-TRX derives Key PAN Receipt Trx as defined by ep2 Security Specification V8.3.0 section 8.12.

---

## Key Features

- **No Extract**: The 16-byte Key PAN Receipt Trm is used directly by HKDF-Expand-SHA256.
- **Hashed DOL Info**: Info is SHA-256(DOL), not the raw DOL.
- **Truncation**: Expand produces 32 bytes; the leftmost 16 bytes are returned and the rightmost 16 bytes are discarded.
- **Fixed Profile**: SHA-256 and all output lengths are fixed. No additional parameters are supported.
- **8.12 versus 8.14**: Section 8.12 hashes the DOL before HKDF-Expand. Section 8.14 uses the DOL directly as HKDF info.

## Data Flow

The complete DOL byte sequence is hashed with SHA-256. That digest is used as info for a 32-byte HKDF-Expand-SHA256 operation keyed directly by Key PAN Receipt Trm. The result is truncated to its leftmost 16 bytes.

## Functions

| Functions | Parameters | Input | Output | Key Length |
|-----------|------------|-------|--------|------------|
| Parameters | N/A | #MECH | PARAM variable | N/A |
| Derive | #MECH | parameters, Key PAN Receipt Trm, DOL data | Generic KEY variable | 128 bits |

## Input Requirements

- Key PAN Receipt Trm must be exactly 16 bytes.
- DOL is the third Derive argument. Its length is not structurally restricted, and an empty byte sequence is valid.
- `0x(...)` supplies raw hexadecimal bytes, `b64(...)` supplies decoded Base64 bytes, and `"..."` supplies UTF-8 bytes. A variable supplies its stored byte representation.
- A string that looks hexadecimal remains UTF-8 text. CryptoScript does not parse the DOL structure.

## Parameters

- **#MECH**: Required. Specifies KDF-EP2-PAN-RECEIPT-TRX.
- No additional parameters are supported. SHA-256, the 32-byte expansion and the 16-byte result are fixed by ep2.

## Example Usage

KEY terminalKey = GenerateKey(HMAC-SHA256, 0x(0123456789ABCDEF23456789ABCDEF01))
PARAM receiptTrxKdf = Parameters(KDF-EP2-PAN-RECEIPT-TRX)
KEY receiptTrx = Derive(receiptTrxKdf, terminalKey, 0x(1322042006015445524D3132333412345678A000000111))

The example returns `D37FF3ECC3F2EAC21EF3C968A9DE2934`.

## ep2 Reference

ep2 Security Specification V8.3.0, section 8.12, Key Derivation Function for Key PAN Receipt Trx.

---
