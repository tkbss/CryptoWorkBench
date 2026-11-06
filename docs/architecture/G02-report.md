# G02 – PCI PIN-block translation

## Baseline and scope

Implemented against `master`, `5285f078af587b62f3c8a3fcd2495e973ef18d33` (G01). The supplied request ends during section 14; the complete preceding matrix defines 13 Allowed, 5 ICC-only and 7 Forbidden transitions. Existing untracked `.codex/` content was not changed.

`PinBlockTranslate(sourceParams, sourceKey, encryptedPinBlock, targetParams, targetKey)` returns an encrypted VAR. All five arguments must be genuine references with their specified types; the two PARAM objects must differ. KEY identity may be shared. No additional script PIN variable, mechanism, context parameter, asymmetric operation, HSM integration or EMV card protocol was introduced.

## Architecture assessment and decisions

| Existing component | Finding and implementation decision |
|---|---|
| PinBlockAlgorithm | Already owns format selection, field generation, key diagnostics and output fields. Extracted shared private EncodeResolved/DecodeResolved paths. Translation calls those paths against private parameter stages, never public Unwrap/Wrap. |
| PinBlockFieldCodec / PinBlockFormat4FieldCodec | Existing ISO-9564 constructions and validation reused without a second runtime implementation. |
| AES_ECB / DES3_ECB | Existing no-padding AES and TDEA analysis primitives retained, including two-stage format 4 and non-blocking DES quality diagnostics. |
| ParameterVariableDeclaration | SetParameter mutates its dictionary before rebuilding Value and has no transaction guarantee. Added a narrowly scoped stage-and-publish facility described below. |
| MechanismRegistry / MechanismParameterContractValidator | Existing five PIN mechanisms and their strict Unwrap-source/Wrap-target contracts remain authoritative. No translation mechanism or additional parameter metadata was added. The function enum is appended, preserving previous values and enabling existing sensitive-function handling. |
| ParameterRegistry | Existing PAN, TRANSACTION, FILL and RANDOM suffice; unchanged. |
| OperationInvocation / FunctionCallEvaluator | Invocation already retains original references and kinds. Translation resolves positions 0/1/2/3/4 directly, without the generic single-mechanism reconstruction path. Evaluator maps typed translation failures into one execution-local diagnostic. |
| OperationFactory / CryptoOperations | Added structured dispatch. Legacy string-array dispatch rejects the call rather than reconstructing references from values. |
| SemanticDiagnosticCode | Appended PinBlockTranslationForbidden=14, PinBlockPanMismatch=15 and InvalidPinBlock=16; existing categories cover contracts, mechanisms, parameters and technical key failures. |
| InfoDocs | Extended existing identities, catalog, URI parser, file provider and completion. Existing enum values, URIs and InfoView Back/history architecture remain intact. |
| ANTLR | Generic function rule already recognizes PinBlockTranslate. Only the INFO alternative pintranslations was added; generated lexer artifacts use the repository's ANTLR 4.13.2 version. |

### KEY representation conflict

`Expression.BuildVariable` represents a directly declared `KEY k=0x(...)` as a `StringVariableDeclaration` whose Type is CryptoTypeKey. Generated or structured keys use KeyVariableDeclaration and carry metadata. Requiring only the latter CLR class would reject valid KEY references; manufacturing a replacement object would violate the identity requirement.

The shared PIN resolver now retains the original VariableDeclaration with KEY type. Metadata is inspected on the original KeyVariableDeclaration when present, while raw KEY declarations retain their original identity and have no invented metadata. Existing Wrap/Unwrap signatures and value semantics remain unchanged. The full existing regression suite passes.

### Atomic parameter publication

1. Reject incompatible translation result declarations before function execution. Validate argument references, mechanisms and the central transition rule.
2. Validate both parameter contracts, full PANs and both technical key lengths before decryption; inspect metadata on original KEY objects.
3. Copy parameter dictionaries and explicit-name provenance into private stages, without serializing and reconstructing public identities.
4. Decode into the source stage; construct/encrypt into the target stage. Existing source output fields do not affect decoding. Target defaults are independently generated through the existing CSPRNG.
5. Both dictionaries, serialized Values and the result VAR are fully prepared before publication.
6. Publish each prepared dictionary and base Value using only non-virtual, non-allocating reference assignments. There are no callbacks, validation, SetParameter calls or other fallible work between the two publications.

This provides exception atomicity for the existing synchronous interpreter. It is not a general transaction manager or a new concurrency guarantee for external unsynchronized readers. `GetParameters()` called after publication returns the new backing dictionary; an externally retained old dictionary remains a pre-publication snapshot. The public PARAM objects and their identifiers remain the same. Failure tests cover both dictionary and serialized Value state, including failure after source decoding because the target fill length does not match the recovered PIN length.

