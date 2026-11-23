# 4. Language Architecture

[Architecture documentation](../README.md)

| Metadata | Value |
| --- | --- |
| Status | AR-01 draft; independent technical and linguistic review pending |
| Implementation baseline | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| Date | 2026-11-06 |
| Verification | Static source inspection; no new application tests |

## 4.1 Scope and Language Architecture Overview

**[F]** CryptoScript owns lexer/parser construction, parse-tree mapping, core syntax records, evaluation and operation dispatch. The host's WorkbenchExecutionPipeline builds the parser, calls program(), gates syntax failures and awaits CryptoScriptRunner.ExecuteAsync. Core has no Avalonia/GUI project dependency. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R07](../evidence/ar-01-baseline-reconciliation.md#r07), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

**[I]** This is an interpreter with an explicit parse-tree-to-AST boundary. Mapping and evaluation remain interleaved per statement; there is no complete-program semantic validation pass before execution. Execute(IEnumerable<StatementNode?>) also accepts AST input without parsing. CryptoScriptProgram contains evaluated results and diagnostics, not the program syntax tree. The [component view](../diagrams/04-language-components-and-dependencies.mmd) separates logical dependencies from generation relationships.

This chapter follows source through operation invocation. Detailed runtime, cryptographic, diagnostic, GUI and MCP policies belong to their later chapters/packages. Assignment-specific validation is included because it determines the new language statement's behavior.

## 4.2 Grammar Organization and Generated Artifacts

**[F]** CryptoScript.g4 is a combined ANTLR grammar with parser rules and an INT lexer rule. It imports the lexer grammar CryptoLexer.g4, which imports TR31Lexer.g4. This is not a separate parser grammar using tokenVocab. Development helpers invoke antlr4 with C# and visitor output; committed C# lexer/parser, listener/visitor interfaces and bases, token and interpreter metadata are the generated artifacts. Runtime instantiates generated C# and ANTLR simulators; it does not read g4 files or run those helpers. [R06](../evidence/ar-01-baseline-reconciliation.md#r06)

The handwritten partial lexer intercepts DES3-RETAIL and #MACLEN before delegating. Active AST mapping uses explicit AntlrTo* methods rather than the generated visitor/listener scaffold. Generated metadata has additional consumers: AntlrLanguageMetadata reflects token constants/vocabulary; MechanismList, ParameterTypeList and CryptoType use it. The AST has no ANTLR context fields, but the language subsystem is not independent of generated metadata. [R06](../evidence/ar-01-baseline-reconciliation.md#r06), [R08](../evidence/ar-01-baseline-reconciliation.md#r08), [R09](../evidence/ar-01-baseline-reconciliation.md#r09), [R12](../evidence/ar-01-baseline-reconciliation.md#r12)

Generated headers identify ANTLR 4.13.2; the runtime package is 4.13.1. Helpers do not pin the external generator, and application builds do not establish reproducible grammar regeneration. **[Q]** The authoritative generator environment remains unresolved; no incompatibility is asserted.

## 4.3 Lexical Model and Syntax

