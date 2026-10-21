# S04 validation — CryptoScript Load

Date: 2026-10-21 (Europe/Zurich). Repository: `tkbss/CryptoWorkBench`.
Branch: `master`. Baseline and final HEAD:
`76d621104898c32ebf33f94f31e2cd8b84569b3c`.

Implementation and validation are local, on Windows x64 with .NET SDK 10.0.401.
No commit, staging, push or CI run was performed. No Avalonia application source
or cryptographic algorithm was changed. `.codex/config.toml` was neither read nor
modified; the pre-existing untracked `.codex/` directory was left untouched.

## Function contract

`Load(path)` and `Load(path, ALL)` import every snapshot root. Explicit identifiers
select one or more roots in argument order and do not require a live dictionary
entry. The first argument must be a genuine, currently declared PATH reference.
`ALL` remains contextual and case sensitive; it cannot be mixed with other
selectors. Duplicate or unknown selectors fail. Lowercase `all` is ordinary.

The optional final `#ONCONFLICT:REJECT` or `#ONCONFLICT:REPLACE` is accepted at most
once. REJECT is the default and fails the entire import on any selected name
conflict. REPLACE inserts new roots and replaces existing roots with complete
deserialized objects, preserving all metadata and references. There is no PATH
exemption: the source PATH may itself conflict or be replaced after reading.
Unselected live variables remain the same objects.

The grammar maps ONCONFLICT as a raw parameter node, without variable-value
resolution. Snapshot selectors have their own resolved argument kind. The option
is documented in the parameter registry but excluded from PARAM runtime fields
and the S01 PARAM metadata allowlist. Preflight rejects it in other functions.
Invalid recognized literal values receive sanitized semantic errors; malformed
Load syntax is redacted in both lexer and parser diagnostics, including FileBuild
console diagnostics.

Load returns `null`, without an artificial return variable. All declaration types
reject direct Load initializers. A recursive argument-tree preflight rejects
nested Save/Load before evaluating any sibling, so earlier nested operations
cannot produce side effects. Direct Load argument resolution also rejects
expressions and nested calls without evaluating them.

## Preparation and publication

1. Resolve the current PATH reference and check argument node shapes.
2. Call only `new VariableSnapshotFileStore().Read(path)` for persistence. S02/S01
   validate the entire file and reconstruct every root and dependency before
   returning. There is no second JSON deserializer or script execution in Load.
3. Validate option placement/value, duplicate selectors, ALL combinations and
   selected names against the fully validated snapshot.
4. Select detached root objects, check existing-name conflicts and references.
5. Construct the complete replacement dictionary privately, retaining all
   unselected live objects.
6. `VariableDictionary.PublishBatch` enumerates and validates the complete state
   into a private dictionary and publishes it with one reference assignment.
   No callback or fallible operation follows that assignment in Load.

Every failure before publication leaves the existing dictionary, its object
identities, metadata and reference graph unchanged. This is atomic publication
within the synchronous interpreter, not a general thread-safety redesign or
a transaction spanning multiple independent CryptoScript statements.

S01 object identity is retained: two imported PARAM roots sharing a TR31H still
share the same header. A selected PARAM can retain a nonselected root header or
embedded header as an internal dependency; this does not publish another root.
No imported reference is linked to a live object by identifier equality.

REPLACE is rejected if an unchanged existing PARAM directly references the exact
old object being replaced. `ReferenceEqualityComparer` is used for the replaced
set. Replacing every referrer together with the header is allowed. A reference to
a different, detached header with the same identifier does not block replacement.
`PARAM.HeaderReference` is the only direct variable-to-variable reference in the
six supported runtime variable types.

## Required, narrowly scoped S01 corrections

S02 is unchanged. Two S01 changes were necessary and each has a regression test
that failed before the correction and passed afterward:

- `VariableSnapshotJsonSerializer.ValidIdentifier` previously required the lexer
  token ID, rejecting the requested `MasterKey` and `SessionKey` names because
  their spelling produces FN. It now accepts ID or FN after the unchanged ASCII
  identifier regex. This does not execute function-shaped names; actual reserved
  type/mechanism tokens remain rejected. The Load-specific grammar accepts these
  contextual names without changing ordinary declaration or Save grammar.
  Evidence: `FunctionShapedSnapshotIdentifiersCanBeSelectedWithoutExecutingThem`
  and `artifacts/S04-identifier-before.log`.
- `ParameterSnapshotCodec` obtained its allowlist from every lexer `P_` token.
  Adding ONCONFLICT would therefore silently expand the S01 PARAM format,
  particularly for TR31 parameters. An explicit exclusion preserves the former
  allowlist. The regression corrupts an unselected PARAM root with ONCONFLICT and
  proves the whole import fails before publishing the selected valid header.
  Evidence: `LoadOptionCannotBeSmuggledIntoSnapshotParamMetadata` and
  `artifacts/S04-param-before.log`.

