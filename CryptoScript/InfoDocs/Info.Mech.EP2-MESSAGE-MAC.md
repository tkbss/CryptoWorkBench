# MECHANISM EP2-MESSAGE-MAC

## Key Features

Computes `Left8(HMAC-SHA256(K, SHA256(messageBytes)))` according to EP2 Security Specification 8.3.0,
section 5.3.1 (pages 9-36 and 9-37), and section 8.2 (page 9-57).
`Left8` takes the leftmost eight bytes of the complete HMAC-SHA256 output.
The return value is a VAR containing exactly eight bytes in `0x(...)` HEX format.
There is no configurable digest, padding, IV or output length.

### Input boundary

The caller supplies the unchanged bytes of the data stream after the MAC field. The MAC field is excluded. No XML normalization, reserialization, encoding changes or whitespace changes are performed. The inner SHA-256 output is 32 binary bytes, never hexadecimal text. Error Notification messages have no MAC; that protocol decision belongs to the caller.

### Keys and diagnostics

Supply exactly 32 bytes in a genuine KEY reference. Known key size metadata must match
256 bits. Import with GenerateKey(HMAC-SHA256, ...) or separately derive the session
key variant using KDF-EP2-SESSION (sections 5.1.4 and 8.11).
Use MAC-SEND for messages from the master; MAC-RECEIVE for messages to the master.
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
| Parameters | EP2-MESSAGE-MAC, no additional arguments | PARAM |
| Mac | PARAM reference, KEY reference, HEX data | Eight-byte HEX VAR |

GenerateKey, Hash, Derive, Encrypt, Decrypt, Wrap and Unwrap are not supported by this
mechanism. Use the existing HMAC or KDF mechanisms to prepare keys.

## Parameters

Only `#MECH:EP2-MESSAGE-MAC` is supported. Additional and duplicate parameters are forbidden.
Both Parameters(...) and direct `PARAM p = #MECH:EP2-MESSAGE-MAC` declarations are supported.

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

The following official example uses the same key and data as section 5.3.1 (pages 9-36 and 9-37).
The expected result is `0x(96B632C95451623B)`.

```cryptoscript
KEY k = GenerateKey(HMAC-SHA256, 0x(0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345))
PARAM p = Parameters(EP2-MESSAGE-MAC)
VAR data = 0x(11112222333344445555)
VAR result = Mac(p, k, data)
```
