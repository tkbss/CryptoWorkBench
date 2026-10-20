# S02 — Snapshot file storage and validation

Date: 2026-10-20. Repository: `tkbss/CryptoWorkBench`, branch `master`.
Baseline: `def806b7210c5af9f0564d5e2cac9854da7035b0`.
No staging, commit or push. The pre-existing untracked `.codex/` directory is unrelated.

## Changes and public contract

Added to the Core library, in `CryptoScript.Serialization`:

- `VariableSnapshotFileStore`: synchronous `Write` and `Read` for a
  `PathVariableDeclaration`; write takes an explicit `IReadOnlyList<VariableDeclaration>`.
- `VariableSnapshotWriteMode`: `CreateNew` (default) and explicitly selected
  `ReplaceExisting`. Replacement also permits an absent destination.
- `VariableSnapshotFileException` and `VariableSnapshotFileError`: stable, sanitized
  error categories.
- Internal `SnapshotFilePublication`: native same-directory publication without a
  copy fallback.
- Internal `SnapshotFilePermissions`: removal of inherited Darwin extended ACLs.
- `CryptoScriptUnitTest/VariableSnapshotFileStoreTests.cs`: filesystem regression tests.

Example:

```csharp
var store = new VariableSnapshotFileStore(); // 16 MiB encoded-byte limit
store.Write(destinationPathVariable, selectedVariables);
var detachedVariables = store.Read(destinationPathVariable);
// Replacing a prior file requires an explicit choice:
store.Write(destinationPathVariable, selectedVariables,
    VariableSnapshotWriteMode.ReplaceExisting);
```

`PathVariableDeclaration.Value` is the literal OS path, not a quoted CryptoScript
expression. Relative paths resolve once against the process working directory.
Normal absolute paths and Windows UNC paths use OS semantics. The service does
not create missing directories, expand environment variables or `~`, execute
commands, select global variables, or access `VariableDictionary`.
Windows drive-relative/root-relative paths, device namespaces, alternate data
streams, reserved DOS device names and trailing-dot/space aliases are rejected.
Unix colons and backslashes remain ordinary filename characters.
There is no filename Unicode/case normalization beyond `Path.GetFullPath`; the
filesystem determines name equivalence. Empty/null paths, malformed Unicode,
NUL, directories, trailing separators and final symlink/reparse entries are rejected.

## S01 format compatibility and bounded reading

All existing S01 serializer, DTO and codec files are unchanged. Output is the exact
complete `VariableSnapshotJsonSerializer.Serialize` text encoded as UTF-8 without
a BOM, with no wrapper, extra metadata or line-ending conversion. It remains
`CryptoScriptVariables`, version 1. Existing S01 JSON is readable directly.
All six types retain their S01 semantics; root/dependency TR31H object identity is
reconstructed by S01. Loaded objects are returned only after complete validation.
No partial results or publication to the interpreter are possible.

Import uses strict UTF-8 decoding, accepts an optional UTF-8 BOM, and rejects
invalid byte sequences rather than substituting characters. UTF-16/UTF-32 are not
auto-detected. A strict JSON parse inspects typed format/version identifiers before
S01 schema validation, so a future version may have a different schema and still
report `UnsupportedFormat`. The actual v1 reconstruction always uses S01.

`maxFileSizeBytes` is a positive constructor parameter, default **16,777,216 bytes**.
The limit includes any input BOM and applies to writes as well as reads. Reads
check length on the opened handle and count bytes throughout the read, probing
at most one byte beyond the limit. Growth after the length check cannot bypass it.
Writes check the encoded size before allocating the byte array or creating a
temporary file. Full S01 serialization necessarily occurs in memory before this
check: the limit bounds file input/output, not arbitrary caller object graphs.
Parsing, JSON strings and reconstructed objects consume additional memory; this
is not a total-memory quota. A caller should not mutate source objects during a write.

## Preparation, permissions and publication

Write validates its arguments, path, existing target and destination directory,
then fully serializes and encodes the selection. Only then does it create a
random `.cryptoscript-<GUID>.tmp` in that same directory, exclusively with
`FileMode.CreateNew`. It writes all bytes, calls `FileStream.Flush(true)`, closes
the stream, rechecks the destination, and performs native publication.
There is no truncation, in-place write, delete-before-replace or copy fallback.

| Platform | Temporary file protection | Publication |
| --- | --- | --- |
| Windows | A protected DACL with inheritance disabled and one allow rule for the current user SID, supplied during creation. | `MoveFileExW`, no copy flag; replacement flag only for `ReplaceExisting`. |
| Linux | `UnixCreateMode` requests owner read/write (`0600`) during exclusive creation, reduced by umask. Ordinary POSIX ACL permissions are constrained by this mode. | `link` for `CreateNew`; `rename` for `ReplaceExisting`. |
| macOS | Same `0600` creation, then an empty extended ACL applied to the open descriptor before writing content. ACL setup failure aborts and cleans up. | Same `link` / `rename` strategy as Linux. |

