# CryptoWorkBenchEncryptedFile 1.0

F05 wire contract. Extension: `.cwe`. The project owner explicitly authorized
incompatible additions within previously unused version 1.0; no migration is required.
No keys, passwords, filenames or source paths are stored.

## Framing and exact AEAD AAD

| Offset | Bytes | Meaning |
|---|---|---|
| 0 | 8 | ASCII `CWBENC` followed by two zero bytes |
| 8 | 2 | Unsigned big-endian major version 1 |
| 10 | 2 | Unsigned big-endian minor version 0 |
| 12 | 4 | Unsigned big-endian UTF-8 JSON byte length, 1–4096 |
| 16 | JSON length | Strict UTF-8 JSON object, no BOM |
| 16 + JSON length | Payload length minus tagLength | Binary ciphertext |
| File length minus tagLength | tagLength | Binary authentication tag |

For GCM and CCM, **AAD is exactly bytes [0, 16 + JSON length)** of the input
container, including magic, versions, encoded JSON length and the original JSON.
No prefix, delimiter, hash, reserialization or additional raw #ADATA is used.
CryptoScript #ADATA is encoded in `associatedData`, thus authenticated through the
header. Reordering fields or changing whitespace changes the authenticated bytes.
Readers retain the original frame for verification. Tags are 16 bytes for both
AEAD modes, matching the existing VAR contract. Ciphertext excludes the tag.

## JSON schema

Writers emit the following fields in table order, omitting absent optional fields.
Readers accept any field order and JSON whitespace. Fields and enum values are
case-sensitive. All fields are JSON strings except `tagLength`, an integer.
Unknown, duplicate, missing, null, wrongly typed, contradictory and disallowed
fields are rejected, as are comments, trailing commas, trailing JSON, malformed
UTF-8 and unsupported major/minor versions. Binary strings use canonical standard
RFC 4648 Base64 with padding; no whitespace, alternate alphabet or omitted padding.

| Field | Presence and constraints |
|---|---|
| format | Required, `CryptoWorkBenchEncryptedFile` |
| mechanism | Required, one of the seven mechanisms below |
| padding | Required, allowed padding below; `NONE` for CTR/GCM/CCM |
| iv | Required, Base64 of 16 bytes for AES-CBC, 8 for DES3-CBC, empty string otherwise |
| payloadEncoding | Required, `binary` |
| authentication | Required, `GCM` for AES-GCM, `CCM` for AES-CCM, `none` otherwise |
| tagLength | Required, integer 16 for AEAD, 0 otherwise |
| nonce | Required for CTR/GCM/CCM; forbidden otherwise. CTR 12 bytes, GCM nonempty, CCM 7–13 bytes |
| counter | Required only for CTR, exactly 4 bytes, forbidden otherwise |
| counterEncoding | Required only for CTR, `uint32-be`, forbidden otherwise |
| associatedData | Optional only for GCM/CCM, canonical Base64, including empty data |
| plaintextLength | Required only for ISO-9797-M1, forbidden otherwise; decimal string 0..9223372036854775807, no sign/leading zeroes except `0` |

The 4096-byte JSON limit also bounds nonce and associated-data metadata. Values
that exceed it are rejected, never truncated. Lengths are decimal strings to avoid
JSON floating-point precision loss. The M3 internal bit-length block uses an
unsigned 64-bit big-endian length at its right edge; multiplication is checked.

## Mechanism and padding matrix

| Mechanism | Key bits | Padding | Empty plaintext |
|---|---|---|---|
| AES-CBC | 128/192/256 | All nine | All except NONE |
| AES-ECB | 128/192/256 | NONE | Accepted |
| AES-CTR | 128/192/256 | No padding (`NONE` in container) | Accepted |
| AES-GCM | 128/192/256 | No padding, AEAD | Accepted |
| AES-CCM | 128/192/256 | No padding, AEAD | Accepted |
| DES3-CBC | 128/192 | All nine | All except NONE |
| DES3-ECB | 128/192 | All nine | All except NONE |

Nine paddings: NONE, PKCS-7, ANSI-X923, ISO-10126, ISO-7816, ISO-9797-M1,
ISO-9797-M2, ISO-9797-M3, TLS-CBC. CBC/DES3 NONE requires a positive block multiple;
AES-ECB NONE permits zero bytes. Other block ciphertext must be a positive block
multiple. Padding is applied once per whole message, never per input buffer.
M1/M3 produce a zero data block for an empty message, and no extra data block for
an aligned nonempty message. M3 prepends its block-sized big-endian bit length.
M1 uses plaintextLength to restore original trailing zeros and validates removed
zero bytes; VAR decryption intentionally keeps the padded data. M3 validates
length, alignment, high bytes and all trailing zeroes. ISO-10126 filler is random.
TLS encryption emits minimal padding, while decryption accepts up to 256 equal
padding octets with value count-1, matching the existing VAR implementation.

