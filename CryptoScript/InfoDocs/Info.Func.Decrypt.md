# Decrypt — decrypt data or a file

`Decrypt(parameters, key, data)` takes three arguments. A declared PATH as the third
argument selects binary streaming decryption and returns PATH. VAR and data literals
retain the existing in-memory VAR behavior. A path string in a VAR or a literal
does not select file processing.

```cryptoscript
KEY k = GenerateKey(AES-CBC, 0x(000102030405060708090A0B0C0D0E0F))
PARAM p = Parameters(AES-CBC)
PATH container = "C:\\Data\\message.bin.cwe"
PATH cleartext = Decrypt(p, k, container)
Print(cleartext)
```

## Container and parameters

Only CryptoWorkBenchEncryptedFile 1.0 containers are supported: `CWBENC\0\0`
magic, version 1.0, at most 4096 bytes of strict UTF-8 JSON metadata, AES-CBC,
binary ciphertext and no authentication tag. Key lengths are 128, 192 or 256 bits.
Ciphertext must contain at least one complete 16-byte block. PKCS-7 supports empty
plaintext; NONE retains every plaintext byte and requires complete blocks.

The stored mechanism, IV and padding describe the file. PARAM must select AES-CBC.
Explicit #IV and #PAD must match the stored values; IV comparison uses decoded
bytes. Automatically generated IVs and padding defaults do not cause conflicts.
The caller's PARAM values and provenance are unchanged.

## File names and contracts

`message.bin.cwe` becomes `message.bin`; `message.bin.1.cwe` becomes `message.bin.1`.
If the name exists, the first free numeric suffix is used: `message.bin.1`,
`message.bin.2`, etc., including gaps. For renamed containers without a final `.cwe`,
the base name is the full container name plus `.decrypted`. At most 1,000,000 names
are attempted. Existing files, directories and links are never replaced.

Declare the result as PATH. Wrong return types, nested file calls and nested
arguments are rejected before file side effects. Declare PARAM and KEY first.
Only a fully decrypted and validated temporary file is atomically published;
the result PATH is registered after success. The original container is unchanged.
Invalid metadata, unsupported versions, truncated data, invalid block lengths,
padding failures and I/O errors fail without publishing partial plaintext.
Cancellation is checked during streaming and before publication; successful
publication wins late cancellation. Temporary plaintext files use private
permissions and are removed on failure where the filesystem permits.

## Security boundaries

**AES-CBC has no integrity protection. Header and ciphertext are unauthenticated.**
Successful padding validation does not establish authenticity or a correct key.
In particular, NONE can accept a wrong key and produce unusable plaintext without
an error. Do not treat decryption success as tamper detection.

Use trusted, stable directories and canonical paths without symlink/reparse-point
components. Source files and directory components are opened through the same
safe infrastructure as Encrypt. Windows denies source writes/deletion while open.
Unix cannot prevent concurrent same-length source changes. Linux publication
requires procfs and hard links; macOS retains the F01/S02 trusted-directory boundary
against hostile writers. Missing atomic publication support fails with no copy
fallback. Managed plaintext and decoded key buffers are cleared where controlled;
deleting a temporary file does not guarantee secure erasure of storage or caches.
