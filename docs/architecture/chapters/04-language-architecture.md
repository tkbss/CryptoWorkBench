# 4. Language Architecture

[Architecture documentation](../README.md) | [Previous: Project Structure and Dependencies](03-project-structure-and-dependencies.md) | [Source evidence](#source-evidence)

| Metadata | Value |
| --- | --- |
| Status | Draft - pending independent review |
| Implementation baseline | `29b6b36e860ff278b845ac439dd123661760d098` |
| Documentation starting point | `29b6b36e860ff278b845ac439dd123661760d098`, branch `docs/architecture-recovery` |
| Analysis and documentation date | 2026-10-16 |
| Verification | Phase-1 static analysis, relevant local source reinspection and inspection of existing test definitions; no application build, test execution or platform run |

This chapter describes the local architecture worktree at the recorded baseline. **[F]** identifies directly verified source facts, **[I]** architectural interpretations and **[Q]** unresolved matters. L01-L22 resolve through the [source evidence register](#source-evidence). Test citations identify inspected assertions, not passing test results. The baseline identifies this checkout's implementation; it does not replace the historical baselines of Chapters 1-3.

## 4.1 Scope and Language Architecture Overview

**[F: L04-L08, L17, L20]** The reusable CryptoScript library owns lexer/parser construction, parse-tree mapping, its own syntax records, statement evaluation and operation dispatch. The active Avalonia editor calls `ParserBuilder.StringBuild`, invokes `CryptoScriptParser.program()` and passes the resulting context to `CryptoScriptRunner.Execute`. The GUI references Core; Core declares no GUI project reference or Avalonia package dependency.

**[I]** CryptoScript is an interpreter with an explicit parse-tree-to-AST boundary and statement-oriented evaluation. The AST separates syntax from runtime objects, but the runner maps and evaluates individual statements in sequence. It does not first construct and semantically validate a complete program AST. This interpretation follows from L05-L08 and L17, rather than from the existence of generated visitor infrastructure.

The chapter follows source text through syntax analysis and AST construction to the invocation of operation delegates. Runtime execution, detailed evaluator behavior, algorithm implementations, mechanisms and cryptographic primitives belong to Chapters 5 and 6. Diagnostic internals and GUI composition belong to Chapters 7 and 10. The active scope remains the Avalonia application and reusable Core established in [Section 1.3](01-introduction.md#13-scope-and-boundaries).

The [Language Components and Dependencies diagram](../diagrams/04-language-components-and-dependencies.mmd) shows logical code groups within Core and its external Avalonia caller. Solid arrows denote code dependencies/calls; dotted arrows identify grammar imports or generation. It is not a deployment diagram. Evidence: L01-L08, L12-L13 and L17-L20.

## 4.2 Grammar Organization and Generated Artifacts

**[F: L01-L03]** `CryptoScript.g4` is a combined ANTLR grammar: it contains parser rules and an `INT` lexer rule. It imports `CryptoLexer.g4`, a lexer grammar that imports `TR31Lexer.g4`. There is no separate parser-grammar declaration using `tokenVocab` in these definitions. The import direction is `CryptoScript -> CryptoLexer -> TR31Lexer`.

| Artifact group | Implemented role | Evidence |
| --- | --- | --- |
| Grammar definitions | Define lexical categories and parser rules, including imported TR-31 fields | L01-L02 |
| Generated C# | `CryptoScriptLexer`, `CryptoScriptParser`, visitor interface/base and listener interface/base | L03 |
| Token/interpreter metadata | `CryptoScript.tokens`, `CryptoScriptLexer.tokens` and corresponding `.interp` files record generated token/interpreter metadata | L03 |
| Handwritten extension | Partial `CryptoScriptLexer.NextToken` intercepts `DES3-RETAIL` and `#MACLEN`, then otherwise delegates to the generated lexer | L03 |
| Generation helpers | Batch and shell scripts invoke external `antlr4` with C# output and `-visitor`; tree helpers invoke the `program` entry for inspection | L03 |
| Runtime metadata bridge | `AntlrLanguageMetadata` reflects public generated lexer constants by prefix and reads vocabulary names | L19 |

**[F: L03, L20]** The helper command generates implementation sources during development. The inspected project and workflow declarations do not invoke grammar regeneration. Runtime parsing instantiates the committed generated C# classes and ANTLR simulators; it does not execute the helper scripts or read `.g4` files. Grammar imports, generation inputs/outputs and runtime calls are therefore different relationships.

**[F: L19]** Generated dependencies extend beyond parsing. `MechanismList` and `ParameterTypeList` initialize mutable lists through `AntlrLanguageMetadata`. `CryptoType` obtains type display names through generated token constants and vocabulary. These consumers depend on the generated implementation, even though the AST records contain no ANTLR types.

**[F: L03, L20]** Generated C# headers identify ANTLR **4.13.2**, while the project declares `Antlr4.Runtime.Standard` **4.13.1**. The scripts do not pin the external generator version. This is an observed version/reproducibility difference; incompatibility is not established.

## 4.3 Lexical Model and Syntax

**[F: L01-L02]** The following table describes grammar definitions. Acceptance by these rules does not establish executable support or cryptographic validity.

| Category | Implemented convention |
| --- | --- |
| Declaration keywords | Uppercase `KEY`, `VAR`, `PARAM`, `PATH`, `TR31H` |
| Identifiers | ASCII letter followed by ASCII letters/digits: `[a-zA-Z][a-zA-Z0-9]*` |
| Function-shaped names | `[A-Z][a-z][a-zA-Z]*`; implementation recognition occurs later |
| Decimal integers | `[0-9]+`; no sign, fraction or grammar-level size limit |
| Hexadecimal literals | `0x(...)` containing one or more hexadecimal digits; the rule does not require an even digit count |
| Base64 literals | `b64(...)` using the specified Base64 character set; full encoding validity is not checked by the rule |
| Normal strings | Double quotes, with the restricted `ESC` alternatives for backslash escapes |
| Mechanisms and paddings | Enumerated uppercase/hyphenated values grouped under `MECHANISM` and `PADDING` |
| Named parameters | Enumerated `#` names, such as `#MECH`, `#IV`, `#PAD`, `#USAGE`, `#KEYTYPE`, used as `PARAM_TYPE ':' value` |
| Information selectors | Fixed lowercase words: `functions`, `mechanisms`, `types`, `parameters`, `paddings`, `mechanism`, `keymap` |
| TR-31 composite literal | A quoted string immediately followed by two hexadecimal segments |
| TR-31 header | Braces around one or more fields; each is a field-name token, colon, field-value token or integer, and semicolon |
| Paths | Drive-prefixed, backslash UNC or slash-prefixed text continuing to the line ending |
| Whitespace | Spaces, tabs and newlines are skipped; no comment rule is declared |

**[F: L01]** `program` accepts zero or more declarations/calls followed by EOF. Statements have no general semicolon terminator. `expression` contains literal alternatives; it does not define arithmetic or variable-reference expressions. Function calls are separate grammar alternatives for declarations and arguments. An identifier can be an argument or parameter value, but `VAR copy = existing` is not a declaration-expression alternative.

**[F/I: L01-L03, L21]** Token overlap restricts the nominal identifier pattern. `FN` precedes `ID`, so a function-shaped name is not an ordinary ID token merely because it also matches that character pattern. General identifiers also overlap imported TR-31 names/values. The TR-31 AST tests explicitly supply token types to bypass existing lexer conflicts; they do not demonstrate a working source-text-to-header execution path. The handwritten lexer interception is an additional lexical dependency.

**[F: L01, L12, L14, L18-L19]** Source keywords and fixed lexical values retain their declared casing. Variable lookup uses unnormalized dictionary keys. Factory function lookup and `CryptoType.Parse` use parameterless `ToLower()`, while parameter storage normalizes names through ordinal case-insensitive comparisons. These are distinct policies. Runtime normalization does not broaden the source `FN` rule; syntax tests include rejection of lowercase `generateKey` (L21).

**[F: L01, L15]** Path lexing accepts Windows and Unix-style absolute forms, but consumes punctuation such as commas/parentheses through the rest of the line. Relative paths and tilde expansion are not defined. Runtime path classification uses separate regular expressions: a drive prefix must be followed by a slash or backslash there, unlike the broader lexer rule. Recognition does not prove path existence, access or portability.

## 4.4 Parser Construction and Syntax Diagnostics

**[F: L04]** `ParserBuilder.StringBuild` constructs `AntlrInputStream`, `CryptoScriptLexer`, `CommonTokenStream` and `CryptoScriptParser` in that order. It replaces default lexer/parser listeners with `LexerErrorListener` and `SyntaxErrorListner`, clears `SyntaxErrorList.Instance()` and returns the parser. It neither explicitly fills the token stream nor invokes a parser entry rule.

**[F: L03-L04, L20]** The caller invokes `program()`. The generated method creates `ProgramContext`, consumes statements and EOF, and reports/recovers recognition failures through the parser error handler. The token stream supplies lexer tokens during parsing. Parse-tree creation is distinct from subsequent mapping to Core-owned AST records.

| Diagnostic component | Verified behavior | Evidence |
| --- | --- | --- |
| `LexerErrorListener` | Sets its static flag and appends a `LexerError` to the shared list | L16 |
| `SyntaxErrorListner` | Sets its own static flag, appends a `ParserError` and accumulates formatted text; a colon error can be redirected to the preceding token | L16 |
| `SyntaxErrorList` | Exposes a singleton mutable list containing both error kinds | L16 |
| Avalonia `ExecuteLine` | Checks the parser listener flag, reports its message and returns before runner execution when set | L20 |

**[F: L04, L16, L20]** These listeners record diagnostics rather than deliberately throwing to terminate parsing. The GUI supplies the syntax-error gate; the runner does not add a corresponding check. That GUI gate tests the parser flag, not the lexer flag or combined list. `StringBuild` clears the list but does not reset both static flags or clear the accumulated parser message buffer. The GUI resets the parser flag after a failed parse.

**[F: L04]** `FileBuild` opens a file reader and constructs the same lexer/token-stream/parser chain, but does not install the custom listeners or clear their state. It also returns before entry-rule invocation. Its behavior must not be described as identical to `StringBuild` diagnostic setup.

**[F: L07-L08, L17, L20]** Semantic execution diagnostics follow another path: evaluators add `SemanticError` objects, commonly throw `SemanticErrorException`, and capture diagnostics in an execution-owned context. The GUI catches execution exceptions and reads runner diagnostics. Syntax positions originate in ANTLR callbacks; execution locations identify statements/operations. Detailed classification, redaction and presentation remain Chapter 7 responsibilities.

## 4.5 Abstract Syntax Tree and Mapping

**[F: L05-L06]** The AST consists of records under `CryptoScript/Model/Ast`. The active path uses explicit static `AntlrTo*` mapping methods and constructors. Generated visitor/listener interfaces and base classes exist but are not used by this mapping path. Evaluators switch on or test Core AST node types.

| AST types | Responsibility and important properties |
| --- | --- |
| `StatementNode` | Abstract statement basis |
| `VariableDeclarationStatementNode` | Holds `Declaration` and parse-tree `RawText` |
| `FunctionCallStatementNode` | Holds the shared function-call node in `Call` |
| `VariableDeclarationNode` | Holds `Identifier`, textual `TypeName`, optional `Initializer`, `Parameters` and optional `Tr31Header` |
| `ExpressionNode` | Abstract basis for literal and function-call initializers |
| `LiteralInitializerNode` | Preserves `RawText` without decoding or runtime classification |
| `FunctionCallExpressionNode` | Holds `Name`, `CallText` and ordered `Arguments`; used in initializers, standalone calls and nested calls |
| `FunctionCallArgumentNode` | Abstract basis for mechanism, variable, literal, parameter, information, nested-call and empty variants |
| `ParameterListInitializerNode` | Holds ordered `Items`, including duplicates |
| `ParameterInitializerNode` | Holds nullable `TypeName`, nullable `RawValue` and `RawText` |
| `Tr31HeaderInitializerNode` | Holds complete header `RawText`; fields are not separate AST nodes |

The [Abstract Syntax Tree Structure diagram](../diagrams/04-abstract-syntax-tree-structure.mmd) uses the established flowchart notation as a structural equivalent. Dotted subtype-to-base edges mean inheritance; solid owner-to-member edges name properties and target multiplicities. Grouped argument variants represent distinct classes, not another implementation class. Evidence: L05-L06.

**[F: L06]** `AntlrToStatement.Map` wraps a declaration or call and returns null for an empty recovery context. `AntlrToVariableDeclaration.Map` copies identifier/type text, creates a literal or call initializer, always creates a parameter-list node and optionally preserves header text. `AntlrToFunctionCallExpression.Map` recursively maps arguments in order; its fallback is `EmptyArgumentNode`. `AntlrToParameterInitializer.Map` preserves missing child values as null. Mapping does not resolve variables, validate runtime types or execute calls.

**[F: L01, L05-L06, L21-L22]** `RawText` and `CallText` use parse-tree `GetText()`, so skipped whitespace outside tokens is absent; string-token content remains. These are textual representations, not exact source slices. No AST record stores source spans, token indices, lines or columns. Parameter-list/header nodes do not derive from `ExpressionNode`. Recovery may produce null statements or missing values, and the mapper is not a comprehensive validator for arbitrarily malformed parse trees.

## 4.6 Declarations and Initializers

**[F: L01, L05-L07, L12-L14]** All five declaration keywords map to the same `VariableDeclarationNode`. Syntax retains the type name as a string; evaluation resolves it through `CryptoType.Parse` before processing the initializer.

| Declaration | Syntax/AST representation | Verified execution distinction |
| --- | --- | --- |
| KEY | Common declaration with `TypeName = KEY` | Resolves to `CryptoTypeKey`; literal construction can produce a `StringVariableDeclaration` carrying that type tag |
| VAR | Common declaration with `TypeName = VAR` | Literal construction generally uses a string variable; path expressions select a path object |
| PARAM | Common declaration with `TypeName = PARAM` | A nonempty parameter list builds `ParameterVariableDeclaration`; literal syntax does not itself ensure that concrete class |
| PATH | Common declaration with `TypeName = PATH` | Recognized path expressions build `PathVariableDeclaration`; the declared type is assigned separately |
| TR31H | Common declaration, optionally containing header text | `CryptoType.Parse` has no TR31H case and throws before header evaluation |

**[F: L01, L06-L07, L22]** The grammar permits four initializer forms for every declared type: literal expression, function call, zero or more `declareparam` items, or TR-31 header. The AST stores expression initializers, parameter lists and headers in separate properties. Thus `type name =` can parse through the zero-item parameter alternative and yield no value; syntactic acceptance is not successful initialization.

**[F: L07, L12-L13]** Literal text is evaluated through `Expression.Create`, then `Expression.BuildVariable`. Object construction depends on the expression class, while the declared `CryptoType` is assigned separately. A syntax-level KEY is therefore not proof of a concrete `KeyVariableDeclaration` with typed key metadata. For function initializers, evaluation obtains `ReturnVariable`, checks its type class against the declared type, assigns its identifier and registers that same object.

**[F: L07, L14]** Nonempty direct parameter lists require PARAM and an item selected by the evaluator's `RawText.Contains("MECH")` search. Items are evaluated in order and stored; repeated parameter names overwrite stored values, while explicit-name tracking retains their occurrence information. These checks and mutation occur during evaluation, not AST mapping.

**[F: L05-L07, L12, L21]** Header syntax is preserved as `Tr31HeaderInitializerNode.RawText`, but the declaration evaluator has no header-consumption branch. Although `CryptoTypeTR31Header` exists, `CryptoType.Parse` omits it. Existing tests explicitly expect type-resolution failure with no semantic error added. The separate `BlockHeader` function dispatch is not evidence that TR31H declarations execute. Parsed headers and executable header declarations must remain distinct claims.

## 4.7 Function Calls, Arguments and Variable Identity

**[F: L08, L18]** Any lexical FN can become a call AST node. `FunctionCallEvaluator` first invokes `OperationFactory.CreateOperation` to recognize the name, before evaluating arguments. Implemented lookup cases are Sign, Mac, Hash, Derive, Encrypt, Decrypt, Print, Info, GenerateKey, Parameters, Wrap, Unwrap, BlockHeader and Compare. `FunctionCall.Call` later obtains the invocation-aware delegate through `CreateInvocationOperation`.

| Source/AST argument | Evaluation | Invocation representation |
| --- | --- | --- |
| `MechanismArgumentNode` | Builds and validates `ArgumentMechanism` | Mechanism value and kind |
| `VariableArgumentNode` | Looks up identifier; `ArgumentVariable.Id` holds the original object | Value, Variable kind and exact `SourceVariable` reference |
| `LiteralArgumentNode` | Builds `ArgumentExpression` using `Expression.Create` | Value and HexLiteral/OtherLiteral kind; no source object |
| `ParameterArgumentNode` | `ParameterEvaluator` builds `ArgumentParameter`; referenced variables become values | Serialized `Type:Value`, Parameter kind; no source object |
| `InfoArgumentNode` | Builds `ArgumentInfo` | Selector and Info kind |
| `NestedCallArgumentNode` | Executes inner call; reclassifies return value through `Expression.Create` | Value and NestedFunctionCall kind; returned-object identity is absent |
| `EmptyArgumentNode` | Falls back to base `Argument` | Empty kind and null value |

Evidence: L08-L11. `SourceVariable` is a property referencing runtime `VariableDeclaration`, not another source-language node or a standalone class.

**[F: L06, L08-L10, L21]** Mapping and ordinary evaluation preserve argument ordering. Nested calls complete depth first and left to right before outer dispatch. Direct variable resolution retains object identity even when different variables contain identical values. `OperationInvocation` copies its ordered argument collection and exposes it read-only, but does not clone the referenced mutable variables. Its `Values` projection creates a string array and discards kind and identity.

**[F: L08, L11, L13]** Named parameter references and nested results do not preserve direct-variable identity. `ArgumentParameter.SetValue` substitutes a referenced variable's value; special parameter rules can also retain/validate literal forms. Nested calls retain only the returned value, reclassified as an expression. The original nested structure remains in the AST but is not delivered as an unevaluated subtree to the operation.

**[F: L18]** Mac, Derive, Encrypt, Decrypt, Wrap and Unwrap convert invocation arguments to `AlgorithmCallArguments`, copying value, kind and source reference. GenerateKey, Hash, BlockHeader, Sign, Print, Info and Compare project to values; Parameters also processes projected values. Identity preservation is therefore path-specific.

**[F: L10, L18]** Algorithm selection can retain the original `ParameterVariableDeclaration`; when only serialized PARAM text is available, it can reconstruct an unregistered temporary. `AlgorithmCallArgument.HasReconstructedSourceVariable` distinguishes reconstructed provenance from an original reference. `AlgorithmCallArguments.FromValues` cannot recreate lost source identity. This chapter stops at that boundary; resolution policy and algorithm behavior belong to Chapter 5.

## 4.8 Processing Sequence and Execution Boundary

**[F: L03-L11, L17-L20]** The active GUI path follows this sequence:

1. `ExecuteLine` supplies source text to `ParserBuilder.StringBuild`.
2. The builder constructs the character stream, lexer, token stream and parser, and installs custom listeners.
3. The caller invokes `program()`; tokenization and syntax analysis produce the parse tree, with error recovery where applicable.
4. The caller checks the parser-error flag and can return before runner execution. This is the GUI's gate, not a generic runner precondition.
5. `CryptoScriptRunner.Execute(context)` obtains a lazy `MapStatements` enumeration. It maps one statement through `AntlrToStatement`, excluding the final EOF child and retaining null recovery entries.
6. `StatementEvaluator` evaluates that statement before the runner requests the next mapped statement. Declarations resolve types and initializers; calls first check function recognition.
7. Function arguments are resolved in order; nested calls recurse and complete before the outer invocation. Variable, mechanism and applicable parameter/contract checks occur on this path.
8. `FunctionCall.Call` constructs `OperationInvocation` from resolved runtime arguments.
9. `OperationFactory.CreateInvocationOperation` supplies the delegate, which executes and sets `ReturnVariable`. Selected operations adapt to `AlgorithmCallArguments`; other paths project values.
10. Declaration processing registers results where applicable; the runner appends evaluated statements and returns `CryptoScriptProgram` with diagnostic membership. On abort, runner diagnostics remain available and the exception propagates to the caller.

**[F/I: L07-L08, L17-L18, L21]** Semantic checks and execution are interleaved. An unknown function is checked before its arguments, but earlier statements can already have changed variables or produced output when a later statement fails. There is no separate whole-program semantic-validation component in this path. `CryptoScriptProgram` contains execution results, not the syntax tree. No rollback is established by the inspected loop.

The [Source-to-Operation Processing Sequence diagram](../diagrams/04-source-to-operation-sequence.mmd) uses a numbered flowchart as the established equivalent of a sequence view. Its explicit statement loop preserves lazy mapping/evaluation; parser-error and execution-error alternatives are distinct. Its argument note distinguishes identity-bearing references from value projections. Evidence: L04, L06-L11 and L17-L20.

## 4.9 Architectural Constraints and Verification Limits

The following observations distinguish verified code structure, potential consequences and behavior not established by this static analysis. The source evidence register makes those distinctions traceable to the recorded baseline; diagram entries identify the sources supporting each view. Intended public contracts and platform guarantees require evidence beyond the declarations and control flow inspected here.

### Verified architectural characteristics

**[F: L04-L08, L17, L20]** Language processing is reusable Core code called directly by Avalonia. AST nodes contain no generated contexts, but mappers depend on generated parser classes. `Execute(IEnumerable<StatementNode?>)` also permits AST input without parsing. Language, evaluation and operation dispatch share one Core assembly; logical diagrams do not imply separate services or assembly-enforced layers.

**[F: L12, L16-L17, L19]** Variable state and syntax diagnostics use shared singleton/static objects. Semantic diagnostic contexts are execution-owned. Generated metadata also initializes runtime lists. **[I]** These boundaries support syntax-model reuse without establishing isolated language sessions or independence from generated metadata.

**[F/I: L01-L08, L18-L19]** Extension touchpoints are explicit: a new syntactic construct can require grammar generation, AST/mapping and evaluator changes; a function using existing call syntax can use the existing FN rule but still needs dispatch support. Mechanism/parameter vocabulary and runtime handling have additional dependencies. These are maintenance constraints derived from the code, not redesign recommendations.

### Observed constraints and potential concerns

| Observation | Architectural consequence | Evidence |
| --- | --- | --- |
| Shared variable store replaces an existing identifier entry | Runner creation does not establish a fresh variable scope; duplicate names replace entries | L07, L12 |
| List, flags and accumulated syntax messages have different reset owners | Parser construction alone does not reset all syntax diagnostic state | L04, L16, L20 |
| GUI gate checks parser errors, not the lexer flag | A lexer-only failure has no equivalent explicit execution gate in that method; a concrete outcome was not exercised | L16, L20 |
| AST records have text but no spans | Precise original character locations are unavailable from AST properties alone | L05-L06, L17 |
| Declared type and concrete variable class differ | A KEY tag does not guarantee a typed key object; class-sensitive downstream resolution can distinguish them | L07, L10, L12-L13 |
| Nested results are reclassified from text | Returned-object identity is lost; existing tests record unquoted results falling back to an empty string expression | L08, L13, L21 |
| TR31H mapping exists without runtime type resolution | Header syntax must not be documented as executable declaration support | L05-L07, L12, L21 |
| INT has no grammar size limit, but runtime classification uses `int.TryParse` | Oversized decimal text can fall through to the empty-string expression fallback | L01, L13 |
| Path tokens consume the rest of a line; runtime matching is separate | Delimiters can be consumed, and lexical path acceptance does not imply equivalent runtime classification | L01, L15 |
| Generator version is unpinned and differs from runtime declaration | Committed generated code is inspectable; exact regeneration and compatibility are not established | L03, L20 |

**[F: L01, L12, L15, L20]** Cross-platform constraints are concrete: identifiers are ASCII; variable names remain case-sensitive; absolute Windows and Unix-style paths are represented as text without normalization. ScriptBook saving uses ASCII encoding, while loading uses `File.ReadAllLines`, relevant to non-ASCII string/path content. These declarations do not establish filesystem casing, valid filenames, accessibility or successful execution on a particular operating system.

### Unverified behavior and explicit limits

**Unverified:** no application tests, builds, grammar generation, concurrent execution, graphical startup or platform-specific path runs were performed for this recovery. Existing test definitions corroborate intended compatibility cases but are not current execution results. PDF/diagram validation establishes publication behavior only. Cryptographic correctness and standards conformance are outside this chapter.

**[Q]** Owner clarification is still needed on which preserved edge cases form the intended public language contract, the authoritative ANTLR generation environment, and the promised isolation between runners/sessions. Implementation behavior above is documented independently of those policy questions. No target architecture or speculative refactoring is proposed.

### Source evidence

All L entries refer to the local baseline `29b6b36e860ff278b845ac439dd123661760d098`. Paths are repository-relative; link fragments identify the first line of each stated range. Verification is static. Diagram evidence below uses the same identifiers.

| ID | Sources and verified locations | Supported findings |
| --- | --- | --- |
| L01 | [CryptoScript/Antlr/CryptoScript.g4, lines 1-57](../../../CryptoScript/Antlr/CryptoScript.g4#L1); [CryptoLexer.g4, lines 1-34 and 36-151](../../../CryptoScript/Antlr/CryptoLexer.g4#L1) | Combined grammar, imports, parser alternatives, lexical categories, ordering and path rules |
| L02 | [CryptoScript/Antlr/TR31Lexer.g4, lines 1-88](../../../CryptoScript/Antlr/TR31Lexer.g4#L1) | Header field-name/value vocabulary and general-token overlaps |
| L03 | [CryptoScriptLexer.cs, lines 1-11, 35-128 and 201-208](../../../CryptoScript/Antlr/CryptoScriptLexer.cs#L1); [CryptoScriptParser.cs, lines 180-250](../../../CryptoScript/Antlr/CryptoScriptParser.cs#L180); [CryptoScriptBaseVisitor.cs, lines 27-56](../../../CryptoScript/Antlr/CryptoScriptBaseVisitor.cs#L27); [CryptoScriptLexerExtensions.cs, lines 3-47](../../../CryptoScript/Antlr/CryptoScriptLexerExtensions.cs#L3); [CompileGrammar.bat, line 1](../../../CryptoScript/Antlr/CompileGrammar.bat#L1); [compile-grammar.sh, lines 6-9](../../../CryptoScript/Antlr/compile-grammar.sh#L6); [Tree.bat, line 1](../../../CryptoScript/Antlr/Tree.bat#L1) | Generated runtime classes/vocabulary, parser recovery, unused traversal scaffold, handwritten interception and external generation helpers; generated token/interpreter files inventoried in the same directory |
| L04 | [CryptoScript/Model/ParserBuilder.cs, lines 8-42](../../../CryptoScript/Model/ParserBuilder.cs#L8) | String/file construction, listener setup and reset differences |
| L05 | [Model/Ast/StatementNode.cs, lines 3-9](../../../CryptoScript/Model/Ast/StatementNode.cs#L3); [VariableDeclarationNode.cs, lines 6-11](../../../CryptoScript/Model/Ast/VariableDeclarationNode.cs#L6); [ExpressionNode.cs, lines 3-4](../../../CryptoScript/Model/Ast/ExpressionNode.cs#L3); [LiteralInitializerNode.cs, lines 3-6](../../../CryptoScript/Model/Ast/LiteralInitializerNode.cs#L3); [FunctionCallExpressionNode.cs, lines 3-15](../../../CryptoScript/Model/Ast/FunctionCallExpressionNode.cs#L3); [ParameterListInitializerNode.cs, lines 3-4](../../../CryptoScript/Model/Ast/ParameterListInitializerNode.cs#L3); [ParameterInitializerNode.cs, lines 3-4](../../../CryptoScript/Model/Ast/ParameterInitializerNode.cs#L3); [Tr31HeaderInitializerNode.cs, line 3](../../../CryptoScript/Model/Ast/Tr31HeaderInitializerNode.cs#L3) | Complete Core AST inventory, properties, inheritance, composition and missing spans |
| L06 | [AntlrToStatement.cs, lines 7-16](../../../CryptoScript/Model/AntlrToStatement.cs#L7); [AntlrToVariableDeclaration.cs, lines 8-18](../../../CryptoScript/Model/AntlrToVariableDeclaration.cs#L8); [AntlrToFunctionCallExpression.cs, lines 7-29](../../../CryptoScript/Model/AntlrToFunctionCallExpression.cs#L7); [AntlrToParameterInitializer.cs, lines 7-10](../../../CryptoScript/Model/AntlrToParameterInitializer.cs#L7) | Explicit constructors, GetText conventions, recursion, ordering and recovery values |
| L07 | [StatementEvaluator.cs, lines 11-51](../../../CryptoScript/Model/StatementEvaluator.cs#L11); [VariableDeclarationEvaluator.cs, lines 21-110](../../../CryptoScript/Model/VariableDeclarationEvaluator.cs#L21) | Statement selection, type/parameter checks, declaration registration and absent header evaluation |
| L08 | [FunctionCallEvaluator.cs, lines 20-118 and 128-205](../../../CryptoScript/Model/FunctionCallEvaluator.cs#L20); [ParameterEvaluator.cs, lines 5-9](../../../CryptoScript/Model/ParameterEvaluator.cs#L5) | Recognition before arguments, ordered recursive evaluation, argument kinds and semantic exceptions |
| L09 | [FunctionCall.cs, lines 12-59](../../../CryptoScript/Model/FunctionCall.cs#L12); [OperationInvocation.cs, lines 6-62](../../../CryptoScript/Model/OperationInvocation.cs#L6) | Runtime call, ordered resolved arguments, exact source reference and value projection |
| L10 | [CryptoAlgorithm/AlgorithmCallArguments.cs, lines 7-76](../../../CryptoScript/CryptoAlgorithm/AlgorithmCallArguments.cs#L7); [AlgorithmArgumentResolver.cs, lines 8-59](../../../CryptoScript/CryptoAlgorithm/AlgorithmArgumentResolver.cs#L8) | Algorithm-boundary representation, original/temporary object resolution and reconstruction marker |
| L11 | [Arguments/Argument.cs, lines 11-98](../../../CryptoScript/Arguments/Argument.cs#L11) | Runtime argument classes and named-parameter value substitution |
| L12 | [CryptoType.cs, lines 22-84](../../../CryptoScript/Model/CryptoType.cs#L22); [Variables/VariableDeclaration.cs, lines 6-12](../../../CryptoScript/Variables/VariableDeclaration.cs#L6); [VariableDictionary.cs, lines 8-49](../../../CryptoScript/Variables/VariableDictionary.cs#L8) | Type mapping, missing TR31H case, runtime value fields, singleton and identifier replacement |
| L13 | [Expressions/Expression.cs, lines 20-113](../../../CryptoScript/Expressions/Expression.cs#L20); [ExpressionPath.cs, lines 9-14](../../../CryptoScript/Expressions/ExpressionPath.cs#L9); [Variables/KeyVariableDeclaration.cs, lines 13-68](../../../CryptoScript/Variables/KeyVariableDeclaration.cs#L13) | Literal classification, type-tag/object distinction, path text and typed key metadata |
| L14 | [Variables/ParameterVariableDeclaration.cs, lines 17-111 and 140-142](../../../CryptoScript/Variables/ParameterVariableDeclaration.cs#L17) | Parameter storage, duplicate overwrite, explicit-name tracking and normalization |
| L15 | [Variables/FormatConversions.cs, lines 101-144](../../../CryptoScript/Variables/FormatConversions.cs#L101) | Value-format classification and Windows/UNC/Unix path regular expressions |
| L16 | [ErrorListner/SyntaxErrorListner.cs, lines 6-54](../../../CryptoScript/ErrorListner/SyntaxErrorListner.cs#L6); [LexerErrorListener.cs, lines 11-38](../../../CryptoScript/ErrorListner/LexerErrorListener.cs#L11); [SyntaxErrorList.cs, lines 10-21](../../../CryptoScript/ErrorListner/SyntaxErrorList.cs#L10) | Shared syntax diagnostics, separate flags, parser message accumulation and colon redirection |
| L17 | [CryptoScriptRunner.cs, lines 21-71](../../../CryptoScript/Model/CryptoScriptRunner.cs#L21); [CryptoScriptProgram.cs, lines 10-23](../../../CryptoScript/Model/CryptoScriptProgram.cs#L10); [SemanticDiagnosticExecution.cs, lines 18-65](../../../CryptoScript/Model/SemanticDiagnosticExecution.cs#L18); [ErrorListner/DiagnosticLocation.cs, lines 7-14](../../../CryptoScript/ErrorListner/DiagnosticLocation.cs#L7) | Lazy map/evaluate loop, AST-input overload, result container and execution diagnostic locations |
| L18 | [Operations/OperationFactory.cs, lines 10-88](../../../CryptoScript/Operations/OperationFactory.cs#L10); [CryptoOperations.cs, lines 14-94 and 261-288](../../../CryptoScript/Operations/CryptoOperations.cs#L14); [OutputOperations.cs, lines 24-25](../../../CryptoScript/Operations/OutputOperations.cs#L24); [GeneralOperations.cs, line 13](../../../CryptoScript/Operations/GeneralOperations.cs#L13) | Recognition/dispatch, object-preserving algorithm handoff and value-only projections |
| L19 | [AntlrLanguageMetadata.cs, lines 8-29](../../../CryptoScript/Model/AntlrLanguageMetadata.cs#L8); [MechanismList.cs, lines 8-10](../../../CryptoScript/Model/MechanismList.cs#L8); [Variables/ParameterTypeList.cs, lines 12-18](../../../CryptoScript/Variables/ParameterTypeList.cs#L12); L12 | Generated constant/vocabulary coupling and runtime list initialization |
| L20 | [CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs, lines 70-118 and 137-193](../../../CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs#L70); [GUI project, lines 38-40](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj#L38); [Core project, lines 1-21](../../../CryptoScript/CryptoScript.csproj#L1); [workflow, lines 17-30](../../../.github/workflows/dotnet.yml#L17) | Active GUI entry, syntax gate, result/error handling, line-based ScriptBooks, encoding, project/package declarations and no configured regeneration |
| L21 | [CryptoScriptUnitTest/Tr31HeaderInitializerAstTests.cs, lines 9-68](../../../CryptoScriptUnitTest/Tr31HeaderInitializerAstTests.cs#L9); [FunctionCallExpressionAstTests.cs, lines 28-68](../../../CryptoScriptUnitTest/FunctionCallExpressionAstTests.cs#L28); [FunctionCallEvaluatorTests.cs, lines 76-150 and 169-206](../../../CryptoScriptUnitTest/FunctionCallEvaluatorTests.cs#L76); [StatementExecutionTests.cs, lines 43-117 and 148-170](../../../CryptoScriptUnitTest/StatementExecutionTests.cs#L43); [SyntaxErrorTests.cs, lines 33-62](../../../CryptoScriptUnitTest/SyntaxErrorTests.cs#L33) | Inspected test assertions for mapping, text, identity, evaluation order, incomplete header support and lexical errors; no tests executed |
| L22 | [ParameterInitializerAstTests.cs, lines 9-70](../../../CryptoScriptUnitTest/ParameterInitializerAstTests.cs#L9); [VariableDeclarationAstTests.cs, lines 132-171](../../../CryptoScriptUnitTest/VariableDeclarationAstTests.cs#L132) | Recovery values, parameter duplicates and empty initializers; inspected assertions only |

Diagram evidence: **D04-COMPONENTS** uses L01-L08, L12-L13 and L17-L20; **D04-AST** uses L05-L06; **D04-SEQUENCE** uses L04, L06-L11 and L17-L20. All are logical views of the same baseline. Flowchart notation preserves the established publication checks; edge meanings and grouping limits are stated with each diagram. No diagram adds an independently deployed component or a whole-program validation phase.

[Previous: Project Structure and Dependencies](03-project-structure-and-dependencies.md) | [Return to architecture documentation](../README.md)
