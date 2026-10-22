# CryptoWorkBenchEncryptedFile 1.0

Status: F01 implementation for review; not a release approval. Extension: `.cwe`.
The format is independent of snapshot serialization and contains no key, password,
source filename, source path or user metadata.

## Wire contract

| Offset | Size | Meaning |
|---|---|---|
| 0 | 8 | ASCII `CWBENC`, followed by two zero bytes |
| 8 | 2 | Unsigned big-endian major version, 1 |
| 10 | 2 | Unsigned big-endian minor version, 0 |
| 12 | 4 | Unsigned big-endian JSON byte length, excluding this 16-byte prefix |
| 16 | 1–4096 | Strict UTF-8 JSON object, without BOM |
| 16 + JSON length | Remaining file | Binary AES-CBC ciphertext; no tag in 1.0 |

Writers emit compact JSON in this exact order: `format`, `mechanism`, `padding`,
`iv`, `payloadEncoding`, `authentication`, `tagLength`. Readers accept any object
field order and JSON whitespace, but reject missing, duplicate or unknown fields,
comments, trailing commas, malformed UTF-8, trailing JSON, wrong value types and
noncanonical IV encodings. String field values are case-sensitive.

| Field | Required 1.0 value |
|---|---|
| format | `CryptoWorkBenchEncryptedFile` |
| mechanism | `AES-CBC` |
| padding | `PKCS-7` or `NONE` |
| iv | Canonical RFC 4648 standard Base64, padded, exactly 16 decoded bytes |
| payloadEncoding | `binary` |
| authentication | `none` |
| tagLength | JSON integer 0 |

The codec validates framing and metadata only, leaves the input at ciphertext,
and returns the exact frame bytes. A future decryptor must additionally validate
the remaining payload length: a nonempty multiple of 16 for both supported paddings,
then validate PKCS-7 during decryption. Empty plaintext with PKCS-7 produces one block.
The raw binary frame (prefix plus exact JSON bytes) is reserved as future AEAD AAD;
never reconstruct AAD by reserializing JSON. A future mechanism can define a trailing
tag and corresponding tagLength, but 1.0 must not accept such metadata today.

Readers reject every version other than 1.0, including unknown minor versions.
New fields or mechanisms require an explicit format revision and reader policy;
breaking framing or meaning requires a new major version. Unknown fields must
never be silently dropped before a future authentication check.

**This AES-CBC format is unauthenticated. Metadata and ciphertext can be modified
without detection. Correct padding is not proof of integrity.**

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

File processing uses a 64 KiB plaintext buffer and the existing AES-CBC cipher,
padding selection and NONE length validator. Windows creates a private DACL;
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
