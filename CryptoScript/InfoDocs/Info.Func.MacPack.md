# MacPack — authenticated file MAC container

```cryptoscript
PARAM p = Parameters(AES-CMAC)
KEY k = GenerateKey(AES-CMAC, 128)
PATH source = "C:\\Data\\input.bin"
PATH container = MacPack(p,k,source)
VAL valid = MacVerify(p,k,container)
Print(valid)
```

Exactly three genuine PARAM, KEY and PATH references are required. Declare these
first. File function calls cannot be nested or contain nested arguments. MacPack
returns PATH; incompatible declarations fail before file operations. The original
bytes are unchanged. `Mac(p,k,source)` continues to return only the raw VAR tag.

The result is `input.bin.cwm`, then `input.bin.1.cwm`, `input.bin.2.cwm`, etc.,
using the first available name (up to 1,000,000 candidates). A privately created
temporary file is flushed and atomically published without replacing any existing
file, directory or link. Cancellation and failures before publication leave no
completed container. Cancellation after successful publication does not undo it.

## CWM version 1

A container is a UTF-8 JSON document without a BOM, at most 65,536 bytes and eight
JSON nesting levels. JSON comments, trailing commas, duplicate keys, unknown fields
and malformed Unicode are errors. Field names are case-sensitive. Property order,
whitespace and valid alternative string escapes are immaterial to verification.

```json
{
  "format": "CWM",
  "version": 1,
  "fileMac": {
    "file": "input.bin",
    "size": 1234,
    "mechanism": "AES-CMAC",
    "parameters": {},
    "mac": "00112233445566778899AABBCCDDEEFF"
  },
  "metadataAuth": {
    "mechanism": "HMAC-SHA256",
    "canonicalization": "RFC8785-JCS",
    "keyDerivation": "HKDF-SHA256",
    "mac": "0000000000000000000000000000000000000000000000000000000000000000"
  }
}
```

The example tags are placeholders. These are the exact required fields; all
strings and integers have the displayed JSON types. `version` is the integer
literal `1`. `size` is a nonnegative integer literal, at most 9,007,199,254,740,991,
measured on the same open handle that supplies the MAC bytes. No floating-point
or exponent notation is accepted. All binary fields use uppercase HEX, two
characters per byte, with no prefix, parentheses, separators or truncation beyond
the file algorithm's explicit macLength. metadataAuth.mac is exactly 32 bytes.

`file` is one relative filename in the container directory. Empty names, dot and
dot-dot, path separators, absolute paths, colon/ADS references, control characters,
Windows wildcard characters and trailing dots/spaces are rejected on every OS.
Moving a container together with its original file preserves the reference.

## File MAC and effective parameters

All F03 file mechanisms are supported: AES-CMAC, DES3-CMAC, AES-GMAC, AES-CBC-MAC,
DES3-CBC, DES3-RETAIL and all eleven registered HMAC mechanisms. `mechanism` is
the canonical registry name. The MAC covers exactly the unchanged binary file
bytes, with F03 padding/truncation applied once to the complete message. Reading
uses a 64-KiB buffer regardless of file size. Transfer files in binary mode without
character-set, newline or record-framing conversion.

The exact `parameters` schema depends on the mechanism:

| Mechanism | Required fields |
|---|---|
| AES-CMAC, DES3-CMAC, all HMAC | Empty object; standard full-length tags |
| AES-GMAC | `nonce`: HEX of the actual supplied NONCE; full 16-byte tag |
| AES-CBC-MAC | `padding`: PAD name; `macLength`: integer 8..16; `iv`: 16 zero bytes in HEX |
| DES3-CBC | `padding`: PAD name; `macLength`: integer 4..8; `iv`: 8 zero bytes in HEX |
| DES3-RETAIL | `padding`: ISO-9797-M1 or ISO-9797-M2; `macLength`: integer 4..8 |

The effective CBC-MAC IV is always zero, including DES3-CBC. The IV used by
DES3-CBC encryption in PARAM does not affect its MAC. CMAC/HMAC ignore unrelated
stored PAD/IV values. GMAC consumes PARAM's NONCE exactly; callers must ensure
nonce uniqueness for each message under a key. Padding constraints, empty-file
behavior and key-quality warnings remain those of F03 (NONE needs nonempty full
blocks). ISO-9797-M3 uses the source handle's length for its length prefix.

## Metadata authentication and independent verification

Only `fileMac` is authenticated. Format, version and metadataAuth algorithm labels
are fixed protocol constants and must match exactly. No second user key is used.

1. Decode KEY to its raw bytes (not its HEX text). These bytes are HKDF input key
   material, for AES, TDEA and HMAC alike.
2. Apply RFC 5869 HKDF-SHA256 Extract-and-Expand with the literal UTF-8 salt
   `CryptoWorkBench/CWM/v1/HKDF-SHA256`, literal UTF-8 info
   `CryptoWorkBench/CWM/v1/metadataAuth/HMAC-SHA256`, no terminating NUL, and a
   32-byte output. These domain strings are fixed by CWM version 1.
3. Canonicalize the complete `fileMac` object using
   [RFC 8785 JCS](https://www.rfc-editor.org/rfc/rfc8785.html), encoded as UTF-8.
4. Compute the complete HMAC-SHA256 using the derived key over those bytes alone.
   Compare all 32 bytes in constant time.
5. After authenticating metadata, check the stored algorithm/effective parameters
   against PARAM, safely open the relative source, reproduce its file MAC, and
   compare its size and tag. No filename, size or parameter is excluded from JCS.

For independent HKDF reproduction: `PRK = HMAC-SHA256(salt, KEY)` and
`Kmeta = HMAC-SHA256(PRK, info || 0x01)` (one expansion block).

The CWM canonicalizer implements the schema's restricted JCS domain: objects,
Unicode strings and safe integers. It sorts property names by UTF-16 ordinal
order, preserves Unicode without normalization, uses RFC string escapes, emits no
whitespace and encodes strict UTF-8. It rejects values outside that domain rather
than pretending to be a general-purpose floating-point JCS serializer. For all
accepted integers, invariant decimal formatting is identical to ECMAScript JCS.
Tests use the RFC's Unicode sorting and safe-integer examples, string escaping,
and an independent HKDF/HMAC computation. Ordinary JSON serialization is used
only for the outer transport, never for the authenticated byte sequence.

## Filesystem boundary

Use trusted directories and canonical paths without symlink/reparse components.
Directory traversal and source opening reuse F01 FilePublication. Windows source
handles deny writes/deletion during reads. Unix handles bind file identity but
cannot prevent same-length concurrent writes; observed length changes fail.
Linux requires procfs and hard-link support. macOS retains F01's trusted-directory
boundary for publication. Bouncy Castle key schedules cannot be explicitly erased;
managed raw key and derived-key buffers are cleared where exposed by the API.

See [MacVerify](cryptoscript-info://function/MacVerify) for result semantics.
