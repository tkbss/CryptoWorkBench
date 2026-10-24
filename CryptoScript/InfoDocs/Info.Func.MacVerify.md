# MacVerify — verify metadata and original file

```cryptoscript
VAL valid = MacVerify(p,k,container)
Print(valid)
```

Requires exactly PARAM, KEY and PATH references. PATH must name a `.cwm` file.
Only direct calls returning VAL are supported; nested calls and nested arguments
are rejected before file operations. VAL is the result type holding lowercase
`true` or `false`. It is distinct from VAR and PATH.
VAL declarations require a Boolean function result, not a literal. Subsequent
value edits accept exactly `true` or `false`; snapshots do not serialize VAL.

The bounded JSON reader first checks CWM format/version, required fields,
algorithm labels, mechanism-specific parameter schemas and full tag lengths,
HEX representations, file size and the relative filename. This validation uses
the stored mechanism independently of the caller's PARAM.
It rejects duplicate keys and unsafe references. It then canonicalizes fileMac
with RFC8785-JCS, derives the metadata key via HKDF-SHA256 and verifies the full
HMAC-SHA256. The original file is never opened before successful metadata
authentication. Authenticated mechanism and effective parameters must agree with
the provided PARAM. The original file is then read incrementally through the
existing secure file infrastructure; its size and file MAC are verified.

| Condition | Result |
|---|---|
| Metadata HMAC, file MAC and file size valid | VAL `true` |
| Well-formed metadata HMAC wrong, including a different valid KEY | VAL `false` |
| File contents or stored size disagree | VAL `false` |
| Invalid JSON/schema/HEX/tag length, unsupported version/algorithm | Semantic execution error |
| Invalid PARAM/KEY, authenticated PARAM mismatch | Semantic execution error |
| Unsafe path, missing file, symlink/reparse point or I/O failure | Semantic execution error |

MAC bytes are compared in constant time. On false, a VAL is returned; on error
no result variable is registered. Invalid call shapes/declaration types use
FunctionContractViolation; runtime failures use FunctionExecutionFailed with a
redacted diagnostic. Cancellation propagates without registering a result.

A malformed or unsupported algorithm label is an error even if the original
metadata HMAC would also fail. An otherwise valid but unauthenticated fileMac
change returns false. Verification creates no files and modifies no inputs.

See [MacPack](cryptoscript-info://function/MacPack) for the exact CWM v1 schema,
mechanisms, effective parameters, JCS domain, HKDF salt/info/output length,
independent verification procedure and filesystem limitations.
