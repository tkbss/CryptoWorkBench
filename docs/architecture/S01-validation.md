# S01 local validation report

Date: 2026-10-20. Branch: `master`. No commit, staging or push performed.

## Files

Modified:

- `CryptoScript/Variables/ParameterVariableDeclaration.cs`: internal factory for
  restoring private state without setters, default generation or value rebuilding.

Added:

- `CryptoScript/Serialization/VariableSnapshotJsonSerializer.cs`: public service,
  graph staging, ID/reference resolution and atomic detached return.
- `CryptoScript/Serialization/SnapshotDtos.cs`: explicit version-1 DTOs.
- `CryptoScript/Serialization/SnapshotJson.cs`: strict JSON/schema validation and
  isolated Newtonsoft settings.
- `CryptoScript/Serialization/KeySnapshotCodec.cs`: key metadata, material and KCV.
- `CryptoScript/Serialization/ParameterSnapshotCodec.cs`: exact parameter state
  and existing declaration/creation-contract validation.
- `CryptoScript/Serialization/HeaderSnapshotCodec.cs`: immutable definitions and
  legacy raw headers.
- `CryptoScript/Serialization/SecretSnapshotCodec.cs`: descriptors, shares and
  original metadata through existing validating constructors.
- `CryptoScriptUnitTest/VariableSnapshotJsonSerializerTests.cs`: 67 NUnit cases.
- `docs/architecture/S01-variable-json-snapshots.md`: format specification,
  per-type JSON examples, graph semantics and S02–S04 boundary.
- `docs/architecture/S01-validation.md`: this report.

## Roundtrip and operational verification

| Type | State checked | Result |
| --- | --- | --- |
| VAR | Every existing format, exact text, numeric representation, GMAC, type ID | Passed |
| KEY | Algorithm/material kind, numeric/text size, Usage, derivation, ordered attributes, all full KCV values; AES/TDEA/HMAC/Unknown and RSA/EC public/private material | Passed |
| PARAM | Ordered values, generated IV/nonce, changed values, explicit provenance, cached text, empty state, header identity | Passed |
| PATH | Windows, UNC and Unix strings; null and declared runtime type | Passed |
| TR31H | Attributes, expected/unresolved length, optional blocks and padding, immutability, legacy values | Passed |
| SECRET | Split and both Shamir profiles, complete/incomplete/empty sets, all share formats, X/bytes/order and original metadata | Passed |

Reconstructed objects successfully perform AES encryption, TR-31 wrap/unwrap and
explicit SECRET restoration. Below-threshold sets deserialize successfully and
still fail explicit restoration as intended. Real interpreter-created header
reference graphs are covered as well as detached graphs.

Further checks cover shared and distinct same-named header objects, anonymous
dependencies, no publication of embedded headers, no global variable changes,
absence of Avalonia references, hostile global JSON settings, missing fields at
every DTO level, duplicate/unknown fields, malformed shapes, integer types/ranges,
invalid parameters, mismatched key metadata, malformed ASN.1 and missing references.
Inherited variable/type source text is also preserved for every type and kept
inside the protectable payload, including structured VAR/PATH when needed.

## Final commands and results

Run from the repository root with .NET 10:

```powershell
dotnet test CryptoScriptUnitTest\CryptoScriptUnitTest.csproj --no-restore --filter FullyQualifiedName~VariableSnapshotJsonSerializerTests --logger 'console;verbosity=normal'
dotnet test CryptoScriptUnitTest\CryptoScriptUnitTest.csproj --no-build --logger 'trx;LogFileName=S01-core.trx' -v minimal
dotnet test CryptoWorkBenchGuiTest\CryptoWorkBenchGuiTest.csproj --no-restore --logger 'trx;LogFileName=S01-gui.trx' -v minimal
dotnet build CryptoWorkBench.sln --no-restore -m:1 -v minimal
dotnet build CryptoWorkBenchAvalonia\CryptoWorkBenchAvalonia.sln --no-restore -m:1 -v minimal
git diff --check
```

- Snapshot tests: **67 passed**, zero failed/skipped.
- Complete Core suite: **7,176 passed**, zero failed/skipped.
- Complete GUI suite: **225 passed**, zero failed/skipped.
- Both solution builds: succeeded; final incremental builds report zero
  warnings/errors.
- Whitespace/diff check: clean.
- TRX evidence is in each test project's ignored `TestResults` directory.

Earlier verification encountered one testhost connection timeout and one
multi-worker solution build that exited unsuccessfully with no diagnostic errors.
The test retry and single-worker builds succeeded. GUI recompilation
reported nine nullable warnings in unchanged GUI files; no GUI source was changed.

## Local and independent review

After the first successful full suites and solution builds, a separate local
review pass re-read the implementation against runtime constructors, parameter
contracts and graph semantics.

Findings corrected:

1. Parameter restoration initially applied only selected support checks.
   It now applies the existing `Parameters` contract where supported, including
   mechanism-specific value checks. TR-31 literal declarations retain their
   existing path because those mechanisms do not support `Parameters()`.
2. `FunctionContractException` and Bouncy Castle's `SecurityUtilityException`
   needed inclusion in the material-free snapshot error boundary.
3. Root-membership checks used repeated linear list scans. Reference-based
   hash membership now avoids quadratic graph classification.

The local review added 16 test cases, including RSA/EC mathematical sizes, malformed DER,
all mandatory DTO fields, parameter value validation, generated/modified nonce
state, real runtime graphs, distinct same-named dependencies and failure after
valid entries.

A separate reviewer agent then inspected the implementation independently and
reproduced two P2 findings against the built Core assembly:

1. Inherited `Statement.Text` was omitted, including `CryptoType.Text`. Both
   now roundtrip via `value.source`. VAR/PATH use a structured payload if needed,
   because original declaration text can contain secret literals.
2. Missing nested RSA objects in otherwise shaped ASN.1 caused Bouncy Castle to
   throw `NullReferenceException`. A local boundary around the asymmetric key
   factories now translates malformed-object exceptions to the documented
   material-free `JsonSerializationException`.

Eight additional regression cases cover these fixes, including the reviewer's
exact public/private DER reproductions. The existing common property comparisons
also check source text now. Both complete suites and both solution builds were
rerun after these corrections; results above describe the final source.

The independent reviewer rechecked the corrected source and current Debug
assembly, repeated both DER reproductions, and checked eight VAR/PATH source/null
type combinations. Both findings were confirmed closed, with no new concrete
findings in the corrections. The reviewer made no filesystem changes.

This review is not a formal security audit.

## Format boundaries

- Plaintext snapshots only; no Save/Load commands, encryption, password KDF,
  filesystem I/O, GUI or Scriptbook integration.
- Custom variable subclasses and unsupported future formats fail explicitly.
- Legacy TR31H text remains exact and retains its existing operation-time
  validation. A structured definition never resolves KBLEN during restore.
- The current header model does not retain whether an existing padding block was
  originally explicit or automatic; its complete observable immutable state is
  preserved.
- Operation-time input completeness and cryptographic authentication remain with
  the existing operations and future secure container. KCV is not authentication.

## Git state

One existing source file modified; ten new S01 files untracked. No files staged.
The pre-existing untracked `.codex/config.toml` was left untouched.
