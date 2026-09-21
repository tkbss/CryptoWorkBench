# MECHANISM KDF-EP2-PAN-SURROGATE-TRX

KDF-EP2-PAN-SURROGATE-TRX derives Key PAN Surrogate Trx as defined by ep2 Security Specification V8.3.0 section 8.14.

---

## Key Features

- **No Extract**: The 16-byte Merchant Key is used directly by HKDF-Expand-SHA256.
- **Raw DOL Info**: The DOL byte sequence is used directly as HKDF info. It is not pre-hashed.
- **Full Output**: Expand produces and returns all 32 bytes; no truncation is performed.
- **Fixed Profile**: SHA-256 and the 32-byte output are fixed. No additional parameters are supported.
- **Primary and Secondary**: Both use this mechanism. Their results differ through the supplied keys or data, not through separate mechanism names.

## Data Flow

HKDF-Expand-SHA256 uses the Merchant Key directly with the raw DOL as info and returns the complete 32-byte result. There is no Extract, DOL hash or truncation step.

## Functions

| Functions | Parameters | Input | Output | Key Length |
|-----------|------------|-------|--------|------------|
| Parameters | N/A | #MECH | PARAM variable | N/A |
| Derive | #MECH | parameters, Merchant Key, DOL data | Generic KEY variable | 256 bits |

## Input Requirements

- The Merchant Key must be exactly 16 bytes.
- DOL is the third Derive argument. Its length is not structurally restricted, and an empty byte sequence is valid.
- `0x(...)` supplies raw hexadecimal bytes, `b64(...)` supplies decoded Base64 bytes, and `"..."` supplies UTF-8 bytes. A variable supplies its stored byte representation.
- A string that looks hexadecimal remains UTF-8 text. CryptoScript does not parse the DOL structure.

## Parameters

- **#MECH**: Required. Specifies KDF-EP2-PAN-SURROGATE-TRX.
- No additional parameters are supported. SHA-256 and the 32-byte result are fixed by ep2.

## Example Usage

KEY merchantKey = GenerateKey(HMAC-SHA256, 0x(0123456789ABCDEF23456789ABCDEF01))
PARAM surrogateTrxKdf = Parameters(KDF-EP2-PAN-SURROGATE-TRX)
KEY surrogateTrx = Derive(surrogateTrxKdf, merchantKey, 0x(5413330089020011))

The example returns `AEA780CDFC3CDA67C0FD0D70D509C9B4C1DD1F40F0C05D922BFD3BC8A01E2E6E`.

## ep2 Reference

ep2 Security Specification V8.3.0, section 8.14, Key Derivation Function for Key PAN Surrogate Trx.

---
