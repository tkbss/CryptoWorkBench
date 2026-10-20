# S03 validation — CryptoScript Save

Date: 2026-10-20. Repository: `tkbss/CryptoWorkBench`, branch `master`.
Baseline: `3b97a5bfd13afa6248a355bff804470d79333efd`.
Local environment: Windows x64, .NET SDK 10.0.401.

## Implementation and API decisions

- `Save(path, variable...)` exports existing variable objects in argument order.
  `ResolvedCallArgument.SourceVariable` carries the actual references; there is
  no value-based lookup, copy, conversion or publication to VariableDictionary.
- `Save(path, ALL)` takes an array snapshot of the dictionary's current values
  in enumeration order. It includes the destination PATH exactly once, as well
  as other PATH, TR31H and SECRET objects. Snapshot membership is captured
  synchronously; the existing global interpreter dictionary is not made
  thread-safe by this change.
- ALL remains an ordinary lexer identifier. Only the exact uppercase identifier
  in Save's selection positions acts as the selector. It cannot be combined
  with any other selection. `ALL` remains usable as a variable elsewhere and as
  the first PATH argument; lowercase `all` remains an ordinary selection.
  Grammar files, generated ANTLR code and AST mapping are unchanged.
- Save validates the entire argument syntax before argument evaluation.
  Literals, mechanisms, inline parameters and nested calls are rejected without
  executing them. All references must exist; the operation also verifies their
  current dictionary identity and rejects duplicate selected objects or IDs.
  The path's role is separate from selection: `Save(path, path)` is valid.
- The only persistence call is
  `VariableSnapshotFileStore.Write(path, selected, VariableSnapshotWriteMode.CreateNew)`.
  There is no new JSON implementation or direct file I/O in the operation.
- Both the typed invocation factory and legacy function-existence precheck
  recognize Save. The legacy string-only entry rejects execution because it
  cannot carry source references. Typed operation factory delegates now
  explicitly allow a nullable result; Save returns null, not a synthetic VAR.
  Two existing test helpers explicitly assert that their cryptographic
  operations still return variables.
- Every declaration initialized directly by Save is rejected before argument
  evaluation or file I/O, independent of declared type. Save as a nested
  function argument is also rejected before executing Save.
- PATH literal declarations now map to ExpressionPath/PathVariableDeclaration
  for quoted strings (with the lexer-supported string escapes) as well as bare
  absolute Windows, UNC and POSIX paths. Relative quoted paths are supported.
  Numeric and HEX initializers produce a controlled declaration error. Other
  declared types retain their existing mapping. OS path validation stays in S02.
- Diagnostics use UnknownVariable, FunctionContractViolation,
  InvalidDeclaration or FunctionExecutionFailed as appropriate. S02's sanitized
  error category is retained in the message. Save's runtime FunctionCall.CallText
  and diagnostic FunctionCall are `Save(<redacted>)`; unknown exceptions get
  the fixed message `Save failed.` with no inner exception or payload.
  Save diagnostics propagate unchanged through enclosing functions. Existing
  functions' diagnostics otherwise retain their previous behavior.

## Compatibility and limitations

The S01 version 1 JSON format and all S01/S02 implementation and test files are
unchanged. Serialization and deserialization retain shared or embedded TR31H
reference identity. Save inherits S02's UTF-8 without BOM, 16 MiB default size
limit, restrictive temporary-file permissions, flush/publication ordering,
CreateNew collision protection and best-effort temporary cleanup.

Filesystem guarantees remain exactly those documented in
[S02-validation.md](S02-validation.md), including platform limitations,
parent-directory/link races and the absence of unconditional crash durability.
Save does not add encryption, authentication, overwrite mode or script loading.

One existing runtime limitation is deliberately preserved: `KEY k = 0x(...)`
creates a StringVariableDeclaration tagged KEY. S01 rejects the class/type
mismatch. Save reports InvalidSnapshot instead of silently converting it.
Typed KEY results such as GenerateKey and Restore are supported. This is
documented in Info(Save) and covered by a failure test; no other type's
declaration behavior was changed.

The Save InfoDocs page includes syntax, examples, selection order, contextual
ALL semantics, PATH escaping, CreateNew behavior, limitations and an explicit
warning that key material and secret shares are stored unencrypted. The page
is registered in the catalog/provider, operation list, functions overview and
output-copy configuration. `Info(Save)` is exercised through the interpreter.

## Changed files

Production/runtime:

