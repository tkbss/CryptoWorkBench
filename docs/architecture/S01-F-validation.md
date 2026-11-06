# S01-F: Snapshot serializer hardening validation

Date: 2026-10-21. Repository: `tkbss/CryptoWorkBench`, branch `master`.
Baseline and unchanged HEAD: `be627795aa0c1f14e4104f9e6915d2f59e747041`.
Review input: ignored local report `artifacts/S01-R-review.md`.

## Scope and exact changes

| File | Change |
|---|---|
| `CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31AttributeRegistry.cs` | R02: `DecodeText` uses an explicit `JsonSerializer.Create` instance and reader, with invariant culture, no type-name handling and additional-content checking. Global `JsonConvert.DefaultSettings` and its converters no longer participate in header string decoding. |
| `CryptoScript/Serialization/ParameterSnapshotCodec.cs` | R03: a non-null typed header reference additionally requires a non-null validated `Tr31HeaderDefinition`. |
| `CryptoScriptUnitTest/VariableSnapshotJsonSerializerTests.cs` | 13 new cases: one global-converter isolation test, four invalid typed legacy-reference cases and eight SECRET negative cases. Shared rejection assertions check no returned roots, generic diagnostics, no InnerException and unchanged global objects/state. |
| `CryptoScriptUnitTest/VariableSnapshotFileStoreTests.cs` | Three existing positive cases now use validated header definitions instead of constructing inconsistent typed references to legacy/empty headers. Existing exact-byte, roundtrip and shared-identity assertions remain intact. |
| `docs/architecture/S01-variable-json-snapshots.md` | R04: accurate ID/FN identifier contract and grammar limitation. R03: narrow compatibility restriction. R01: complete S01-v1 JSON proposed as encrypted inner payload, no general public metadata/identifier exemption; public projection requires separate approval; GMAC-only has authenticity but no confidentiality. |
| `docs/architecture/S01-F-validation.md` | This validation and compatibility record. |

The header change is a string-decoding isolation fix at the shared registry boundary,
not a cryptographic algorithm change. No public API was added. HeaderSnapshotCodec,
Tr31HeaderDefinition.Create and SnapshotJson were inspected; their existing call
chain now reaches the isolated decoder without needing a broader refactoring.
Tr31HeaderBinding and ArgumentParameter.SetValue were inspected and unchanged.
The new reference check aligns snapshot restoration with the latter's existing
validated-definition requirement.

S01 JSON version remains 1. S02 production file infrastructure, Save/Load syntax,
REJECT/REPLACE, grammar, cryptographic algorithms and Avalonia application code
are unchanged. No SecureSave/SecureLoad implementation is included.

## Compatibility

Valid typed TR31H graphs, shared references, embedded dependencies and standalone
legacy headers retain their existing representation. Previously accepted
inconsistent snapshots whose non-null PARAM.HeaderReference points to a TR31H
without a definition are now rejected. This applies to both deserialization and
serialization's detached validation pass. It prevents such a typed reference
from falling into the legacy/raw operation path.

The four negative reference cases cover a plausible legacy string, corrupted
legacy text containing a sensitive marker, null legacy data and an incorrectly
typed legacy payload. None returns earlier valid roots. Standalone legacy raw
text continues to be preserved verbatim under the established contract; this
change does not introduce general wire-header validation for standalone legacy
objects. Existing operation-time validation remains responsible for those values.

The initial full snapshot regression run exposed three test fixtures that had
constructed precisely the now-rejected state using the internal factory. Only
their header setup was corrected. Tests were not removed and assertions were not
weakened. The subsequent regression and full Core runs passed.

R02 does not change valid header data or v1 schema. Host-installed Newtonsoft
converters no longer alter this decoding path. The regression installs a custom
string converter, restores the prior DefaultSettings delegate in finally, checks
exact LB optional-block contents and serialized roundtrip bytes, and requires
zero converter calls during both reconstruction and validation.

R01 is a proposed future protection contract superseding the earlier public
metadata design, not a current encryption guarantee or format migration.

## Tests and builds

Local Windows x64, .NET 10, Debug configuration. No new remote CI was run.

