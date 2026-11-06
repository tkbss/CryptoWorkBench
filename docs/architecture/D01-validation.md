# D01 — Delete validation

Date: 2026-10-21. Local validation on Windows, .NET 10, Release.
Baseline and unchanged HEAD: `a692eb7ad94649ad32046206d7a90bda150df911`, branch `master`.
No commit or push performed. Local tracking comparison with `origin/master`: ahead 0, behind 0; no remote refresh performed for this implementation task.

## Implementation and boundaries

`Delete(variable, ...)` resolves identifiers to genuine `SourceVariable` references without evaluating nested calls. Exact uppercase `ALL` is a contextual selector, including for an empty dictionary; lowercase `all` remains an identifier. The existing contextual argument grammar supports FN-token names such as `MasterKey`.

The operation captures the current roots, validates the complete selection and checks remaining PARAM references against selected TR31H objects using reference equality. It prepares the remaining ordered roots and calls `PublishBatch` once. No individual removal, object mutation, automatic relinking or value-based lookup occurs. Remaining identities and metadata are preserved. Embedded headers are not promoted to roots.

Delete is synchronous and void through both runner entry points. Initializer and nested uses are rejected before argument evaluation, including sibling Print calls. Diagnostics distinguish missing arguments, unknown names, duplicates, invalid ALL combinations, invalid argument types, forbidden void use and reference conflicts. Call text is redacted and messages contain no variable material.

No file deletion, zeroization, password request, separate async dispatch, new dependency, GUI implementation change, cryptographic change or S01/S02/Secure-container change was introduced. Test-created temporary files are cleaned up by the test fixture only. `.codex/config.toml` was neither read nor changed. The pre-existing untracked `.codex/` remains outside this change.

## Actual local validation results

| Check | Result |
|---|---|
| Targeted `DeleteTests` | 32 passed, 0 failed, 0 skipped |
| Complete Core suite | 7,667 passed, 0 failed, 4 platform/privilege skips; 7,671 regular tests |
| Complete GUI suite | 239 passed, 0 failed, 0 skipped |
| `CryptoWorkBench.sln`, Release | Success, 0 errors |
| `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln`, Release | Success, 0 errors |
| Documentation | 128 Markdown InfoDocs; catalog/content tests included in passing suites; `Info(Delete)` exercised |
| `git diff --check` | Passed |
| Independent architecture/regression review | No open findings |

Commands used `--no-restore`; full-suite runs additionally used `--no-build` after successful builds. Targeted tests used `--filter FullyQualifiedName~DeleteTests`. TRX files and console logs are ignored local evidence under `artifacts/test-results/D01-{targeted,core,gui}.trx` and `artifacts/D01-{targeted,core,gui,build-core,build-gui}.log`.

The four regular Core skips are:

- `MacOsInheritedExtendedAclIsRemovedBeforePublication`: Darwin extended ACLs, unavailable on Windows.
- `NativePublicationDoesNotFollowLateSymlink`: symbolic-link creation privilege unavailable.
- `TargetSymlinksIncludingDanglingLinksAreRejected(False)` and `(True)`: symbolic-link creation privilege unavailable.

`MeasureLocalHardware` is an explicitly manual benchmark, reported separately by the adapter as skipped/not executed and excluded from the regular test summary above. It is not a failed or platform-skipped test.

Compilation emitted six existing Core nullable warnings (CS8603) and eight existing GUI nullable warnings (CS8602, CS8618, CS8625) across the targeted build and solution builds. The subsequent incremental Core solution build reported only NU1900; the GUI build reported eight nullable warnings plus three project NU1900 warnings. NU1900 indicates the NuGet audit source `https://api.nuget.org/v3/index.json` was unreachable; dependency vulnerability status was therefore not established. Avalonia also printed its Community telemetry notice. Git emitted line-ending normalization notices, without whitespace errors.

## Coverage and independent review

Tests exercise all six runtime types, single/multiple/ALL deletion, empty ALL, exact selector casing, FN-token identifiers, retained identity/order, unchanged removed objects, safe diagnostics and unchanged state on invalid arguments. Reference tests cover shared PARAM references, joint removal, embedded headers and distinct same-named objects. The typed invocation seam additionally rejects stale/value-only references.

The public Save/Delete/Load/Print integration test verifies equal key values and metadata after import, different restored object identity, persistence of the exported file after Delete(ALL), and successful import after redeclaring PATH. Existing S01–S06 regression suites passed unchanged apart from the documentation catalog count adjustment.

An independent agent performed a read-only architecture and regression review of the implementation, grammar/evaluator integration, diagnostics, tests and documentation. It found no actionable issues in atomicity, reference integrity, invalid-call side effects, unchanged remaining objects or existing snapshot behavior. The reviewer did not rerun tests; the executed results above are from the implementation session. No corrective implementation changes were required. Validation is local Windows validation, not a new four-platform CI run.

## Complete D01 file list

14 paths (10 modified, 4 new):

1. `CryptoScript/Antlr/CryptoScript.g4`
2. `CryptoScript/Antlr/CryptoScriptParser.cs` (regenerated with ANTLR 4.13.2)
3. `CryptoScript/CryptoScript.csproj`
4. `CryptoScript/Documentation/InfoDocumentCatalog.cs`
5. `CryptoScript/InfoDocs/Info.Func.Delete.md` (new)
6. `CryptoScript/InfoDocs/Info.Functions.md`
7. `CryptoScript/Model/FunctionCallEvaluator.cs`
8. `CryptoScript/Model/VariableDeclarationEvaluator.cs`
9. `CryptoScript/Operations/DeleteOperations.cs` (new)
10. `CryptoScript/Operations/OperationFactory.cs`
11. `CryptoScript/Operations/OperationList.cs`
12. `CryptoScriptUnitTest/DeleteTests.cs` (new)
13. `CryptoScriptUnitTest/InfoDocumentCatalogTests.cs`
14. `docs/architecture/D01-validation.md` (new)

Index remains unstaged. Generated build outputs, logs and TRX files remain excluded.
