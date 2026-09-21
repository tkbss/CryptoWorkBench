# MECHANISM KDF-EP2-SESSION

KDF-EP2-SESSION derives the session-key variants defined by ep2 Security Specification V8.3.0 section 8.11. SHA-256, each HKDF info value and each output length are fixed by the selected variant.

---

## Key Features

- **Complete HKDF**: The 16-byte Session Key is the IKM for HKDF-Extract-SHA256 with the required 32-byte salt. The resulting PRK is used by HKDF-Expand-SHA256.
- **Variant Info**: #VARIANT selects a fixed info byte sequence. The info value is used directly and is not hashed.
- **Output Length**: The selected variant fixes the result at 16 or 32 bytes. #OUTLEN is not supported.
- **Fixed Hash**: SHA-256 is fixed by the ep2 profile. #HASH is not supported.
- **Info Placeholder**: The third Derive argument must be present and empty because info is defined by #VARIANT.

## Data Flow

Session Key and salt are processed by HKDF-Extract-SHA256. HKDF-Expand-SHA256 then combines the resulting PRK with the selected variant info to produce the Session Key Variant.

| Variant | Info | Output |
|---------|------|--------|
| TC | 7184718471847184 | 32 Byte |
| MAC-SEND | EB5AEB5AEB5AEB5A | 32 Byte |
| MAC-RECEIVE | 9309930993099309 | 32 Byte |
| ENCRYPTION | 0C7E0C7E0C7E0C7E | 16 Byte |
| PIN | C5B1C5B1C5B1C5B1 | 16 Byte |
| KEY-ENCRYPTION | 3FE73FE73FE73FE7 | 16 Byte |

## Functions

| Functions | Parameters | Input | Output | Key Length |
|-----------|------------|-------|--------|------------|
| Parameters | N/A | #MECH, #SALT, #VARIANT | PARAM variable | N/A |
| Derive | #MECH, #SALT, #VARIANT | parameters, Session Key, empty data | Generic KEY variable | 128 or 256 bits |

## Input Requirements

- The Session Key must be exactly 16 bytes.
- #SALT must resolve to exactly 32 bytes.
- The third Derive argument must be the empty string (`""`); non-empty data is rejected.
- Salt accepts `0x(...)` hexadecimal bytes, `b64(...)` decoded Base64 bytes, a UTF-8 string, or the stored byte representation of a variable.

## Parameters

- **#MECH**: Required. Specifies KDF-EP2-SESSION.
- **#SALT**: Required and exactly 32 bytes.
- **#VARIANT**: Required. Accepts TC, MAC-SEND, MAC-RECEIVE, ENCRYPTION, PIN or KEY-ENCRYPTION.
- **#HASH**: Not supported; SHA-256 is fixed.
- **#OUTLEN**: Not supported; the variant fixes the output length.

## Example Usage

KEY sessionKey = GenerateKey(HMAC-SHA256, 0x(0123456789ABCDEF23456789ABCDEF01))
PARAM sessionKdf = Parameters(KDF-EP2-SESSION, #SALT:0x(0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345), #VARIANT:TC)
KEY sessionVariant = Derive(sessionKdf, sessionKey, "")
PARAM encryptionKdf = Parameters(KDF-EP2-SESSION, #SALT:0x(0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345), #VARIANT:ENCRYPTION)
KEY encryptionVariant = Derive(encryptionKdf, sessionKey, "")

The TC example returns `CDA5C89A6B4F073779AB3B882C5CDFEFE756621E2FD4C4AC46C9FFCB9915C5CC`. The ENCRYPTION example returns `29DDFF143885D21CD2425D1FDDA2C229`.

## ep2 Reference

ep2 Security Specification V8.3.0, section 8.11, Session Key Variant Generation.

---