### Rules and sensitive data

PinBlockTranslationRules is the immutable 25-entry source used by runtime and documentation contract tests. Full PAN comparisons preserve leading zeros and include the check digit; formats 1/2 reject unused PANs. Issuance and PAN-tokenization exceptions are deliberately unsupported. Target format 2 is ICC-only, including 2 to 2; external ICC use cannot be verified by this runtime.

Translation clears copied key buffers in finally blocks. Shared PIN processing clears mutable clear-block and AES intermediate buffers on success and failure. Managed PIN strings and provider-internal memory cannot be guaranteed erased. Structural PIN-block validation is not authentication. These development and analysis features do not imply PCI certification.

## Documentation and verification

There are exactly 108 InfoDocs: the previous 82 plus one overview and 25 detail pages. Every matrix cell links to its canonical detail URI. The 18 permitted/ICC-only pages contain executable deterministic examples. All examples are executed by tests and match five independent Python cryptography/OpenSSL calculations; see `evidence/G02-vectors.py`.

Initial implementation validation on 2026-10-18 (superseded by the final correction runs below):

- CryptoScriptUnitTest: **6,841 passed**, including 152 G02 translation tests, all existing PIN tests and G01 regressions.
- CryptoWorkBenchGuiTest: **184 passed**, including completion, all 25 detail navigations and Back.
- All allowed transitions tested across every supported source/target key-size combination with different keys and PIN lengths 4, 5 and 12, including leading zeros.
- All nine PAN-bound format pairs reject full-PAN mismatches before malformed ciphertext processing; PAN boundary lengths and unused PAN rejection are covered.
- Reference/arity failures, same KEY reuse, distinct equal-valued PARAMs, stale source outputs, secure target defaults, late failure atomicity, metadata warning identity and redacted single diagnostics are covered.
- Catalog/URI round trips, exact filenames, matrix statuses, internal links and all executable examples are covered alongside existing InfoDocs regression tests.

Commands:

```text
dotnet build CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore -m:1 -nr:false
dotnet build CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore -m:1 -nr:false
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --no-restore
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore
git diff --check
```

Some combined build/test invocations encountered a testhost connection timeout in this environment; separate builds and no-build test runs completed successfully. The GUI build reports nine existing nullable warnings in unrelated UI files. No Linux/macOS execution was performed; filename casing and uniqueness are checked by contracts.

