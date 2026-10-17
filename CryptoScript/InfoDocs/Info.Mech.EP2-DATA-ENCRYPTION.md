# MECHANISM EP2-DATA-ENCRYPTION

## Profile and normative references

Implements EP2 Security Specification 8.3.0, sections 5.5 and 5.5.1 (confidential
data encipherment) and 8.3 (Encryption, pages 9-58 and 9-59), including Table 10.
Sections 5.1.4 and 8.11 define the separately derived Session Key Variant
Encryption; section 8.10 specifies random number generator requirements.

AES-128 CTR uses a 16-byte key and constructs each AES input as:

`Nonce[12] || FieldOffset[1] || Counter[3]`

The counter starts at `000000` and advances in big-endian order within its three
bytes only. Neither nonce nor offset can be changed by a carry. At most 2^24
blocks (268,435,456 bytes, 256 MiB) are accepted per operation; the last counter
is `FFFFFF`. Excess input is rejected before transformation. Partial final blocks
are XORed with the needed keystream prefix. There is no padding; Encrypt and
Decrypt use the same transformation and preserve byte length.

**CTR alone provides no data integrity or authenticity.** Required protocol
authentication and validation remain the caller's responsibility.

## Direction and field offsets

SEND means messages **from the master**; RECEIVE means messages **to the master**.
The direction is independent of the local Encrypt or Decrypt operation. A receiver
decrypting a message from the master must still select SEND.

| Field | SEND | RECEIVE |
|---|---|---|
| Application Expiration Date | 00 | 00 |
| Application PAN | 01 | 01 |
| Card Verification Code 2 | 02 | RFU |
| Component Secret | 03 | RFU |
| E-Commerce Card Number | 04 | RFU |
| Track 2 Data | 05 | RFU |
| Track 2 Equivalent Data | 06 | RFU |
| Track 3 Data | 07 | RFU |
| All remaining offsets | 08–FF RFU | 02–FF RFU |

RFU offsets are rejected, including when decrypting. Direction validates the field
selection; it does not add another byte or otherwise alter the AES counterblock.
The caller selects the right protocol field and supplies its exact encoded bytes.

## Parameters and functions

Only Parameters, Encrypt and Decrypt are supported. GenerateKey, Hash, Mac, Derive,
Wrap, Unwrap and BlockHeader are not supported by this mechanism.

| Parameter | Required value |
|---|---|
| #MECH | EP2-DATA-ENCRYPTION |
| #NONCE | HEX value containing exactly 12 bytes |
| #OFFSET | HEX value containing exactly one byte, valid for the direction |
| #DIRECTION | Normal string literal `"SEND"` or `"RECEIVE"` |

OFFSET and DIRECTION are Workbench interface names for the EP2 rules. Parameter
values may be supplied through variables resolving to those representations.
There are no defaults. Additional or duplicate parameters are rejected. In
particular, no #IV, #COUNTER or #PAD can override the profile. Both Parameters(...)
and a direct `PARAM p = #MECH:EP2-DATA-ENCRYPTION ...` declaration are supported.

Encrypt(p, k, data) and Decrypt(p, k, data) require exactly three arguments:

- Position 0: genuine PARAM variable reference with the matching mechanism.
- Position 1: genuine KEY variable reference containing exactly 16 key bytes.
- Position 2: direct HEX literal or genuine HEX VAR reference.

Results are HEX VARs. Serialized/reconstructed objects, nested function results,
raw key literals, string/Base64 data and implicit data conversions are rejected.
Legacy Encrypt(string[]) and Decrypt(string[]) calls are explicitly unsupported.
The core accepts empty bytes; the current grammar cannot spell `0x()` and is not
extended for empty literals. An accepted byte sequence does not establish protocol validity.

Known key size metadata must match 128 bits. Expected metadata is AES and Encrypt
or Decrypt usage for the respective operation. Declared algorithm or usage conflicts
produce non-blocking Semantic Diagnostics; Unknown algorithm and unspecified usage
are accepted. Original KEY identity and metadata are preserved. Invalid key bytes,
sizes, nonce lengths, offsets and argument contracts block execution.

## Nonce and key responsibility

The caller supplies the nonce explicitly. This mechanism generates no nonce,
tracks no reuse and performs no session management. It restarts the counter at zero
for every invocation: never reuse the same nonce/offset counterblock range with
the same key for a new encryption. Merely changing SEND to RECEIVE does not separate
keystreams. The caller or enclosing EP2 protocol owns nonce generation, uniqueness,
distribution and lifetime, including coordination across fields and directions.
Use a random source meeting EP2 section 8.10 (PTS B9 validation, or the specified
independent laboratory assessment using NIST SP800-22 when PTS validation is absent).
The published nonce below is a test value, not a production nonce.

Prepare Session Key Variant Encryption separately with
[KDF-EP2-SESSION](cryptoscript-info://mechanism/KDF-EP2-SESSION), #VARIANT:ENCRYPTION.
It returns 16 bytes. No derivation occurs inside this encryption mechanism;
metadata cannot prove that a caller supplied the correct session variant.

Generic [AES-CTR](cryptoscript-info://mechanism/AES-CTR) retains its existing key,
nonce and 32-bit counter contracts. The EP2 profile fixes AES-128, a 24-bit initial
zero counter and the Table 10 field offset, and requires object-aware arguments.

## Executable reference example

EP2 section 8.3 specifies initial counterblock
`0123456789ABCDEF2345678906000000` for SEND / Track 2 Equivalent Data.
Encryption returns `BC689340D7866D4EF5C0C35A4844BAAB0288`; decryption returns
`6799998900000200051D2512220080810790`.

```cryptoscript
KEY k = GenerateKey(AES-ECB, 0x(0123456789ABCDEF23456789ABCDEF01))
PARAM p = Parameters(EP2-DATA-ENCRYPTION, #NONCE:0x(0123456789ABCDEF23456789), #OFFSET:0x(06), #DIRECTION:"SEND")
VAR plain = 0x(6799998900000200051D2512220080810790)
VAR cipher = Encrypt(p, k, plain)
VAR restored = Decrypt(p, k, cipher)
```

Equivalent direct parameter declaration:

```cryptoscript
PARAM p = #MECH:EP2-DATA-ENCRYPTION #NONCE:0x(0123456789ABCDEF23456789) #OFFSET:0x(06) #DIRECTION:"SEND"
```