CTR encrypts `nonce[12] || counter[4]`, increments only the unsigned big-endian
counter, and rejects any message requiring a wrapped counter. The final available
counter block is allowed. GCM plaintext is limited to 2^36-32 bytes. For CCM,
q=15-nonceLength and plaintext must be less than 2^(8q) bytes (and within the
signed stream length range). A 12-byte nonce permits at most 16,777,215 bytes.
Limits are checked before payload reads. The implementation has no int-sized
whole-message array limit.

Only GCM/CCM authenticate headers and data. ECB, CBC, CTR and DES3 provide **no
integrity protection**. A valid padding or length is not evidence of authenticity.
They remain reference/legacy mechanisms, not preferred modern file encryption.

## Reproducible reference vector

AES-128-CBC, NONE; NIST SP 800-38A F.2.1 first block:

```text
key       2B7E151628AED2A6ABF7158809CF4F3C
iv        000102030405060708090A0B0C0D0E0F
plaintext 6BC1BEE22E409F96E93D7E117393172A
ciphertext 7649ABAC8119B246CEE98E9B12E9197D
```

Exact metadata (ASCII, no newline):

```json
{"format":"CryptoWorkBenchEncryptedFile","mechanism":"AES-CBC","padding":"NONE","iv":"AAECAwQFBgcICQoLDA0ODw==","payloadEncoding":"binary","authentication":"none","tagLength":0}
```

The adjacent `F01-reference.hex` is the complete container as uppercase hex plus a
final text newline. Decode it to bytes, not including that newline. Reproduce it:

```cryptoscript
KEY k = GenerateKey(AES-CBC, 0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(AES-CBC, #IV:0x(000102030405060708090A0B0C0D0E0F), #PAD:NONE)
PATH source = "message.bin"
PATH encrypted = Encrypt(p,k,source)
```

Create `message.bin` with the 16 plaintext bytes above before running. The test
`NistVectorProducesExactContainer` checks the known ciphertext and exact frame.

## Publication and platform boundary

File processing uses fixed buffers of at most 64 KiB plus block/tag state.
See F05-implementation.md for algorithm selection and controlled-buffer cleanup. Windows creates a private DACL;
Unix creates mode 0600 and macOS removes inherited extended ACLs from the empty file.
Only a completed, flushed temporary file is published. Names are tried in increasing
order, using atomic no-replace operations, never an Exists/write or copy fallback.

Directory traversal is handle-bound and rejects symlink/reparse components. The
source is opened once with no-follow semantics and must be a regular file. Windows
directory handles deny rename; the source denies write/delete sharing. Unix readers
cannot exclude concurrent same-length modifications, which remain a caller responsibility.
Length shrink/growth is detected; streaming is bounded by the initially observed length.

Windows publishes the still-open output using FileRenameInfo with replacement disabled.
Linux links `/proc/self/fd/<output-fd>` to the directory FD with AT_SYMLINK_FOLLOW;
procfs and hard-link support are required, otherwise the operation fails safely.
macOS links relative to the bound directory after comparing device/inode identity
of the temporary entry against the open output handle. As in S02, a trusted output
directory is required there: malicious directory writers can race the identity
check and link. This is a documented trust boundary, not a claim of race freedom
against equal-privilege adversaries. Atomic no-overwrite behavior holds for competing
normal publishers on all supported platforms.

On Unix, the current absolute directory path is obtained from the bound handle just
before publication, so a prior directory move does not return the old path. A stable
namespace is still required during commit and subsequent use; another process can
always rename/delete published files. macOS Intel explicitly uses the INODE64 stat
ABI; macOS ARM64 uses the native ABI. macOS paths through `/var` or `/tmp` symlinks
must use their canonical `/private/...` spelling.

Cancellation is checked per chunk and per name attempt before commit. There is no
post-commit cancellation check. Temp cleanup is best effort; no rollback removes
an already published result. Flush(true) does not promise directory-entry durability
across every power loss/filesystem. The synchronous API remains synchronous; the
asynchronous runner transports its token through the execution/invocation context.

Native ABI references: [Apple stat definitions](https://github.com/apple-oss-distributions/xnu/blob/main/bsd/sys/stat.h),
[Windows handle rename](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle).
