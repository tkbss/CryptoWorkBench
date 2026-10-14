# 1. Introduction

[Architecture documentation](../README.md) | [Baseline and evidence](../evidence/01-introduction-baseline.md)

| Metadata | Value |
| --- | --- |
| Status | Completed initial draft; awaiting review |
| Analysis baseline | `d3099c80af3891ad54444e544f528c9c5ed6b93f` |
| Analysis date | 2026-10-13 |
| Verification | Static repository inspection; no build or runtime validation |

## 1.1 Purpose of This Document

This document recovers and explains the current architecture of CryptoScript Workbench from verifiable repository evidence. It provides a common reference for understanding the system, tracing responsibilities and dependencies, and guiding subsequent architecture analysis.

The implemented architecture is the subject of recovery. Architectural interpretations, unresolved questions, recommendations and future plans are explicitly labeled. This documentation does not change the application architecture or establish a new target design.

Every implementation finding is tied to an analysis baseline and a source reference. This makes the findings reviewable as the repository evolves. The initial baseline and its limitations are recorded in the [evidence index](../evidence/01-introduction-baseline.md#analysis-baseline).

## 1.2 System Purpose

**[F]** The repository contains a CryptoScript language and execution library, a console frontend, an Avalonia GUI library and a desktop entry point. Both the console and the GUI editor build a parser through `ParserBuilder.StringBuild`, obtain a program context and invoke `CryptoScriptRunner.Execute`. The desktop entry point starts the Avalonia application. These statements are supported by [E01 and E02](../evidence/01-introduction-baseline.md#repository-evidence).

**[F]** The inspected cryptographic dispatch includes AES and Triple-DES mechanisms, message authentication, hashing, key derivation, TR-31 wrapping and PIN-block mechanisms. These are code-level observations, not a certification or a guarantee that every input or operation succeeds. See [E03](../evidence/01-introduction-baseline.md#repository-evidence).

**[I]** CryptoScript Workbench can therefore be understood as a workbench for expressing and executing cryptographic operations through a shared scripting core and separate console and GUI entry points. This interpretation is derived from the frontend calls and project references in E01 and E02; it does not imply a general command-line interface or a separate execution service.

## 1.3 Scope and Boundaries

The recovery scope is the CryptoWorkBench repository. Chapter 1 establishes the purpose, boundaries, evidence conventions and document organization. Subsequent chapters will analyze the language, runtime, cryptography, diagnostics, interfaces, execution sequences, GUI, tests and architecture assessment using their own recorded evidence.

**[F]** The core, console, GUI and desktop project files inspected for this introduction target `net10.0`. The core declares references to `Antlr4.Runtime.Standard`, `BouncyCastle.Cryptography` and `Newtonsoft.Json`; the GUI and desktop projects declare Avalonia dependencies. These are project declarations at the baseline, not a report of restored or deployed dependency versions. See [E01](../evidence/01-introduction-baseline.md#repository-evidence).

**[P]** Version 1 focuses on symmetric cryptography, with hashing, authentication, derivation and wrapping as related workbench capabilities. Version 2 is planned to include classical asymmetric cryptography, certificate handling, Cryptographic Message Syntax (CMS) and post-quantum mechanisms. This version split is the project scope supplied for this architecture-recovery task. It describes plans and focus areas, not verified release tags or implemented version 2 functionality.

**[F]** The existing mechanism roadmap explicitly marks ECDSA, RSA-OAEP and RSA-PSS as not implemented. The baseline factory does not dispatch these mechanisms. An `Rsa` class exists, but its key and parameter generation overrides delegate to `AsymmtricCryptoAlgorithm`, whose corresponding methods construct declaration objects. Class presence alone does not establish usable RSA functionality. See [E04](../evidence/01-introduction-baseline.md#repository-evidence).

**[Q]** The contracts, integration boundaries and delivery sequence for certificate handling, CMS and post-quantum mechanisms remain to be analyzed. This introduction establishes no implemented support for these planned areas. Repository evidence and explicit future design decisions will be needed before describing their architecture.

Local uncommitted application changes are excluded from the committed baseline findings. No build, test run, runtime trace, package restore or platform validation was performed for this introduction. Security properties, standards conformance and supported deployment environments require additional evidence; they are not inferred from package names or framework choices.

## 1.4 Intended Audience

The intended readers are developers and maintainers seeking to understand the code, reviewers checking architecture findings against repository evidence, and project stakeholders assessing current scope and future evolution.

Readers should be able to begin with the system overview and then follow the chapters relevant to their work. The introduction requires no knowledge of individual implementation classes. Later technical findings will name the actual source elements and explain their role using repository references.

## 1.5 Architecture Documentation Approach

Recovery proceeds from evidence to explanation. Each analysis records its commit baseline, inspects relevant project and source files, cites the supporting declarations or methods, and separates direct observations from interpretation. Tests and runtime observations may supplement later findings only when their execution and results are recorded.

Markdown is the leading text format. Mermaid is the primary diagram source format. Diagram labels and legends are written in English. Diagrams must explain their abstraction level and edge semantics and cite their evidence; a project reference must not be presented as a runtime message flow.

The initial [project reference diagram](../diagrams/01-project-references.mmd) shows four production projects and three direct `ProjectReference` declarations. **[F]** Every arrow points from the referencing project to the referenced project. It omits test projects and external packages and makes no statement about deployment topology or execution order. Its evidence and legend are recorded in [D01](../evidence/01-introduction-baseline.md#diagram-evidence).

**[R]** Subsequent recovery work should preserve this evidence-first approach and add detailed diagrams only after verifying their relationships. This recommendation follows from the need to prevent inferred relationships or roadmap items from being mistaken for implemented behavior.

## 1.6 Conventions and Terminology

| Term or label | Meaning in this documentation |
| --- | --- |
| CryptoScript Workbench | The system described by this architecture document |
| CryptoWorkBench | The repository name and existing solution naming |
| CryptoScript | The core project; identifiers retain their source spelling |
| Analysis baseline | The immutable Git commit used for implementation findings |
| **[F] Verified implementation fact** | A directly evidenced declaration, relationship or code behavior; static inspection is identified as such |
| **[I] Architectural interpretation** | An explicitly identified inference from verified facts |
| **[Q] Open question** | An unresolved matter requiring further evidence or a decision |
| **[R] Recommendation** | A proposed investigation or improvement, separate from the current implementation |
| **[P] Plan** | Intended future scope, separate from implemented functionality |
| Mechanism | A cryptographic mechanism identifier used in the inspected dispatch, such as `AES-CBC` |
| GUI | Graphical user interface |
| CMS | Cryptographic Message Syntax; a planned version 2 scope item |

Evidence identifiers such as E01 refer to entries in the [evidence index](../evidence/01-introduction-baseline.md#repository-evidence). Existing identifiers, including `AsymmtricCryptoAlgorithm`, are reproduced exactly to support source lookup. Neither an identifier nor a package dependency proves a complete user-facing capability.

Documentation links use portable relative paths with forward slashes and exact filename casing. Markdown and Mermaid sources belong under Git. Generated PDF, DOCX and rendered diagram files belong under `generated/` as reproducible outputs unless a later publication policy explicitly requires committing them.

## 1.7 Document Organization

The [documentation entry point](../README.md#table-of-contents) contains the approved 13-chapter structure and navigation to completed content. Chapters 2 and 3 establish the system and project context. Chapters 4 through 8 cover the language, runtime, cryptography, diagnostics and integration. Chapter 9 covers data flows and execution sequences; Chapter 10, GUI Architecture, follows it immediately. Chapters 11 through 13 cover testing, architecture assessment and future evolution.

Chapter sources are stored in `chapters/`, Mermaid sources in `diagrams/`, and verification records in `evidence/`. The areas `decisions/`, `assets/` and `tools/` are reserved and will be created when corresponding content becomes available. Unanalyzed chapters have table-of-contents entries and reserved filenames, but no placeholder technical content.

The documentation is ready to receive subsequent recovery findings. Chapter 1 remains an initial draft for review, and later findings must distinguish the implemented baseline from assessments and future scope.

[Return to architecture documentation](../README.md)
