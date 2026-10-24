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

CryptoWorkBenchEncryptedFile 1.0 supports AES-CBC (all nine paddings), AES-ECB
(NONE), AES-CTR/GCM/CCM (no padding), and DES3-CBC/ECB (all nine paddings).
AES keys are 128/192/256 bits; DES3 keys are 128/192 bits. Metadata is strict UTF-8
JSON, at most 4096 bytes. GCM/CCM have a separate 16-byte tag and authenticate the
original complete binary header as AAD. #ADATA is embedded in that header.

PARAM must select the stored mechanism. Explicit #IV, #NONCE, #COUNTER, #ADATA
and #PAD must match stored values; binary values compare decoded bytes. Automatic
defaults cause no conflicts. PARAM values and provenance remain unchanged.
Unsupported parameter/padding combinations are rejected.

M1 uses its stored original length and validates removed zero bytes, preserving
original trailing zeros; VAR M1 decryption still retains all padded bytes. M3
validates its full length block and zero suffix. AES-ECB permits empty ciphertext;
CBC/DES3 require a positive block multiple. AEAD empty plaintext still requires a tag.

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

GCM/CCM reject altered metadata, ciphertext and tags and wrong keys before publication.
Unverified plaintext is confined to a private temporary file, removed on failure.

**CBC, ECB, CTR and DES3 have no integrity protection. Header and ciphertext are
unauthenticated.** These are reference/legacy mechanisms, not preferred modern file encryption.
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