Format, version, byte-size, variable-type, metadata and reference validation
otherwise remain unchanged. No snapshot migration or version change was added.

## Changed files

All paths below are repository relative. Generated artifacts were regenerated
with ANTLR 4.13.2; generated trailing whitespace was removed as in prior phases.

| Area | Files |
| --- | --- |
| Grammar | `CryptoScript/Antlr/CryptoLexer.g4`; `CryptoScript/Antlr/CryptoScript.g4` |
| Generated lexer/parser metadata | `CryptoScript/Antlr/CryptoScript.interp`; `CryptoScript/Antlr/CryptoScript.tokens`; `CryptoScript/Antlr/CryptoScriptLexer.interp`; `CryptoScript/Antlr/CryptoScriptLexer.tokens` |
| Generated C# | `CryptoScript/Antlr/CryptoScriptBaseListener.cs`; `CryptoScript/Antlr/CryptoScriptBaseVisitor.cs`; `CryptoScript/Antlr/CryptoScriptLexer.cs`; `CryptoScript/Antlr/CryptoScriptListener.cs`; `CryptoScript/Antlr/CryptoScriptParser.cs`; `CryptoScript/Antlr/CryptoScriptVisitor.cs` |
| Arguments/AST mapping | `CryptoScript/Arguments/Argument.cs`; `CryptoScript/Model/AntlrToFunctionCallExpression.cs`; `CryptoScript/Model/OperationInvocation.cs` |
| Evaluation/dispatch | `CryptoScript/Model/FunctionCall.cs`; `CryptoScript/Model/FunctionCallEvaluator.cs`; `CryptoScript/Model/VariableDeclarationEvaluator.cs`; `CryptoScript/Operations/OperationFactory.cs`; `CryptoScript/Operations/OperationList.cs`; `CryptoScript/Operations/SnapshotOperations.cs` |
| State and parameter scope | `CryptoScript/Variables/VariableDictionary.cs`; `CryptoScript/Variables/ParameterTypeList.cs`; `CryptoScript/Model/ParameterRegistry.cs` |
| S01 boundary corrections | `CryptoScript/Serialization/VariableSnapshotJsonSerializer.cs`; `CryptoScript/Serialization/ParameterSnapshotCodec.cs` |
| Syntax diagnostics | `CryptoScript/ErrorListner/LexerErrorListener.cs`; `CryptoScript/ErrorListner/SyntaxErrorListner.cs`; `CryptoScript/ErrorListner/SecretImportDiagnosticScope.cs` renamed/generalized to `CryptoScript/ErrorListner/ImportDiagnosticScope.cs` |
| Documentation registration | `CryptoScript/CryptoScript.csproj`; `CryptoScript/Documentation/FileInfoDocumentationProvider.cs`; `CryptoScript/Documentation/InfoDocumentCatalog.cs` |
| InfoDocs | new `CryptoScript/InfoDocs/Info.Func.Load.md`; `CryptoScript/InfoDocs/Info.Func.Save.md`; `CryptoScript/InfoDocs/Info.Functions.md`; `CryptoScript/InfoDocs/Info.Parameters.md` |
| Core tests | new `CryptoScriptUnitTest/LoadFunctionTests.cs`; `CryptoScriptUnitTest/AntlrLanguageMetadataTests.cs`; `CryptoScriptUnitTest/InfoDocumentCatalogTests.cs` |
| GUI tests | new `CryptoWorkBenchGuiTest/LoadInfoTests.cs` |
| Validation report | new `docs/architecture/S04-validation.md` |

The Info page is copied with the Core assembly and registered for `Info(Load)`,
catalog lookup, the function overview and the existing internal-link navigation.
The GUI regression verifies opening the Load link and returning to the overview.
No Avalonia completion or overlay implementation was changed.

## Tests and final results

| Validation | Result |
| --- | --- |
| Load-only public execution/parser tests | **95 passed**, 0 failed |
| Load + Save + S01/S02 focused suite | **261 passed**, 4 skipped, 0 failed |
| Full Core suite | **7,415 passed**, 4 skipped, 0 failed; 7,419 total |
| Full GUI suite | **226 passed**, 0 failed |
| `CryptoWorkBench.sln` Debug build | Success, 0 errors, 0 warnings in final incremental output |
| `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` Debug build | Success, 0 errors, 0 warnings in final incremental output |
| `git diff --check` | Passed |

