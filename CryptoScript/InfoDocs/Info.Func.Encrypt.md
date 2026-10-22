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

The file mode currently supports AES-128/192/256-CBC and PKCS-7 or NONE only.
PKCS-7 accepts empty files and every byte length. NONE requires a nonempty multiple
of 16 bytes, matching the in-memory mode. Other mechanisms and padding schemes
remain available in the VAR mode.

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
header and binary ciphertext. The header stores the mechanism, padding and actual
IV, but no key or original path. An explicitly supplied #IV is used exactly.
When #IV was generated automatically, each file gets a fresh random IV without
changing PARAM. Reusing an explicit IV is the caller's responsibility.

**AES-CBC has no integrity protection. Neither metadata nor ciphertext is
authenticated.** The container does not detect malicious modification.

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
not supported. Failures have no copying fallback. Decrypt(PATH) and Mac(PATH) are
not implemented in this release.
