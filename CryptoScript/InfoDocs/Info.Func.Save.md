# Save — export variables as plaintext JSON

```cryptoscript
Save(path, variable)
Save(path, variable1, variable2, ...)
Save(path, ALL)
```

The first argument must reference an existing `PATH` variable. Remaining arguments
must reference existing variables; literals, parameters and nested function calls
are not accepted. Explicit selections retain their argument order. Repeating a
selected variable is an error. The destination PATH can itself be selected once.

`ALL` is case sensitive and special only in Save's selection arguments. It selects
a snapshot of every currently declared variable in dictionary order, including
the destination PATH, other PATH variables, TR31H definitions and SECRET shares.
It cannot be combined with another selection, including another `ALL`.
A variable named `ALL` remains usable in other functions and as the first PATH
argument; `Save(path, ALL)` always means the complete selection, even if a
variable named `ALL` exists. Lowercase `all` is an ordinary variable reference.

```cryptoscript
PATH destination = "snapshot.json"
VAR message = "Hello"
KEY key = GenerateKey(AES-ECB, 128)
Save(destination, message, key)
```

Choose a new destination for a subsequent export:

```cryptoscript
PATH complete = "complete-snapshot.json"
Save(complete, ALL)
Info(Save)
```

Quoted paths support the normal CryptoScript string escapes, including `\\` for
a Windows backslash. Absolute bare Windows, UNC and POSIX paths are also accepted;
place a bare path at the end of its declaration line. Relative paths resolve
against the process working directory. The destination directory must exist.

Save has no return value. `VAR result = Save(...)` and using Save as a nested
argument are errors, rejected before Save accesses the filesystem.

Save uses the version 1 S01 snapshot format and S02 file store without changing
their contracts. Supported runtime classes are VAR, KEY, PARAM, PATH, TR31H and
SECRET. The serializer preserves embedded/shared TR31H references. Unsupported or
inconsistent runtime objects cause an error; Save does not repair or convert them.
Literal declarations such as `KEY k = 0x(00112233445566778899AABBCCDDEEFF)`
and valid `b64(...)` literals create complete KEY objects and can be exported.
Their algorithm remains `Unknown`, their bit size comes from the decoded bytes,
and KCV/KCVC/KCVK remain unavailable. Save and the snapshot roundtrip preserve
these properties. A suitable mechanism such as AES can use such a key without
changing its metadata, provided its material is technically valid for that
mechanism. See `Info(types)` for KEY metadata.

Files use UTF-8 without BOM. Save always uses `CreateNew`: an existing target is
never overwritten, including a target created by a competing process. S02
validates and serializes before preparing a private temporary file, flushes it
and publishes it in the same directory. Temporary-file cleanup is best effort.
The default S02 size limit is 16 MiB. Filesystem permissions, link handling,
atomicity and durability have the limitations documented in
`docs/architecture/S02-validation.md`; this is not an unconditional crash-safety
guarantee. Use a destination directory and ancestors you control.

**Security:** this file is not encrypted or authenticated. KEY material and
SECRET shares are exported in full. Saving enough shares together can permit
recovery of the original key. Restrictive file permissions do not provide
encryption, custodial separation or protection from privileged processes.
Save does not implement SecureSave, execute snapshot contents, modify source
variables or publish new variables. Use [Load](cryptoscript-info://function/Load)
to import snapshots with explicit conflict handling.
