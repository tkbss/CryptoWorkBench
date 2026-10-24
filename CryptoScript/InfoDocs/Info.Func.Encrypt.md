# Encrypt — encrypt data or a file

`Encrypt(parameters, key, data)` always takes three arguments.

| Third argument | Result | Processing |
|---|---|---|
| Existing VAR or supported data literal | VAR | Existing in-memory encryption |
| Declared PATH variable | PATH | Binary streaming file encryption |

```cryptoscript
KEY k = GenerateKey(AES-CBC, 256)
PARAM p = Parameters(AES-CBC, #PAD:PKCS-7)
PATH source = "C:\\Data\\message.bin"
PATH encrypted = Encrypt(p, k, source)
Print(encrypted)
```

File mode supports AES-CBC and DES3-CBC/ECB with all nine CryptoScript paddings,
AES-ECB with NONE, and AES-CTR/GCM/CCM without padding. AES keys are 128/192/256
bits; DES3 keys are 128/192 bits. CBC/DES3 NONE requires a positive block multiple.
AES-ECB allows an empty file. All other valid combinations accept empty plaintext.

The result is the absolute path of the completed file. The declaration must be
PATH; a mismatched declaration fails before opening files. File calls cannot be
nested or contain nested calls. Declare PARAM and KEY first. A VAR containing a
path string never selects file processing; direct path literals are not supported.

The source is unchanged. Output names are `message.bin.cwe`, `message.bin.1.cwe`,
`message.bin.2.cwe`, etc. The first free name is used, including gaps. Existing
files, directories and links are never overwritten. This numbering is not a
version history. At most 1,000,000 candidates are attempted; invalid or overlong
names fail without truncation.

Each `.cwe` contains a CryptoWorkBenchEncryptedFile 1.0 binary frame, a UTF-8 JSON
header, binary ciphertext and (for GCM/CCM) a separate 16-byte binary tag.
IV/nonce/counter and optional associated data are stored, never keys or source paths.
Each operation gets a fresh automatic IV/nonce without changing PARAM. Explicit
values are preserved; preventing unsafe IV/nonce reuse is the caller's responsibility.
CTR uses a 12-byte nonce and a four-byte big-endian counter and rejects overflow.
CCM with the default 12-byte nonce accepts at most 16,777,215 plaintext bytes;
explicit nonces of 7–13 bytes select the corresponding CCM limit.

GCM/CCM authenticate the exact binary frame and JSON header as AAD, including
Base64-encoded #ADATA. Their tags therefore differ from VAR tags with only the
original #ADATA. Block/CTR ciphertext and padding retain VAR semantics. ISO-10126
contains random filler. M1 stores the original length for lossless PATH restoration.

**CBC, ECB, CTR and DES3 have no integrity protection.** They are reference and
legacy mechanisms, not preferred modern file encryption. GCM/CCM provide integrity
but still require appropriate key handling and nonce uniqueness.

Failed processing does not register the result variable or publish partial output.
Temporary files are removed where permitted. ExecuteAsync cancellation is checked
between chunks and before publication; successful publication wins late cancellation.
The synchronous runner retains its existing contract. GUI hosts must run long
operations on a worker, as for the existing asynchronous runner.

Use a trusted, stable directory and canonical paths without symlink/reparse-point
components. Windows denies source writes/deletion while reading. Unix retains a
stable source handle but cannot prevent another process from modifying its bytes;
observed length changes fail, while same-length writes cannot reliably be detected.
Linux requires procfs and hard-link support. On macOS publication retains the
existing snapshot trust boundary: hostile writers to the target directory are
not supported. Failures have no copying fallback. Use
[Decrypt(PATH)](cryptoscript-info://function/Decrypt) to decrypt completed containers.
Mac(PATH) streams a raw file MAC. MacPack/MacVerify additionally provide
authenticated CWM containers; see [MacPack](cryptoscript-info://function/MacPack).
