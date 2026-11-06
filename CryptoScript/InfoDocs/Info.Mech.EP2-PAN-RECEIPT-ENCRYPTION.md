# MECHANISM EP2-PAN-RECEIPT-ENCRYPTION

## Purpose and normative references

Implements PAN receipt encipherment from EP2 Security Specification 8.3.0,
section 5.5.2 (page 9-39): create Application Primary Account Number (PAN) -
Enciphered using AES-128 ECB, mandatory ISO-7816 padding, and **Key PAN Receipt Trx**.
The acquirer decrypts this value for process sales slip, manual entry or rectification.

The input key must already be the 16-byte transaction key. Derive it separately with
[KDF-EP2-PAN-RECEIPT-TRX](cryptoscript-info://mechanism/KDF-EP2-PAN-RECEIPT-TRX),
defined in section 8.12 (pages 9-67 and 9-68). Section 8.13 (page 9-69) defines the
terminal key derivation, one level earlier. Key PAN Receipt Trm, Session Key Variant
Encryption and Session Key Variant Key Encryption are not substitutes for the
transaction key. This mechanism performs no derivation and cannot prove a key's
protocol role from its bytes or metadata.

## Byte representation and padding

Supply the already correctly encoded PAN bytes. HEX denotes those exact bytes;
the mechanism does not convert ASCII digits, Packed BCD, EMV objects or any other
PAN representation. Section 5.5.2 specifies the encryption step, not a complete
general PAN conversion contract for CryptoScript. Field encoding and protocol
validity remain the caller's responsibility. `0x(1234567890123456)` is eight bytes,
not the ASCII encoding of sixteen decimal characters.

Encrypt appends `80` and then zero bytes to reach a multiple of 16 bytes. An input
already aligned to 16 bytes receives a complete additional padding block.
AES uses ECB with provider padding disabled. The result is a HEX VAR.

Decrypt accepts only a positive multiple of 16 ciphertext bytes, decrypts with
AES-128 ECB, and strictly validates ISO-7816 padding. The final block must contain
the `80` marker followed only by zeros; missing/invalid markers and nonzero bytes
after the marker cause a controlled exception. The original bytes, including any
embedded or trailing `00` and `80` data bytes, are returned as a HEX VAR.

The cryptographic core supports empty plaintext (one padding block on encryption).
The CryptoScript lexer cannot spell an empty HEX literal `0x()`; no grammar extension
or implicit string conversion is introduced. Empty ciphertext is invalid.

## Functions and contracts

| Function | Arguments | Result |
|---|---|---|
| Parameters | EP2-PAN-RECEIPT-ENCRYPTION | PARAM |
| Encrypt | PARAM reference, KEY reference, HEX data | HEX ciphertext VAR |
| Decrypt | PARAM reference, KEY reference, HEX ciphertext | HEX original-data VAR |

Only matching `#MECH:EP2-PAN-RECEIPT-ENCRYPTION` is allowed. Additional, duplicate
or contradictory parameters are rejected. No #PAD, #IV, #NONCE, #COUNTER or other
option is accepted. Padding cannot be changed or disabled. Direct
`PARAM p = #MECH:EP2-PAN-RECEIPT-ENCRYPTION` declarations are validated immediately.

Encrypt and Decrypt require exactly three arguments: a genuine PARAM reference at
position 0, a genuine KEY reference at position 1, and a direct HEX literal or
genuine HEX VAR reference at position 2. Reconstructed/serialized objects, nested
function results, raw key literals, Base64 and string data are rejected. Legacy
Encrypt(string[]) and Decrypt(string[]) APIs are unsupported for this profile.
GenerateKey, Hash, Mac, Derive, Wrap, Unwrap and BlockHeader are unsupported.

Key bytes must be exactly 16 bytes; known size metadata must agree with 128 bits.
Expected algorithm metadata is AES; expected usage is Encrypt or Decrypt respectively.
Declared metadata conflicts emit non-blocking Semantic Diagnostics. Unknown algorithm
and unspecified usage remain accepted, including existing EP2-KDF outputs. KEY
identity and metadata are preserved. Invalid key sizes, byte encodings, argument
contracts and ciphertext/padding remain blocking errors. Diagnostics omit key and
PAN values. Temporary key and padded-plaintext copies are cleared where possible.

## Reference vectors and executable example

The **normative KDF vector**, section 8.12, page 9-68, is:

| Value | HEX bytes |
|---|---|
| Key PAN Receipt Trm | 0123456789ABCDEF23456789ABCDEF01 |
| Prepared DOL data | 1322042006015445524D3132333412345678A000000111 |
| Derived Key PAN Receipt Trx | D37FF3ECC3F2EAC21EF3C968A9DE2934 |

The following **independently computed AES vector is not a normative EP2 encryption
vector**. It uses that derived transaction key:

| Value | HEX bytes |
|---|---|
| PAN input bytes | 1234567890123456 |
| ISO-7816 padded input | 12345678901234568000000000000000 |
| Ciphertext | 3E3F25CCEE9B49A90EA87134D3B18222 |
| Decrypted original bytes | 1234567890123456 |

```cryptoscript
KEY terminal = GenerateKey(HMAC-SHA256, 0x(0123456789ABCDEF23456789ABCDEF01))
PARAM kdf = Parameters(KDF-EP2-PAN-RECEIPT-TRX)
KEY trx = Derive(kdf, terminal, 0x(1322042006015445524D3132333412345678A000000111))
PARAM p = Parameters(EP2-PAN-RECEIPT-ENCRYPTION)
VAR pan = 0x(1234567890123456)
VAR cipher = Encrypt(p, trx, pan)
VAR restored = Decrypt(p, trx, cipher)
```

## Security boundary

ECB is used here because this particular EP2 profile requires it, not as a general
recommendation for arbitrary data. It is deterministic and exposes equality of
blocks encrypted under the same key. AES-ECB supplies no independent integrity or
authenticity protection. Successful decryption and padding validation do not prove
that the ciphertext or PAN is authentic. The surrounding protocol must handle
authentication and failure responses appropriately; padding validation is not a MAC.

Generic [AES-ECB](cryptoscript-info://mechanism/AES-ECB) retains its existing contract.
[EP2-DATA-ENCRYPTION](cryptoscript-info://mechanism/EP2-DATA-ENCRYPTION) is the separate
session AES-CTR profile and does not implement PAN receipt encipherment.