- `CryptoScript/Operations/SnapshotOperations.cs` (new)
- `CryptoScript/Operations/OperationFactory.cs`
- `CryptoScript/Operations/OperationList.cs`
- `CryptoScript/Model/FunctionCallEvaluator.cs`
- `CryptoScript/Model/VariableDeclarationEvaluator.cs`
- `CryptoScript/Model/SemanticDiagnosticExecution.cs`

Documentation and registration:

- `CryptoScript/InfoDocs/Info.Func.Save.md` (new)
- `CryptoScript/InfoDocs/Info.Functions.md`
- `CryptoScript/Documentation/InfoDocumentCatalog.cs`
- `CryptoScript/Documentation/FileInfoDocumentationProvider.cs`
- `CryptoScript/CryptoScript.csproj`
- `docs/architecture/S03-validation.md` (new)

Tests:

- `CryptoScriptUnitTest/SaveFunctionTests.cs` (new)
- `CryptoScriptUnitTest/InfoDocumentCatalogTests.cs` (catalog count)
- `CryptoScriptUnitTest/AesDes3SemanticDiagnosticsTests.cs` (result assertion)
- `CryptoScriptUnitTest/PinBlockSemanticDiagnosticsTests.cs` (result assertion)

No changes to Avalonia, SecureSave, cryptography, S01/S02 or `.codex/config.toml`.

## Validation results

All numbers below are actual local results, not projected CI counts.

| Check | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| SaveFunctionTests | 54 | 0 | 0 |
| Complete Core suite | 7,275 | 0 | 4 |
| Complete GUI suite | 225 | 0 | 0 |

The Core suite contains 7,279 cases. Its four unchanged local skips are:

- `MacOsInheritedExtendedAclIsRemovedBeforePublication`: macOS-only ACL test.
- `NativePublicationDoesNotFollowLateSymlink`: Windows symlink privilege absent.
- `TargetSymlinksIncludingDanglingLinksAreRejected(False)`: same privilege.
- `TargetSymlinksIncludingDanglingLinksAreRejected(True)`: same privilege.

The 54 Save cases exercise the public parser, AST mapping, evaluator and runtime:
single/multiple selection; all six runtime classes; ALL and a variable named
ALL; destination selection; shared/embedded header identities; Unicode; quoted,
bare and relative paths; independent Windows/UNC/POSIX declaration mapping;
source immutability; duplicate/unknown/invalid arguments; rejection of nested
side effects and return-value usage; existing files; filesystem and serialization
errors; sanitization of unexpected exception messages; and Info(Save).
All filesystem operations in these tests stay within uniquely created temporary
directories, removed in teardown; platform syntax-only cases perform no I/O at
their example paths. Dictionary contents and event handlers are restored.

Commands (all exit code 0):

```powershell
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore --filter FullyQualifiedName~SaveFunctionTests --logger "trx;LogFileName=S03-targeted.trx" -v minimal
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --logger "trx;LogFileName=S03-core.trx" -v minimal
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore --logger "trx;LogFileName=S03-gui.trx" -v minimal
dotnet build CryptoWorkBench.sln --no-restore -m:1 -v minimal
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore -m:1 -v minimal
git diff --check
```

Both Debug solution builds succeeded with zero errors and zero warnings in the
final incremental build output. The GUI test build also emitted the nine
existing nullable warnings from unchanged Avalonia files. The nullable factory
result initially exposed three warnings in two Core test helpers; explicit
non-null result assertions resolved those, with the final targeted rebuild
emitting none. No warnings were suppressed.

Local logs: ignored `artifacts/S03-targeted.log`, `S03-core.log`, `S03-gui.log`,
`S03-build-core.log`, `S03-build-gui.log`; TRX files are under the respective
test projects' ignored TestResults directories.

The first targeted run had four test-fixture failures (three uses of a
Parameters construction route that does not support typed TR31H references,
and an Info event subscribed as Print). The fixtures were corrected to use the
existing typed declaration route and InfoEvent. All final tests passed.

## Independent review

Independent read-only agent review (`s03_review`) covered the uncommitted diff,
including argument validation, ALL selection, SourceVariable identity, S02-only
persistence, error redaction, void return contract, PATH mapping, documentation
registration, regression tests and the two nullable-result assertions.
Result: **no findings**. No production correction was required after review.
Additional boundary/security tests were then run successfully with the full
Core suite.

## Delivery state

Local implementation and requested validation complete. No commit, push or new
CI run was performed. HEAD remains the baseline; index is empty. The pre-existing
untracked `.codex/` directory was left untouched.
