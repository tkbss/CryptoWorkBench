# Hash

`VAR digest = Hash(parameters, source)` computes an unkeyed digest over memory data
or the exact bytes of a binary file. Exactly two arguments are required.

## Binary file example

Create the source file before executing this example and supply its absolute path:

```text
PARAM p = Parameters(HASH-SHA256)
PATH source = "C:/data/input.bin"
VAR digest = Hash(p, source)
Print(digest)
```

File mode requires actual PARAM and PATH variable references in a direct Hash call.
The result must be declared VAR; nested file Hash calls and nested argument functions
are rejected before file access. A VAR containing a filename and a string literal
remain in-memory data: neither opens a file. Literal paths cannot select file mode.
Existing memory HEX, Base64 and UTF-8 inputs retain their semantics.

## Mechanisms and result

All eleven registered HASH mechanisms are supported: HASH-SHA1, HASH-SHA224,
HASH-SHA256, HASH-SHA384, HASH-SHA512, HASH-SHA512-224, HASH-SHA512-256,
HASH-SHA3-224, HASH-SHA3-256, HASH-SHA3-384 and HASH-SHA3-512.
The file-mode result contains the complete digest in `0x(HEX)` with uppercase digits.
No padding, truncation, text decoding, newline conversion or other byte transformation
is applied. Empty files are supported. There are no extra HASH parameters.

## File processing and safety

Files are read incrementally in 65,536-byte blocks with bounded working memory.
Hash uses the existing path validation and bound-directory FilePublication opening:
only regular files are accepted; source symbolic links and ancestor symbolic links
or reparse points are rejected. Parent directories must remain trusted, as with
Encrypt and Mac. No output or temporary file is created, and the source is read-only.
Temporary byte buffers are cleared on completion, failure and cancellation.
Cancellation and any failure prevent registration of a result variable.

The opened source length is checked before reading, after each read, at EOF and
after digest finalization. Detectable growth, shrinkage or premature EOF fails.
On Windows the existing handle sharing denies ordinary concurrent writes/deletion.
Unix does not enforce this sharing policy. Same-length writes, changes reverted
between checks, existing writable mappings or privileged writers cannot in general
be detected on every platform. This is not an atomic filesystem snapshot. Use a
stable source under trusted directory control for a reproducible result. Supported
native platforms are Windows x64, Linux x64, macOS x64 and macOS ARM64.

## Hash versus MAC

A hash has no secret key and does not authenticate the file or its origin. Compare
it against a trusted expected digest to detect changes. Use a keyed MAC for message
authentication; [MacPack](cryptoscript-info://function/MacPack) and
[MacVerify](cryptoscript-info://function/MacVerify) provide authenticated CWM metadata.
SHA-1 is retained for compatibility and is unsuitable for collision resistance.

[Function list](cryptoscript-info://functions)