| Validation | Passed | Skipped | Failed | Total |
|---|---:|---:|---:|---:|
| Targeted new hardening cases plus existing standalone legacy/shared-identity cases | 16 | 0 | 0 | 16 |
| Final S01/S02/Save/Load regressions | 274 | 4 | 0 | 278 |
| Full Core suite | 7,428 | 4 | 0 | 7,432 |
| Full GUI suite | 226 | 0 | 0 | 226 |

The snapshot regression group consists of S01 serializer 80 passed, S02 file store
45 passed / 4 skipped, Save 54 passed and Load 95 passed. The 13 additional cases
increase the baseline Core total from 7,419 to 7,432. Test groups overlap and are
not additive independent coverage counts.

New SECRET cases: absent profile property; null Shamir profile; malformed Base64;
empty Base64; X=256; more shares than descriptor.Parts using distinct valid X
values; unknown share format; unknown profile. Each uses an earlier valid root,
requires the fixed sanitized error message and no InnerException, and checks
that the global variable collection and observed state remain unchanged. Existing
valid SECRET scenarios, typed/shared-header cases and standalone legacy roundtrip
remain in the passing full suite.

Four existing platform/privilege-dependent skips:

- `MacOsInheritedExtendedAclIsRemovedBeforePublication`
- `NativePublicationDoesNotFollowLateSymlink`
- `TargetSymlinksIncludingDanglingLinksAreRejected(False)`
- `TargetSymlinksIncludingDanglingLinksAreRejected(True)`

Both `CryptoWorkBench.sln` and
`CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` built successfully with
`--no-restore -m:1`; each final incremental build reported zero warnings/errors.
The GUI test build emitted nine existing nullable warnings in unchanged Avalonia
code (CS8618, CS8625, CS8602). No new warning in changed code was reported.
The Avalonia build also printed its existing telemetry/licensing informational
message. `git diff --check` passed; Git printed LF-to-CRLF conversion notices for
edited text files, not whitespace errors.

Commands (PowerShell; output captured in ignored `artifacts/S01-F-*.log`):

```text
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore --filter "FullyQualifiedName~Hardening|FullyQualifiedName~LegacyHeaderPreservesRawValue|FullyQualifiedName~SharedHeaderIdentitySurvivesRootOrderOrEmbedding" --logger "trx;LogFileName=S01-F-targeted.trx" --results-directory artifacts/S01-F -v minimal
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore --filter "FullyQualifiedName~VariableSnapshot|FullyQualifiedName~SaveFunctionTests|FullyQualifiedName~LoadFunctionTests" --logger "trx;LogFileName=S01-F-regressions-final.trx" --results-directory artifacts/S01-F -v minimal
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --no-restore --logger "trx;LogFileName=S01-F-core.trx" --results-directory artifacts/S01-F -v minimal
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore --logger "trx;LogFileName=S01-F-gui.trx" --results-directory artifacts/S01-F -v minimal
dotnet build CryptoWorkBench.sln --no-restore -m:1 -v minimal
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore -m:1 -v minimal
git diff --check
```

TRX evidence is in `artifacts/S01-F/`. Logs:
`artifacts/S01-F-targeted.log`, `S01-F-regressions-final.log`, `S01-F-core.log`,
`S01-F-gui.log`, `S01-F-build-core.log`, `S01-F-build-gui.log`.
The initial failed fixture run is retained separately as
`S01-F-regressions.log` / `S01-F/S01-F-regressions.trx`
(271 passed, 4 skipped, 3 failed); it is not the final validation result.

## Independent review

The read-only `s01f_review` reviewer independently inspected R02's complete call
chain, R03's runtime/reference contract, new tests and architecture documentation
against the requested scope. The initial review reported no findings. Final
follow-up review of the corrected FileStore fixtures and this validation record
also reported no findings and approved local completion. The reviewer checked
TRX/log counts, both build outcomes, unchanged fixture assertions, the documented
initial failure and compatibility restriction, HEAD, empty index and 0/0
ahead/behind. No review-driven production correction was required after the
passing full-suite runs.

## Delivery state

No commit or push. Index remains unchanged/empty. HEAD remains the stated
baseline, with ahead/behind 0/0 against the existing origin/master tracking ref.
The six listed files constitute this change; the pre-existing untracked `.codex/`
directory is excluded. `.codex/config.toml` was neither read nor modified.
Local validation only: no claims of new Linux/macOS or remote CI results.