The GUI test compilation emitted the nine existing nullable warnings in unchanged
Avalonia source files. No warning suppression was added. The four Core skips are
the macOS inherited-ACL test, the native late-symlink publication test, and two
target-symlink tests requiring platform privileges unavailable on this Windows
run. They are not Load failures.

The tests exercise all six variable types, exact Save/Load/Save roundtrip bytes,
selective names absent from the live dictionary, implicit and explicit ALL,
default/explicit REJECT, REPLACE with mixed old/new names, whole-object replacement
across types, duplicate/missing selectors, bad parameter values and positions,
PATH conflicts, root and embedded/shared header identity, conservative reference
rejection, detached same-name headers, empty snapshots, and rollback identity and
serialized-state checks on failures. Corruption in unselected roots and reference
targets still aborts the import. Invalid JSON, future versions, invalid UTF-8,
missing files, directory paths and oversized files preserve sanitized S02 error
categories. Serialized script-looking text remains data. Unknown-algorithm KEY
imports reproduce the AES-128 FIPS-197 known answer without metadata mutation.

The syntax tests cover malformed HEX/Base64, unsupported nested parameter values,
missing separators and unterminated strings in both in-memory and console parser
paths. Semantic and unexpected-exception tests check redacted call text, no raw
payload in messages and no inner exception transport. Save and Secret diagnostic
regressions also pass.

Commands used (PowerShell; output logs in ignored `artifacts/`):

```text
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --filter FullyQualifiedName~LoadFunctionTests --logger "trx;LogFileName=S04-targeted-final.trx" -v minimal
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore --filter "FullyQualifiedName~LoadFunctionTests|FullyQualifiedName~VariableSnapshot|FullyQualifiedName~SaveFunctionTests" --logger "trx;LogFileName=S04-focused-final.trx" -v minimal
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --logger "trx;LogFileName=S04-core-final.trx" -v minimal
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore --logger "trx;LogFileName=S04-gui-final.trx" -v minimal
dotnet build CryptoWorkBench.sln --no-restore -m:1 -v minimal
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore -m:1 -v minimal
git diff --check
```

ANTLR regeneration, from `CryptoScript/Antlr`:

```text
java -jar antlr4-4.13.2-complete.jar -Dlanguage=CSharp -visitor CryptoScript.g4
```

Final logs: `artifacts/S04-targeted-final.log`, `S04-focused-final.log`,
`S04-core-final.log`, `S04-gui-final.log`, `S04-build-core.log` and
`S04-build-gui.log`. TRX files reside in the respective projects' ignored
`TestResults` directories. An initial sandbox run could not connect to its local
testhost; the validation runs succeeded outside that sandbox. Initial full-suite
failures exposed the new parameter registry entry and two fixed inventory counts;
these were corrected without weakening the registry or metadata assertions.
An initial new GUI assertion incorrectly expected the existing hardcoded Info
completion list to contain all functions. It was replaced with the requested
documentation navigation check, preserving the no-Avalonia-change constraint.

## Independent review

The read-only `s04_review` agent reviewed the uncommitted implementation against
the full user specification, including AST resolution, publication, reference
identity, diagnostics, Save compatibility and documentation. It reported:

1. **P1:** an invalid Base64 ONCONFLICT value could reach raw syntax diagnostics.
   Fixed by semantic handling of recognized literal forms and scoped redaction
   for malformed Load parser/lexer diagnostics, with regression tests.
2. **P2:** three historical `VAR/PARAM/KEY = Load(TYPE, path)` examples remained
   in the function overview. Removed and replaced by the implemented void API.

The independent follow-up review confirmed both fixes, exercised additional
parser probes and reported no remaining findings. A final independent check of
the PARAM snapshot allowlist correction and its failing-before/passing-after test
also reported no findings. All final test suites and both builds passed after
these corrections.

## Limits and delivery state

This phase was validated locally on Windows x64 only; Linux x64, macOS x64 and
macOS ARM64 were not rerun. Earlier phases' cross-platform validation is baseline
context, not evidence of an S04 cross-platform run. File access, filesystem races,
link handling and the 16 MiB limit retain the S02 guarantees and limitations.
Snapshots remain plaintext and unauthenticated; no SecureLoad/SecureSave or
cryptographic algorithm changes are included.

The S01 name correction permits function-shaped snapshot names specifically for
Load selection. It does not extend ordinary declaration/reference grammar in
other functions. Existing Save behavior for previously valid scripts is unchanged.
Publication assumes the existing synchronous interpreter model; concurrent host
mutation of the global dictionary is outside this contract.

Local implementation, review and requested validation are complete. Changes are
uncommitted, the index remains empty, and HEAD remains the stated baseline.