**[F]** Declaration keywords are KEY, VAR, PARAM, PATH, TR31H, VAL and SECRET. Nominal identifiers use an ASCII letter followed by ASCII letters/digits. FN-shaped names start with uppercase then lowercase and continue with letters; FN precedes ID, so matching the nominal identifier pattern does not guarantee an ID token. Imported TR-31 tokens also overlap names. Fixed mechanism, padding, parameter and information tokens retain their lexical casing. [R06](../evidence/ar-01-baseline-reconciliation.md#r06)

INT contains unsigned decimal digits without a grammar size bound. HEX/Base64/string tokens have lexical shapes, not complete runtime validity guarantees; HEX does not itself require an even digit count. Strings allow the grammar's restricted escapes. Absolute drive, UNC and slash-prefixed path tokens consume through the line ending, potentially swallowing commas or parentheses. Relative paths and tilde expansion are not introduced by these rules. Runtime path classification/validation is a separate step. Whitespace is skipped and no general comment rule or statement semicolon is defined. TR-31 fields use semicolons inside braces. [R06](../evidence/ar-01-baseline-reconciliation.md#r06), [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R12](../evidence/ar-01-baseline-reconciliation.md#r12)

program accepts statement* EOF; statement selects declaration, functionCall or assignment. expression remains literal-only: there are no arithmetic or bare-variable-reference expressions. Function calls are separate initializer/argument alternatives. Thus name = expression is shorthand for the supported initializer alternatives, not a general expression language. Assignment requires a nonempty initializer; the legacy declaration parameter-list alternative still allows zero items syntactically.

CreateHeader, Info, Load/SecureLoad/Delete and secure options have contextual argument rules. FN-shaped snapshot selectors and raw conflict options must not be treated as ordinary variable lookup in every call. Variable dictionary lookup remains case-sensitive; parameter storage has its own case-insensitive normalization. Factory/type normalization does not broaden source FN syntax. [R06](../evidence/ar-01-baseline-reconciliation.md#r06), [R11](../evidence/ar-01-baseline-reconciliation.md#r11), [R12](../evidence/ar-01-baseline-reconciliation.md#r12), [R14](../evidence/ar-01-baseline-reconciliation.md#r14)

## 4.4 Parser Construction and Syntax Diagnostics

**[F]** StringBuild verifies interpreter access, constructs AntlrInputStream, CryptoScriptLexer, CommonTokenStream and CryptoScriptParser, replaces default listeners with custom lexer/parser listeners and clears the shared syntax-error list. It returns before invoking program() and does not explicitly fill the token stream. FileBuild also verifies access and installs custom listeners, configured for console diagnostics; its reset behavior is not identical to StringBuild. [R07](../evidence/ar-01-baseline-reconciliation.md#r07)

Listeners collect diagnostics rather than intentionally stopping parsing. Generated parser recovery can produce incomplete contexts. The host pipeline explicitly clears the shared list, both flags and the parser message buffer, parses, copies diagnostics and rejects either lexer or parser errors before execution. This supersedes the former parser-only GUI gate. Parser construction alone still does not implement the complete host reset/gate contract, and the runner's AST-input path is not a generic syntax gate. [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R07](../evidence/ar-01-baseline-reconciliation.md#r07), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

Semantic diagnostics are separate: evaluators add SemanticError objects and may throw SemanticErrorException; execution-owned contexts capture them with execution/statement/operation locations. Diagnostic severity alone is not proof that execution stopped. A thrown terminating exception and a continuing diagnostic are distinct. [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

## 4.5 Abstract Syntax Tree and Mapping

**[F]** StatementNode has declaration, function-call and assignment variants. VariableDeclarationStatementNode holds Declaration and RawText; FunctionCallStatementNode holds Call. AssignmentStatementNode holds Identifier, optional Initializer, Parameters, optional Tr31Header and RawText, but no declared type: execution resolves the existing target. VariableDeclarationNode additionally retains textual TypeName. [R08](../evidence/ar-01-baseline-reconciliation.md#r08), [R09](../evidence/ar-01-baseline-reconciliation.md#r09)

ExpressionNode has literal and function-call initializer records. FunctionCallExpressionNode stores Name, CallText and ordered Arguments. Argument variants represent mechanisms, variables, literals, parameters, information selectors, nested calls, empty recovery values, secure options and header attributes. ParameterListInitializerNode retains ordered items, including duplicates. ParameterInitializerNode retains nullable type/value text and RawText. Tr31HeaderInitializerNode now retains structured header fields as well as RawText. Parameter lists and headers are not ExpressionNode subclasses.

AntlrToStatement selects an assignment before call/declaration and can return null for a recovery context. Explicit mappers construct records recursively without variable resolution, runtime type validation or execution. GetText-derived RawText/CallText omit skipped whitespace outside tokens and are not exact original source slices. The syntax records still do not provide general source spans, token indices, line or column fields. Missing recovery children remain a concern; mapping is not comprehensive malformed-tree validation.

The [AST view](../diagrams/04-abstract-syntax-tree-structure.mmd) distinguishes inheritance from composition and does not invent runtime lifetimes. The original structured-header gap is superseded; syntax/runtime separation remains valid.

## 4.6 Declarations, Initializers and Assignments

**[F]** Declaration evaluation resolves TypeName through CryptoType.Parse. KEY literals now construct KeyVariableDeclaration and reject invalid/nonempty-byte violations; PATH uses a dedicated path preparer. VAL requires a Boolean function result; SECRET requires a function result. TR31H resolves to CryptoTypeTR31Header, and brace fields are evaluated through CreateHeader semantics. The former missing type case/header-evaluation branch is no longer current. [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R12](../evidence/ar-01-baseline-reconciliation.md#r12), [R14](../evidence/ar-01-baseline-reconciliation.md#r14)

Function declarations check returned type and register the result. Nonempty direct parameter lists require PARAM and use ParameterInitializerEvaluator. Parameter ordering, overwrite/explicit-name behavior and legacy declaration compatibility must be distinguished from stricter assignment preparation. Syntactic acceptance, including empty declaration alternatives, is not successful initialization.

**[F]** Redeclaring an existing variable identifier replaces the existing dictionary entry through VariableDictionary.Add. VariableDeclarationEvaluator registers parameter/function results directly and delegates literal registration to Expression.BuildVariable. Redeclaration validates against its own TypeName; it does not retain the previous entry's declared type. Subsequent assignment is a distinct operation: AssignmentEvaluator requires an existing target, retains its declared type and publishes a prepared replacement through VariableDictionary.Update. The stricter assignment validation and target-preservation guarantees must not be attributed to redeclaration. [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R12](../evidence/ar-01-baseline-reconciliation.md#r12)

AssignmentEvaluator first finds the existing target; an unknown target fails before evaluating the RHS and is not implicitly declared. Exactly one initializer category must be present. Literal assignments are supported for VAR, KEY and PATH; assignment INT overflow is rejected before the legacy expression fallback. PARAM lists require a consistent structured mechanism and validated serialization. TR31H fields become a CreateHeader call. The declared CryptoScript type is retained even when the concrete runtime representation changes.

For function initializers, ValidateCallTree checks the whole nested tree before running siblings. Print, Info, Save, Load, SecureSave, SecureLoad and Delete are rejected as void initializers. An explicit audited function set must opt in; unknown functions fail. PARAM targets restrict the tree to Parameters; PinBlockTranslate's top-level assignment result requires VAR. File-operation contracts are checked before invocation. Returned values need a compatible type, valid concrete representation and detached identity; live dictionary aliases cannot be renamed into the target.

Preparation completes before setting identifier/type/text and calling VariableDictionary.Update once. Failure preserves the target entry; it does not roll back unrelated PARAM effects of an operation, files or earlier statements. Assignment diagnostics preserve operation context while redacting generic failures. Cancellation propagates. This is target publication atomicity under interpreter ownership, not a script transaction. [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R12](../evidence/ar-01-baseline-reconciliation.md#r12), [R13](../evidence/ar-01-baseline-reconciliation.md#r13), [R16](../evidence/ar-01-baseline-reconciliation.md#r16)


```text
VAR target = "old"
target = "new"
Print(target)
```

The example demonstrates existing-target replacement. It does not introduce bare-variable-copy or arithmetic syntax. Existing tests cover sync/async behavior, unknown targets before RHS, rejected initializers, metadata preservation, integer boundaries, parameter consistency/serialization and foreign-effect limits. These assertions were inspected, not executed for this documentation.

## 4.7 Function Calls, Arguments and Variable Identity

**[F]** FN syntax alone does not establish an implemented function. Recognition, special operation contracts and argument evaluation precede invocation. Current dispatch includes Mac, Hash, Derive, Encrypt, Decrypt, Print, Info, GenerateKey, Parameters, Wrap, Unwrap, BlockHeader, CreateHeader, Check, Compare, Share, Secret, Restore, PinBlockTranslate, Save, Load, SecureSave, SecureLoad, Delete, MacPack and MacVerify; the old Sign case is absent. Secure persistence has explicit async dispatch requirements. [R11](../evidence/ar-01-baseline-reconciliation.md#r11), [R14](../evidence/ar-01-baseline-reconciliation.md#r14)

Ordinary nested calls complete depth-first, left-to-right before the outer operation. Variable arguments resolve to the original runtime object and can retain SourceVariable even if another variable has the same value. Literal arguments carry value/kind without a source object. Named parameter handling normally substitutes values; context-specific header/secure/snapshot arguments retain their dedicated representations. Most nested results are reclassified from returned value text and lose original identity; BlockHeaderVariableDeclaration is now a deliberate exception returned as ArgumentVariable.

OperationInvocation copies its ordered argument collection and exposes it read-only, but does not clone referenced mutable variables. Values projects strings and loses kind/identity. AlgorithmCallArguments can preserve source references; reconstruction from serialized PARAM text produces a temporary and records reconstructed provenance, not original identity. FromValues cannot recover lost identity. Function/algorithm-specific adapters determine where projection occurs; the old universal value-only classification of Hash/GenerateKey and nested results must not be reused without inspecting current dispatch. [R11](../evidence/ar-01-baseline-reconciliation.md#r11)

**[I]** Syntax records preserve call structure while operations receive resolved arguments. There is no general lazy-expression subtree handed to cryptographic operations. Detailed argument resolver and algorithm policy remains a runtime/cryptography concern.

## 4.8 Processing Sequence and Execution Boundary

The [processing view](../diagrams/04-source-to-operation-sequence.mmd) follows the implemented host path. **[F]** [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R07](../evidence/ar-01-baseline-reconciliation.md#r07), [R09](../evidence/ar-01-baseline-reconciliation.md#r09), [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R11](../evidence/ar-01-baseline-reconciliation.md#r11), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

1. Host admission encloses parsing, runner work and state capture. Syntax state is reset and StringBuild constructs the frontend; the caller invokes program().
2. Either lexer or parser diagnostics reject the parse unit before runner execution. This rejection still enters FinalizePresentation, which attempts variable state capture. The outcome remains SyntaxError; CaptureFailed separately records capture failure, and the displayed state is marked stale. No runner is started on this path.
3. ExecuteAsync enters interpreter ownership. MapStatements yields one mapped statement, excluding EOF and retaining null recovery entries.
4. StatementEvaluator selects declaration, assignment, call or null. Evaluation occurs before the next statement is mapped.
5. Declarations prepare/register their initializer. Assignments resolve the existing target, validate and prepare a replacement before publication. Calls resolve arguments and dispatch through FunctionCall/OperationFactory.
6. SecureSave/SecureLoad standalone statements have dedicated asynchronous handling and host password requests. Cancellation is checked before statement evaluation; no check retroactively cancels a final completed operation.
7. Results and execution diagnostics are collected. Terminating exceptions leave the loop; prior successful effects remain under their operation contracts. The runner retains the last execution's diagnostics after abort.
8. The host waits for outstanding password callbacks to unwind, copies diagnostics/output and captures actual variable state. Capture success is distinct from script success; failed capture preserves stale presentation rather than fabricating empty runtime state.

The synchronous runner also takes an interpreter lease. Semantic checks and effects remain interleaved. Assignment's whole-call-tree preflight is local to that initializer, not whole-program validation. Error severity alone does not determine termination; KeyCheckValueSearchException has a continuing path. No automatic whole-script rollback is introduced.

## 4.9 Architectural Constraints and Verification Limits

**[F]** ScriptBook saving in WorkbenchSaveCommit.WriteSnapshot encodes each nonblank line with Encoding.ASCII.GetBytes and writes a newline. Non-ASCII content, including strings or path text, may be altered during serialization. CryptoScriptEditViewModel loads lines with File.ReadAllLinesAsync(filePath, pipeline.ExecutionToken), without an explicit encoding argument, and passes them to ExecuteBookAdmittedAsync. Loading reads the saved content; it does not restore characters lost during saving. This is a ScriptBook text-persistence restriction, not a claim about variable snapshot formats. [R05](../evidence/ar-01-baseline-reconciliation.md#r05)

**[F/I]** Shared variables, static output and syntax state still constrain reuse; interpreter coordination prevents competing admitted work but is not per-session storage isolation. AST records separate syntax from runtime objects yet do not carry precise source spans. Token overlap, greedy path lexing, identifier casing and value-based nested-result conversion remain relevant. Assignment explicitly rejects integer overflow; this must not be generalized to legacy declaration fallback behavior. [R06](../evidence/ar-01-baseline-reconciliation.md#r06), [R08](../evidence/ar-01-baseline-reconciliation.md#r08), [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R11](../evidence/ar-01-baseline-reconciliation.md#r11), [R12](../evidence/ar-01-baseline-reconciliation.md#r12), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

Extension touchpoints remain explicit: syntax changes may require grammar regeneration, AST, mapper and evaluator changes; functions using existing call syntax need dispatch support; mechanisms/parameters depend on generated vocabulary and runtime handling. Assignment functions additionally require an audited opt-in. File/password behavior adds host and resource boundaries without changing the fact that these logical layers share a core assembly.

**[Q]** Which preserved edge cases are promised public language contracts, which generator environment is authoritative, and what isolation/support policy is promised require owner decisions. No concurrency, platform, cryptographic conformance or application tests were newly executed for AR-01. Test references are inspected assertions. The earlier L01–L22 register and explanations remain in the exact historical Chapter 4; current R01–R16 evidence is independently tied to fbbadae2. Technical and linguistic review of this reconciled chapter remains pending.

[Architecture documentation](../README.md)
