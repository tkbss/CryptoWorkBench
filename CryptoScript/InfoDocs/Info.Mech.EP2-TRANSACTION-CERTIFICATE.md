# MECHANISM EP2-TRANSACTION-CERTIFICATE

## Key Features

Computes `Left8(HMAC-SHA256(K, preparedDolBytes))` according to EP2 Security Specification 8.3.0,
section 5.6 (page 9-41), and section 8.2 (page 9-57).
`Left8` takes the leftmost eight bytes of the complete HMAC-SHA256 output.
The return value is a VAR containing exactly eight bytes in `0x(...)` HEX format.
There is no configurable digest, padding, IV or output length.

### Input boundary

The caller supplies the already prepared binary DOL value sequence. There is no additional SHA-256 prehash, DOL parser or TLV processing. DOL selection, object lookup, ordering and length handling belong to the caller. Use plaintext values for fields that will themselves be encrypted (for example PAN). Treat date, expdate and time as numeric (n). General DOL handling follows EMV 4.3 Book 3 section 5.4. Select the application DOL, or the Reservation/FollowOn DOL for the transaction types specified in the footnote to section 5.6. The MAC cannot validate these preparation rules from the resulting bytes.

### Keys and diagnostics

Supply exactly 32 bytes in a genuine KEY reference. Known key size metadata must match
256 bits. Import with GenerateKey(HMAC-SHA256, ...) or separately derive the session
key variant using KDF-EP2-SESSION (sections 5.1.4 and 8.11).
Use TC for the Acquirer Transaction Certificate.
The raw 16-byte Session Key is not a MAC key. Variant selection belongs to the caller;
DerivationMechanism alone does not prove the selected variant.

Expected metadata is Hmac and MacGenerate. KeyAlgorithm.Unknown and unspecified usage
are accepted. Conflicting declared algorithm/usage produces non-blocking warnings,
without modifying the KEY. Invalid bytes, key sizes and argument contracts block execution.
These mechanisms perform calculation only; they do not verify a received MAC or enforce
protocol validity. Section 5.3.2 requires rejection on verification failure at the protocol layer.

## Functions

| Function | Input | Output |
|---|---|---|
| Parameters | EP2-TRANSACTION-CERTIFICATE, no additional arguments | PARAM |
| Mac | PARAM reference, KEY reference, HEX data | Eight-byte HEX VAR |

GenerateKey, Hash, Derive, Encrypt, Decrypt, Wrap and Unwrap are not supported by this
mechanism. Use the existing HMAC or KDF mechanisms to prepare keys.

## Parameters

Only `#MECH:EP2-TRANSACTION-CERTIFICATE` is supported. Additional and duplicate parameters are forbidden.
Both Parameters(...) and direct `PARAM p = #MECH:EP2-TRANSACTION-CERTIFICATE` declarations are supported.

`Mac(p, k, data)` requires exactly three positional arguments:

- Position 0: genuine PARAM variable reference with the matching MECH.
- Position 1: genuine KEY variable reference; no raw key literals or VAR substitutes.
- Position 2: direct HEX literal or genuine HEX VAR reference.

Reconstructed PARAM/KEY objects, serialized values, nested function results, strings,
Base64 and implicit conversions are rejected. Identical key bytes do not merge object identity.
Legacy Mac(string[]) is explicitly unsupported for these mechanisms.
The cryptographic core permits empty bytes, without claiming that an empty message or
transaction is EP2-valid. The current grammar cannot spell an empty HEX literal `0x()`;
no grammar extension or string-input exception is introduced here.

## Example Usage

The following official example uses the same key and data as section 5.6 (page 9-41).
The expected result is `0x(6FD9A4429C6348AA)`.

```cryptoscript
KEY k = GenerateKey(HMAC-SHA256, 0x(0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345))
PARAM p = Parameters(EP2-TRANSACTION-CERTIFICATE)
VAR data = 0x(11112222333344445555)
VAR result = Mac(p, k, data)
```