On Unix, successful `link` publication leaves two names for the same complete
inode briefly; cleanup removes the temporary name. `EEXIST` cannot overwrite a
late competing destination, including a dangling symlink. Filesystems that do
not support hard links fail instead of exposing a partially copied destination.
`ReplaceExisting` replaces the directory entry; it does not write through a
destination symlink or modify another hard link's file contents.

Successful replacement uses the new private file's permissions and identity;
old target ACLs, ownership, timestamps and other metadata are not preserved.
Existing handles/hard links can continue to reference the old object.
Privileged users, administrators, backup software and the executing user's other
processes are outside the permissions boundary. Filesystems/mounts without
effective per-user permissions (for example some removable or remote volumes)
may ignore or reject requested protection. This implementation does not certify
such a filesystem's confidentiality.

The Windows creation API supplies the security descriptor at file creation.
See [Microsoft's ACL creation API](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemaclextensions.create).
Darwin extended ACLs can supplement mode bits; see
[Apple's file permission model](https://developer.apple.com/library/archive/documentation/FileManagement/Conceptual/FileSystemProgrammingGuide/FileSystemDetails/FileSystemDetails.html)
and [descriptor-based ACL setting](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man3/acl_set.3.html).

## Atomicity, durability and race boundaries

These guarantees assume a regular local filesystem honoring the native
operations and a destination directory and ancestors controlled by the caller.
They do not constitute an OS-independent transaction or crash-safety guarantee.

| Platform | Reachable guarantee and limitation |
| --- | --- |
| Windows | On a supporting local filesystem, a successful same-volume move publishes the fully prepared file. Sharing violations, open handles, antivirus software and competing replacement operations can cause controlled failure. The portable contract does not promise universal atomicity across Windows filesystem drivers or remote servers. |
| Linux | A successful same-filesystem `link` adds a name for the complete inode without replacing an existing entry; `rename` atomically exchanges the destination name. Readers see a complete old or new snapshot under these filesystem assumptions. |
| macOS | Same namespace guarantees for local filesystems supporting `link`/`rename`. ACL hardening precedes content. Neither namespace atomicity nor ordinary flush implies power-loss durability. |

`Flush(true)` requests that file data and intermediate buffers reach the device
before publication. The service does **not** fsync the containing directory after
publication, issue Darwin `F_FULLFSYNC`, or promise storage-controller/remote-server
cache persistence. A crash or power failure can lose the new name/content,
leave a temporary file, or leave old/new state depending on the filesystem.
After Unix link publication, a crash can leave both names. No unlimited
crash-safety or durable transaction is claimed.

Final target symlinks, dangling links, junctions and other reparse entries are
rejected when observed. Write checks before preparation and again immediately
before publication. A final link inserted after that check still cannot redirect
the content write: no destination file is opened for writing; native publication
fails or replaces the link entry itself. The service is not a sandbox:

- Parent directory symlinks/junctions are resolved normally. This supports
  legitimate aliases such as macOS temporary-directory paths.
- Parent directories can be renamed, retargeted or replaced between path-based
  operations. A hostile actor with directory modification rights can redirect
  publication or interfere with the temporary name and its cleanup.
- Read has a check/open race: a final symlink inserted after the check may be
  followed by the subsequent file open. No native `openat`/`O_NOFOLLOW` or
  handle-relative Windows resolution is implemented.
- On macOS, a hostile process could open the initially empty temporary file
  through an inherited ACL before that ACL is removed and retain its handle.
- External in-place changes while reading can cause validation failure or a
  different valid snapshot. There is no cooperative lock protocol, authentication
  or point-in-time guarantee against arbitrary writers.

Use an application/user-owned directory with trusted ancestors. Regular snapshot
files are the supported input; device files and FIFOs are not a supported transport.
Cleanup is best effort after ordinary failures and after successful publication;
cleanup failure never masks the primary error or reports a completed publication
as failed. A surviving temporary file contains plaintext, with the restrictions
described above. The service performs no automatic stale-file deletion.

The native design deliberately avoids the Unix copy fallback in
[.NET's file move implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/IO/FileSystem.Unix.cs).
See also [Windows MoveFileEx](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw),
[POSIX rename](https://pubs.opengroup.org/onlinepubs/9799919799/functions/rename.html),
[POSIX link](https://pubs.opengroup.org/onlinepubs/9799919799/functions/link.html),
and [.NET Flush(Boolean)](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush).

## Error contract

All service errors use fixed messages containing only the category, without
paths, original exception messages, inner exceptions, key bytes, passwords or JSON.
No payload is logged by the service.

| Category | Examples |
| --- | --- |
| `InvalidArgument` | Null selection, undefined write mode, nonpositive limit |
| `InvalidPath` | Invalid path syntax, directory target, final symlink/reparse point |
| `AlreadyExists` | Existing regular target under `CreateNew`, including a publication race |
| `NotFound` | Missing input or missing destination directory |
| `AccessDenied` | OS permission failure, Windows publication sharing/access failure |
| `WriteError` / `ReadError` | Other I/O failures, including read sharing failure |
| `InvalidSnapshot` | S01 serialization/validation failure, malformed JSON, invalid UTF-8 |
| `UnsupportedFormat` | Well-typed unknown envelope format or version |
| `SizeLimitExceeded` | Encoded file size exceeds configured bound |

Errors observed first take precedence: for example an existing target under
`CreateNew` is rejected before attempting to serialize invalid source variables.
No variables are returned on any failed read. Preparation errors leave an
existing destination's content unchanged.

## Validation results

Local runtime: Windows x64, .NET SDK 10.0.401 / runtime 10.0.12.
Linux and macOS execution is not available in this local Windows run; the existing
CI matrix will execute the platform branches after a separately authorized commit/push.
No S02 CI execution is claimed.

Commands from the repository root:

```powershell
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore --logger 'trx;LogFileName=S02-core.trx' -v minimal
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore --logger 'trx;LogFileName=S02-gui.trx' -v minimal
dotnet build CryptoWorkBench.sln --no-restore -m:1 -v minimal
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore -m:1 -v minimal
git diff --check
```

| Validation | Result |
| --- | --- |
| Complete Core suite | **7,210 passed, 0 failed, 4 skipped; 7,214 total** |
| S02 cases within the full Core run | **34 passed, 0 failed, 4 skipped; 38 total** |
| Complete GUI suite | **225 passed, 0 failed, 0 skipped** |
| Both solution builds | **Succeeded; 0 errors and 0 warnings in final incremental builds** |
| Whitespace check | **Clean**: `git diff --check`, plus no-index whitespace checks of all six new files |

The four skipped cases are the macOS-only inherited-ACL test and three symlink
cases requiring Windows privilege unavailable to this account (OS error 1314).
The late regular-file competitor test passed independently of symlink support.
Unix/macOS branches and native symlink behavior need those platforms' CI runs.
UNC/network-share behavior and forced disk-full/flush failures were not exercised.
The GUI recompilation emitted nine existing nullable warnings in unchanged GUI
files; no new S02 compiler warnings remain.

Raw TRX evidence is in the test projects' ignored `TestResults/S02-core.trx`
and `TestResults/S02-gui.trx`. Command logs are in ignored `artifacts/S02-*.log`.

Test coverage includes all six variable types, exact S01 bytes/BOM behavior,
Unicode filenames/data, native path scenarios, explicit replacement/default
non-overwrite, missing directories, serialization failures, permission and
sharing errors, invalid/unsupported JSON, byte limits, permission hardening,
concurrent writers, late competitors, link handling, cleanup, unchanged source
state/global variables, and root/embedded shared TR31H references.
Every fixture owns a unique temporary directory and removes it in teardown;
permission tests restore access rules before cleanup.

The initial sandboxed testhost failed to start and was interrupted. Authorized
execution outside the sandbox worked. Initial test corrections addressed Windows
ACL restoration (a freshly read security descriptor is not marked dirty),
Windows symlink privilege error 1314, expected competing-rename access failures,
and runtime construction of an unpaired surrogate instead of an attribute string.

## Independent review

A separate reviewer inspected the implementation and tests without editing them.
Findings and actions:

1. **P2, corrected:** Unknown format/version was initially checked after full v1
   DTO validation. Inspection now checks the typed envelope first; regression
   tests cover future/foreign envelopes with different schema.
2. **Permissions hardening, applied:** Mode `0600` alone cannot eliminate inherited
   Darwin extended ACL grants. Added descriptor-based ACL removal before writing
   and a macOS regression test with an actual inherited allow ACE.
3. **Test coverage, applied:** Added destination-directory write denial and
   preservation of the prior target, including ACL/mode restoration.

The reviewer rechecked the corrections, final implementation, this report, TRX
counts and build logs. **No remaining concrete findings.** The review explicitly
confirmed the documented race/durability limits and the outstanding native
Linux/macOS execution; it did not claim to have executed those platforms.

## Scope

No Save/Load/SecureSave/SecureLoad commands, ANTLR changes, ALL selection,
encryption/password KDFs, GUI/Scriptbook integration or S01 format change.
Files contain plaintext secret material; encryption is explicitly outside S02.