Reference: PCI PIN Security Requirements and Testing Procedures v3.1, Requirement 3-3, and [Implementing ISO Format 4 PIN Blocks, September 2021, Figure 3.5, page 10](https://www.pcisecuritystandards.org/documents/Implementing_ISO_Format_4_PIN_Blocks_Information_Supplement.pdf).

## Independent review corrections: M1, M2, L1, L2

### M1 — Result declaration preflight

Cause: the ordinary declaration evaluator checked the returned type only after translation had published its PARAM stages. A `KEY result=PinBlockTranslate(...)` therefore failed too late.

Correction: `CryptoScript/Model/VariableDeclarationEvaluator.cs` rejects any non-VAR declaration initialized by PinBlockTranslate before evaluating the function. It uses the existing Declaration error shape and leaves all other function evaluation paths unchanged. No generic return-type registry was introduced; the stage-and-publish implementation remains intact.

Tests in `CryptoScriptUnitTest/PinBlockTranslationReviewRegressionTests.cs` use a valid 3-to-4 vector, stale source FILL and absent target RANDOM. They assert unchanged PARAM dictionaries/Values and original KEY identities/state, no result, exactly one declaration error, and zero operation dispatches (therefore no decrypt/encrypt/RNG). A positive VAR case verifies publication of recovered FILL and generated RANDOM. Wrap/Unwrap regressions and the complete suite pass.

Residual limitation: this is intentionally a preflight for the fixed PinBlockTranslate return contract, not a generalized transactional declaration facility.

### M2 — Public contract context

Cause: translation reused source Unwrap and target Wrap validation directly, so contract exceptions exposed the internal function name and omitted the parameter side.

Correction: `CryptoScript/CryptoAlgorithm/PINBLOCK/PinBlockTranslation.cs` validates both existing contracts before key processing and PAN decoding, contextualizing failures with source/target. `CryptoScript/Model/FunctionContractException.cs` retains the original exception as InnerException and preserves Error, Mechanism and ParameterName while assigning PinBlockTranslate as the public Function. `CryptoScript/Model/FunctionCallEvaluator.cs` supplies the mechanism to the existing execution-location merger. No contract definitions changed. Unused PAN rejection keeps its previous InvalidParameter behavior.

Regression tests cover source/target extra IV and missing PAN, each as a direct call and a nested Print argument. All eight cases assert one error, FunctionContractViolation and the original contract category, correct function/statement/operation/parent/ mechanism association, safe side-specific messages, and unchanged original state. A separate test verifies exception-cause preservation; two tests verify the original Wrap/Unwrap names and messages.

Residual limitation: source validation takes precedence if both parameter objects violate their contracts. Messages report field names and mechanism identifiers, never field values.

### L1 — Deterministic failure boundaries

Cause: the original failure cases exercised validation and a target field-length failure, but not target cipher failure or the final boundary before publication.

Correction: `CryptoScript/Operations/CryptoOperations.cs` passes its existing instance-local algorithm factory into translation. `PinBlockTranslation.cs` uses it only after validating the supported mechanism. Internal instance-local callbacks mark after-source-decode and before-publish; an internal cipher delegate in `CryptoScript/CryptoAlgorithm/PINBLOCK/PinBlockAlgorithm.cs` permits deterministic failure at the target encryption call. Production defaults remain the existing AES/TDEA primitives. No global state, script option, rollback logic or callback between the two publications was added.

Three injected-failure tests verify that the intended boundary is reached exactly once, that both original PARAMs and KEY objects retain their identity and complete state, that no result or partial output fields appear, and that exactly one redacted PinBlockTranslate error is recorded. M1 supplies the fourth requested boundary. The positive test independently demonstrates that both prepared output fields publish on success.

Residual limitation: these tests prove synchronous exception atomicity at reachable boundaries, not atomic observation by concurrent unsynchronized readers or recovery from process termination. Internal callbacks are available to the friend test assembly and are unset in production.

### L2 — Rendered Markdown navigation

Cause: the existing GUI test invoked the view-model command directly and bypassed Markdown hyperlink wiring.

Correction: `CryptoWorkBenchGuiTest/PinTranslationInfoTests.cs` now contains 25 parameterized headless tests using the existing ScriptBookTestApp infrastructure. Each creates an actual InfoView, executes `Info(pintranslations)` through the parser/runner and InfoEvent, finds the rendered CHyperlink for the matrix cell, invokes the callback installed by the Markdown engine, checks the expected detail document, and executes the rendered Back button's bound command. It then verifies the exact original matrix and restored rendered link. The static InfoEvent subscription is removed in finally. No GUI production code changed.

Result: all 25 rendered-link cases pass. Residual limitation: these headless tests exercise the actual rendered link handler and Back binding, not physical pointer hit testing.

## M1/M2/L1/L2 correction verification and preserved evidence

Verified on 2026-10-18 with .NET SDK 10.0.401, Debug, Windows:

| Verification | Result | Evidence under `evidence/G02-review-fixes/` |
|---|---|---|
| Complete Core/integration suite | 6,857 passed, 0 failed/skipped; +16 correction tests | `core-full.trx` |
| G02 and InfoDocument focused selection | 351 passed, including all 168 translation tests | `g02-and-docs.trx` |
| Complete GUI suite | 209 passed, 0 failed/skipped; +25 rendered-link tests | `gui-full.trx` |
| Root CryptoWorkBench.sln full Rebuild | Success, 0 warnings/errors | `build-core.txt` |
| CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln full Rebuild | Success, 9 warnings, 0 errors | `build-gui.txt` |
| Both baseline solutions full Rebuild | Success; compiler warning sets match exactly | `baseline-core.txt`, `baseline-gui.txt`, `verification.txt` |
| Documentation | All 108 delivered Markdown files retained; 105 catalog routes and 2 legacy topics load through the production provider | `documents.txt` |
| Independent vectors and diff whitespace | All five vectors unchanged; git diff --check exit 0 | `verification.txt` |

Documentation precision: the existing total of 108 includes `MechanismRoadmap.md`, a standalone roadmap with no runtime Info topic in either the baseline or G02. Thus 107 files are runtime-addressable and the roadmap remains a readable delivered file. No new navigation contract was invented to change that pre-existing distinction. All 25 detail pages, their overview, 18 executable examples and matrix classification 13/5/7 remain unchanged.

Baseline builds used an isolated `git archive` of `5285f078af587b62f3c8a3fcd2495e973ef18d33` in ignored `artifacts/g02-review-baseline-5285f078/`; the active checkout was not reset. The baseline restore emitted NU1900 because the NuGet vulnerability service was unavailable (6 total root-build warnings; 17 total GUI-build warnings, including 9 compiler warnings). Those network/restore warnings are distinct from compiler warnings. The current builds used existing restore assets. Normalized compiler diagnostics match by file, line, code and message: 0 Core, 9 GUI, with no new compiler warnings.

Reproduction commands (logs retain the full build output):

```powershell
dotnet build CryptoWorkBench.sln --no-restore -t:Rebuild -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal '-flp:logfile=docs/architecture/evidence/G02-review-fixes/build-core.txt;verbosity=normal'
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore -t:Rebuild -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal '-flp:logfile=docs/architecture/evidence/G02-review-fixes/build-gui.txt;verbosity=normal'
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --no-restore --logger 'trx;LogFileName=core-full.trx' --results-directory docs/architecture/evidence/G02-review-fixes
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore --logger 'trx;LogFileName=gui-full.trx' --results-directory docs/architecture/evidence/G02-review-fixes
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --no-restore --filter 'FullyQualifiedName~PinBlockTranslation|FullyQualifiedName~InfoDocument' --logger 'trx;LogFileName=g02-and-docs.trx' --results-directory docs/architecture/evidence/G02-review-fixes
git diff --check
```

`g02-focused.trx` preserves the earlier 163-case passing run before the five additional nested-context/cause tests; the final full and focused logs supersede it. Build/test evidence uses non-ignored filenames for inclusion in the later commit. The complete G02 inventory, including all new documentation, test vectors and review evidence, is `evidence/G02-review-fixes/commit-inventory.txt`. No commit, push, merge, G03/G04 work or access to `.codex/config.toml` was performed.

This verification preceded the final independent review, which identified L3 below.

## L3 — TR31H declaration diagnostic

Cause: the M1 check ran after `CryptoType.Parse`. Although the grammar accepts TR31H, that existing resolver rejects it with a generic exception, bypassing the semantic declaration diagnostic.

Correction: the single PinBlockTranslate return-type check in `CryptoScript/Model/VariableDeclarationEvaluator.cs` now runs before type resolution and compares the declared type name with VAR. Every other type produces one existing SemanticErrorException with explicit InvalidDeclaration code, Error severity, the actual type as Identifier, and a message naming both expected VAR and actual type. Statement context is supplied by the existing execution infrastructure; no operation has started and no operation ID is manufactured. The old check was moved, not duplicated. CryptoType.Parse, grammar, other function paths, cryptography and parameter publication are unchanged.

Tests: `CryptoScriptUnitTest/PinBlockTranslationReviewRegressionTests.cs` replaces the single KEY case with five parameterized VAR/KEY/PARAM/PATH/TR31H cases, a net increase of four tests. VAR succeeds; all four invalid cases assert exactly one semantic error with correct code/severity/message and exception identity, zero operation dispatches, unchanged source/target PARAM dictionaries and serialized state, unchanged original KEY objects, no result, statement index 1 and absent operation/parent IDs. The existing positive publication test remains. Existing VariableDeclaration evaluator/AST tests, including `Tr31TypeResolutionStillFailsBeforeFunctionEvaluation`, confirm that non-translation TR31H behavior remains unchanged; no general TR31H support was added.

The only implementation/test source files changed for L3 are the evaluator and this regression fixture; this report, the inventory and new evidence files complete the correction scope. Earlier independent results for M2, L1, L2, the matrix, vectors and 108 documentation files remain applicable.

### L3 verification, 2026-10-18

Evidence is preserved under `evidence/G02-review-fixes/`; the commands above were repeated using the `l3-` log names below. The focused filter was `FullyQualifiedName~PinBlockTranslation|FullyQualifiedName~VariableDeclaration`.

| Verification | Result | Evidence |
|---|---|---|
| M1/L3, all G02 translation tests and declaration regressions | 238 passed, including 172 G02 translation cases | `l3-focused.trx` |
| Complete Core/integration suite | 6,861 passed, 0 failed/skipped | `l3-core-full.trx` |
| Complete GUI suite, initial run | 208 passed, 1 failed, 0 skipped | `l3-gui-full.trx` |
| Complete GUI suite, separate retry | 209 passed, 0 failed/skipped | `l3-gui-retry.trx` |
| Root solution full Rebuild | Success, 0 warnings/errors | `l3-build-core.txt` |
| Desktop solution full Rebuild | Success, 9 known compiler warnings, 0 errors | `l3-build-gui.txt` |
| Warning comparison and diff check | Compiler warnings identical to baseline; git diff --check exit 0 | `l3-verification.txt` |

The initial GUI failure was a NullReferenceException in `Avalonia.Headless.HeadlessUnitTestSession.Dispose` while cleaning up the (1,0) rendered-link test. It occurred during concurrent execution of the two separate test-suite processes. The complete GUI suite passed on the separate retry without source changes. This does not prove concurrency caused the failure or establish a fix for the intermittent headless cleanup behavior; both logs are retained for review. No GUI code was changed for L3.

Remaining findings: L3 is corrected and covered by the five-type regression; no known outstanding G02 functional finding remains. The observed intermittent test-infrastructure cleanup failure remains a verification caveat. No commit, push, merge or G03/G04 work was performed, and `.codex/config.toml` was not accessed.

**READY FOR FINAL VERIFICATION**
