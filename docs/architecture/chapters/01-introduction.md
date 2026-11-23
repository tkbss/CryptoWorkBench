# 1. Introduction

[Architecture documentation](../README.md)

| Metadata | Value |
| --- | --- |
| Status | AR-01 draft; independent technical and linguistic review pending |
| Implementation baseline | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| Date | 2026-11-06 |
| Verification | Static source inspection; no new application tests |

## 1.1 Purpose of This Document

This architecture recovery explains the implemented CryptoScript Workbench through verifiable repository evidence. It supports developers tracing responsibilities, dependencies and execution behavior. It recovers the current system rather than defining a target design. Every current implementation finding uses the pinned baseline above and the shared source register. Static inspection establishes declarations and control flow, not passing tests, deployment support or cryptographic conformance.

The earlier Chapters 1–3 were reviewed at their historical baselines; Chapter 4 remained a draft. AR-01 changes do not inherit that approval. Exact historical chapter and diagram bytes are retained under evidence/history/abbf7b7b, and original evidence and review reports remain intact. Historical claims must be read at their original commits.

## 1.2 System Purpose

**[F]** The Avalonia desktop application hosts a reusable CryptoScript interpreter and cryptographic operation library. The desktop entry point starts the classic Avalonia lifetime; GUI application services coordinate parsing, execution, output and variable presentation. The shared execution pipeline invokes ParserBuilder and the asynchronous runner. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

**[F]** Dispatch includes symmetric encryption, MACs, hashing, key derivation, TR-31 and PIN-block operations. Snapshot persistence, secure import/export and file-oriented operation contracts extend the runtime's local resource responsibilities. Mechanism names do not demonstrate communication with payment systems, cards or HSMs. [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15)

**[I]** The system is a desktop workbench for expressing and executing cryptographic operations through an embedded, reusable scripting core. Automation contracts and MCP expose an additional host integration boundary without turning CryptoScript into a separate server.

## 1.3 Scope and Boundaries

**[O]** The established product scope includes the reusable core, Avalonia desktop host, GUI composition, application services, presentation, language processing, runtime, cryptography, diagnostics, documentation and relevant verification infrastructure. AR-01 includes implemented automation/MCP project and process boundaries. Detailed MCP transport, security, lease and lifecycle analysis is reserved for AR-MCP-01. Unfinished GUI redesign and changes in other worktrees are excluded.

**[F]** CryptoWorkBenchConsole and its console-specific GUI test fixture are absent from the pinned tree. The earlier Rsa stub and AsymmtricCryptoAlgorithm infrastructure are also absent; their old descriptions are historical evidence, not current capability. The complete project inventory is in Chapter 3. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02), [R14](../evidence/ar-01-baseline-reconciliation.md#r14)

**[P]** The owner-supplied version focus remains symmetric cryptography for version 1, with classical asymmetric cryptography, certificates, CMS and post-quantum mechanisms planned for version 2. This is planning context, not verified release tags or implemented support. **[Q]** Contracts and delivery sequencing for these areas require later evidence and decisions. A future web application remains outside current recovery scope.

## 1.4 Evidence and Verification

Current source identities R01–R16 are shared between languages. The machine-readable register records immutable commit, repository path, SHA-256 and line count for each source. Chapter references identify the relevant responsibility or declaration; the evidence appendix provides source links. Inspection of test assertions is labeled separately from test execution. Historical CI and PDF results retain their original dates and inputs.

**[R]** Reassess any claim when its source changes. Distinguish declared dependencies, restored packages, observed runtime behavior and officially supported environments. No cryptographic certification or complete isolation guarantee follows from project structure.

## 1.5 Architecture Views

The [project-reference view](../diagrams/01-project-references.mmd) depicts first-party production projects. A solid arrow points from referencing to referenced project. It is a build-time view, not a runtime message sequence or complete repository inventory. Prepared upstream Prism projects and test/support projects are inventoried in Chapter 3. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02)

**[R]** Keep process boundaries, package boundaries and logical components distinct. Add relationships only after verifying their implementation.

## 1.6 Conventions and Terminology

**[F]** means verified implementation fact; **[O]** owner decision/context; **[I]** architectural interpretation; **[Q]** open question; **[R]** recommendation; **[P]** plan. Identifiers retain exact source spelling. CryptoScript Workbench names the system; CryptoWorkBench names the repository/solutions; CryptoScript names the core. A mechanism is a dispatch identifier, not a certification claim.

English source paths remain stable. German sources use the same filenames beneath de/. Chapter/section numbers, evidence identities, code and source targets are identical. Markdown and Mermaid are authoritative; generated PDFs, SVGs and QA files remain ignored. Translation terminology and review limitations are recorded in the bilingual publication decision.

## 1.7 Document Organization

The thirteen-chapter architecture plan is retained. This AR-01 edition publishes Chapters 1–4 only. The preserved partial Chapter 9 covers partial-state execution and presentation at its own historical baseline; it is excluded from both PDFs. Chapters 5–8 and 10–13 remain planned rather than fabricated analysis. The unfinished GUI redesign is not described.

Chapter 2 establishes runtime context, Chapter 3 packaging/build dependencies, and Chapter 4 language processing. Runtime and cryptographic detail belong to Chapters 5–6; diagnostics to Chapter 7; integration to Chapter 8; broader flows to Chapter 9; GUI architecture to Chapter 10; testing, assessment and evolution to Chapters 11–13.

[Architecture documentation](../README.md)
