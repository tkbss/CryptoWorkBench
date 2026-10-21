# SecureSave

Saves selected variables as an authenticated password-protected container. This is a void, asynchronous statement: the host must use `ExecuteAsync` and provide the runner's password dialog. It cannot initialize a variable or be nested in another function.

```cryptoscript
SecureSave(path, variable)
SecureSave(path, variable1, variable2)
SecureSave(path, ALL)
SecureSave(path, ALL, #SECUREMODE:GMAC, #SECUREKDF:PBKDF2, #PBKDFITER:600000)
SecureSave(path, variable, #ARGONMEM:65536, #ARGONITER:3, #ARGONLANES:4)
```

The first argument must reference an existing PATH variable. Selectors use Save's object-reference semantics. `ALL` selects all current variables in dictionary order, including the PATH variable. Duplicate selectors are rejected. Options must follow all selectors; their order is arbitrary, and each may occur once.

| Option | Default | Accepted values |
|---|---|---|
| `#SECUREMODE` | `GCM` | `GCM`, `GMAC` |
| `#SECUREKDF` | `ARGON2ID` | `ARGON2ID`, `PBKDF2` |
| `#ARGONMEM` | `65536` | 65536–262144 KiB |
| `#ARGONITER` | `3` | 3–6 |
| `#ARGONLANES` | `4` | 1–4 |
| `#PBKDFITER` | `600000` | 600000–2000000 |

Argon options are valid only with Argon2id; PBKDFITER only with PBKDF2. Argon memory must be divisible by `4 * lanes`, at least `8 * lanes`, and `memoryKiB * iterations` must not exceed 786432. Parameters are never silently reduced.

GCM encrypts the exact S01-v1 JSON and authenticates its public header and manifest with AES-256-GCM. GMAC authenticates the header and exact S01 text using an empty AES-GCM plaintext: **all saved values remain readable in GMAC mode**. Both use a fresh random 16-byte salt, independent random 12-byte nonce, and 16-byte tag. Argon2id v1.3 is the default memory-hard password KDF. PBKDF2 uses HMAC-SHA256. HKDF-SHA256 separates the final AES key by format version, mode and KDF.

The host asks for a password and confirmation after syntax, selection, snapshot and size validation. Passwords use strict UTF-8, 1–1024 bytes, without trimming or Unicode normalization. Use a long, unique passphrase. Cancellation or closing the dialog stops execution without publishing. The absolute destination and detached snapshot are fixed before the dialog.

The public manifest reveals root identifiers, types, and, for KEY roots, algorithm, actual bit length and the full existing KCV (AES/TDEA), or null where unavailable. It excludes dependency-only headers. GCM does not expose key material, PATH contents, PARAM values, SECRET shares or source declarations. Public metadata is untrusted until authentication succeeds.

The destination must not exist (`CreateNew`); there is no overwrite or copy fallback. A private temporary file in the destination directory is flushed and atomically published using the existing S02 platform protections. Cancellation before publication cleans up; successful publication is final. Callers must control the destination directory and its ancestors; parent-directory replacement races are outside this guarantee.

Limits: 16 MiB UTF-8 S01 payload, 96 MiB complete file, 1 MiB each for manifest JSON and manifest binary framing, 4096 roots, and 256 ASCII characters per identifier. Only one interpreter execution/KDF runs at a time. The trusted S01 serializer can allocate managed strings/objects before final payload size validation; these are not process-memory limits.

Owned password bytes, keys and plaintext byte buffers are cleared. Immutable GUI/S01 strings, GC copies and BouncyCastle internal working memory cannot be guaranteed fully erased. Authentication does not guarantee password strength or protect against a compromised host. SecureLoad is not yet available; existing Load reads plaintext S01 files only.
