# Introduction: Analysis Baseline and Evidence

[Architecture documentation](../README.md) | [Chapter 1](../chapters/01-introduction.md)

## Analysis Baseline

| Item | Record |
| --- | --- |
| Baseline commit | `d3099c80af3891ad54444e544f528c9c5ed6b93f` |
| Analysis date | 2026-10-13 |
| Method | Static inspection of committed project files, implementation files and existing mechanism roadmap |
| Source selection | `git show <baseline>:<repository-relative-path>` |
| Runtime validation | None; no builds, tests, restores or runtime traces performed |
| Working tree | Application changes existed before documentation creation; those changes are excluded from these findings |

Relative source links below resolve to the current checkout. The recorded commit, path and inspected declaration identify the authoritative evidence even if the working tree or a later commit changes the linked file. To reproduce a finding, read the specified path at the baseline commit, for example:

```text
git show d3099c80af3891ad54444e544f528c9c5ed6b93f:CryptoScript/Model/ParserBuilder.cs
```

The initial working-tree inspection found modifications in these tracked application files:

```text
CryptoScript/Antlr/CryptoLexer.g4
CryptoScript/Antlr/CryptoScript.interp
CryptoScript/Antlr/CryptoScript.tokens
CryptoScript/Antlr/CryptoScriptLexer.cs
CryptoScript/Antlr/CryptoScriptLexer.interp
CryptoScript/Antlr/CryptoScriptLexer.tokens
CryptoScript/Antlr/CryptoScriptParser.cs
CryptoScript/CryptoAlgorithm/AES/AES_CMAC.cs
CryptoScript/CryptoAlgorithm/AlgorithmFactory.cs
CryptoScript/CryptoAlgorithm/DES3/DES3_RETAIL.cs
CryptoScript/Model/MechanismParameterContractValidator.cs
CryptoScript/Model/MechanismRegistry.cs
CryptoScript/Model/ParameterRegistry.cs
```

Untracked application content was also present under `CryptoScript/CryptoAlgorithm/EMV/` and in `CryptoScriptUnitTest/EmvApplicationCryptogramTests.cs`. Further untracked EMV test files appeared during inspection. This is a record of the non-clean analysis environment, not evidence of committed behavior. No findings in Chapter 1 rely on those local changes.

## Repository Evidence

All entries below are **[F] verified implementation facts** established by static inspection at the baseline. They identify declarations and calls; they do not report executed tests or observed runtime success.

| ID | Repository sources | Inspected elements and supported observations |
| --- | --- | --- |
| E01 | [Core project](../../../CryptoScript/CryptoScript.csproj), [console project](../../../CryptoWorkBenchConsole/CryptoWorkBenchConsole.csproj), [GUI project](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj), [desktop project](../../../CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj) | `TargetFramework` is `net10.0` in these four files. Console and GUI each declare a `ProjectReference` to CryptoScript. Desktop declares a `ProjectReference` to the GUI. The core declares ANTLR runtime, BouncyCastle and Newtonsoft.Json packages; GUI and desktop declare Avalonia packages. |
| E02 | [Parser builder](../../../CryptoScript/Model/ParserBuilder.cs), [runner](../../../CryptoScript/Model/CryptoScriptRunner.cs), [console entry point](../../../CryptoWorkBenchConsole/Program.cs), [GUI editor view model](../../../CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs), [desktop entry point](../../../CryptoWorkBenchAvalonia.Desktop/Program.cs) | `ParserBuilder.StringBuild` constructs an ANTLR input stream, lexer, token stream and parser. `CryptoScriptRunner.Execute` maps parser statements and evaluates statement nodes. Console `Main` and GUI `ExecuteLine` call the parser and runner. Desktop `Main` starts the Avalonia application with a classic desktop lifetime. |
| E03 | [Algorithm factory](../../../CryptoScript/CryptoAlgorithm/AlgorithmFactory.cs), [AES-CBC implementation](../../../CryptoScript/CryptoAlgorithm/AES/AES_CBC.cs), [HASH implementation](../../../CryptoScript/CryptoAlgorithm/HASH/HASH.cs), [HMAC implementation](../../../CryptoScript/CryptoAlgorithm/HMAC/HMAC.cs) | `AlgorithmFactory.Create` dispatches AES, DES3, HASH, HMAC, key-derivation, TR-31 and PIN-block mechanisms. `AES_CBC.ModeEncryption` and `ModeDecryption` call .NET AES transforms. HASH and HMAC define supported mechanism sets and their corresponding operation methods. This supports the introductory capability categories, without claiming conformance or exhaustive operation coverage. |
| E04 | [Existing mechanism roadmap](../../../CryptoScript/InfoDocs/MechanismRoadmap.md), [RSA class](../../../CryptoScript/CryptoAlgorithm/RSA/Rsa.cs), [asymmetric base class](../../../CryptoScript/CryptoAlgorithm/AsymmetricCryptoAlgorithm.cs), [algorithm factory](../../../CryptoScript/CryptoAlgorithm/AlgorithmFactory.cs) | The roadmap labels ECDSA, RSA-OAEP and RSA-PSS as not implemented. `AlgorithmFactory.Create` has no dispatch for those identifiers. `Rsa.GenerateKey` and `GenerateParameters` delegate to the base; the corresponding `AsymmtricCryptoAlgorithm` methods construct declaration objects. These inspected methods do not establish a usable RSA mechanism. |

## Interpretation and Planning Sources

**[I] Shared core with console and GUI entry points:** Derived from the three direct project references in E01 and frontend calls in E02. This is an architectural interpretation of those facts, not a separate deployment or service boundary.

**[P] Product version scope:** The architecture-recovery task specifies symmetric cryptography as the version 1 focus and classical asymmetric cryptography, certificate handling, CMS and post-quantum mechanisms as planned version 2 scope. The task is the source of that planning scope, not implementation evidence. E04 corroborates the planned status of specific asymmetric mechanisms, but does not establish the broader version 2 delivery plan or support for certificates, CMS or post-quantum mechanisms.

**Documentation structure:** The user confirmed the 13 chapter titles and the seven Introduction section titles on 2026-10-13. The approved structure is recorded in the documentation README and Chapter 1. It is an organizational requirement, not a recovered implementation fact.

## Diagram Evidence

**D01:** [Project reference overview](../diagrams/01-project-references.mmd).

- Classification: **[F]**, based on the `ProjectReference` declarations in E01.
- Scope: Four production projects; test projects and external packages are omitted.
- Legend: Each solid arrow means a direct project reference. The arrow starts at the referencing project and ends at the referenced project.
- Abstraction: Build-time project relationships only; arrows do not represent runtime messages, execution order or deployment connections.
- Source of truth: Mermaid source and baseline project declarations. Rendered files are reproducible outputs.

## Limitations and Follow-Up

**[Q]** Detailed language, runtime, GUI, integration and cryptographic contracts are not established by this introductory inspection. Later chapters require their own analysis and evidence.

**[Q]** Version 2 contracts and integration boundaries require repository evidence or explicit design decisions before they can be described as architecture.

**[R]** For each later recovery finding, record the baseline, source path, relevant symbol or declaration, verification method and limitations. Record tests or runtime observations only when performed. This keeps findings reviewable and prevents source-level inspection from being mistaken for runtime or standards validation.
