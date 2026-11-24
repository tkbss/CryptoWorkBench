# 2. System Overview

[Architecture documentation](../README.md)

| Metadata | Value |
| --- | --- |
| Status | AR-01 draft; independent technical and linguistic review pending |
| Implementation baseline | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| Date | 2026-11-06 |
| Verification | Static source inspection; no new application tests |

## 2.1 System Context

**[F]** The desktop combines an Avalonia GUI and desktop entry point with the reusable CryptoScript library. The user edits/executes scripts, inspects results, variables, diagnostics and local help, and opens/saves ScriptBooks. Automation contracts and an implemented local MCP integration add an external client interaction. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05)

**[I]** The [system context](../diagrams/02-system-context.mmd) treats the workbench as one software system. The Workbench User actor abstracts GUI interactions; it does not invent administrator, auditor or key-custodian roles. A local automation client is now evidenced, so the old absence-of-network-integration finding no longer describes this baseline. This does not establish remote payment-system, database or HSM connectivity.

**[O]** The desktop remains the strategic application client, and reuse of CryptoScript is an architectural objective. A future web application remains outside current scope. MCP protocol/security design is deferred to AR-MCP-01.

## 2.2 System Responsibilities

**[F]** The language frontend builds lexer/parser state, maps statements into core-owned AST records and evaluates declarations, assignments and calls. Runtime operations resolve arguments and mechanisms, maintain variables/keys/parameters, produce diagnostics and output, and access local resources under operation-specific contracts. [R06](../evidence/ar-01-baseline-reconciliation.md#r06), [R08](../evidence/ar-01-baseline-reconciliation.md#r08), [R09](../evidence/ar-01-baseline-reconciliation.md#r09), [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R14](../evidence/ar-01-baseline-reconciliation.md#r14)

The host coordinates admitted work, syntax rejection, asynchronous execution, password requests, output capture and detached presentation snapshots. Opening a ScriptBook is not merely passive import: the editor import path executes script lines, while excluding recorded output lines. Cancellation and host coordination are relevant; this is not a new transaction covering the complete book. [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

Save/Load and secure snapshot operations, Delete, and file encryption/MAC/hash contracts extend the earlier in-memory-only overview. Do not confuse variable snapshots with ScriptBook source files. Each operation has its own validation/publication behavior; this chapter does not derive a universal filesystem transaction. [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15)

## 2.3 Architectural Overview

**[F/I]** A .NET project is a build definition; an assembly is a packaging boundary implied by it; a logical component groups responsibilities. None of these automatically defines another operating-system process. CryptoScript is an embedded library called in the desktop process. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04)

The [container view](../diagrams/02-container-overview.mmd) includes the desktop process and the separate invocation of the same desktop executable with --mcp-stdio. That startup branch runs the pinned proxy and returns before Avalonia startup. The normal desktop can start LocalMcpHost, which delegates to the host's automation implementation through shared interfaces. Mcp is a library project, not a separate server executable project. The proxy and desktop are distinct execution contexts; the exact transport/session analysis is intentionally deferred. [R04](../evidence/ar-01-baseline-reconciliation.md#r04)

Local help, ScriptBooks and runtime data files are resources, not deployed services. Core Console.WriteLine calls still exist, but normal desktop startup redirects inherited standard output/error to TextWriter.Null; presentation uses captured output. A call to Console.WriteLine must not be diagrammed as a guaranteed visible terminal. [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15)

## 2.4 Major System Components

| Component | Responsibility |
| --- | --- |
| Desktop startup | Select proxy or classic desktop path; attach desktop MCP integration |
| GUI composition | Prism/DryIoc shell, view/view-model and service composition |
| Host services | Admission, editor coordination, execution pipeline, password interaction and presentation |
| Automation contracts | Host-neutral interfaces, DTOs and contract representation |
| MCP adapter | Local transport/proxy and delegation to automation interfaces |
| Language frontend | Grammar-derived lexer/parser, syntax diagnostics and explicit AST mapping |
| Runtime | Ordered statement evaluation, shared variables and execution-owned diagnostics |
| Operations | Function/mechanism resolution, cryptography, persistence and file operations |
| Documentation/output | Local InfoDocs provider, output events and host capture |

**[F]** These responsibilities are supported by [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R07](../evidence/ar-01-baseline-reconciliation.md#r07), [R09](../evidence/ar-01-baseline-reconciliation.md#r09), [R13](../evidence/ar-01-baseline-reconciliation.md#r13), [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15). **[I]** They are logical groupings, not independently deployed services. Detailed algorithm, diagnostics, GUI and MCP internals belong to subsequent packages.

## 2.5 External Dependencies

**[F]** The product targets .NET 10. Core declares Antlr4.Runtime.Standard 4.13.1, BouncyCastle.Cryptography 2.7.0 and Newtonsoft.Json 13.0.4; framework cryptography APIs are separate from these NuGet declarations. GUI/desktop declare Avalonia 12.1.4 and related UI packages. Prism is integrated through prepared source references. Mcp declares ModelContextProtocol.AspNetCore [2.3.0] and Microsoft.AspNetCore.App. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R03](../evidence/ar-01-baseline-reconciliation.md#r03)

Package declarations describe dependencies, not observed restored/deployed versions. UI packages support editing, Markdown rendering, icons, dialogs and tables; they are not external business systems. NUnit, test SDKs, assertions and coverage tooling are development dependencies. Chapter 3 records package/build details.

## 2.6 Execution Environments

**[F]** The desktop uses StartWithClassicDesktopLifetime, UsePlatformDetect and Inter fonts. global.json selects SDK 10.0.401 with rollForward disabled and prerelease SDKs disallowed. CI declares Windows x64, Linux x64, macOS ARM64 and macOS Intel jobs with RID-specific publication. Its Prism-aware stages now include both build configurations, core/GUI/MCP suites, targeted regressions and a native file-publication check. [R03](../evidence/ar-01-baseline-reconciliation.md#r03), [R04](../evidence/ar-01-baseline-reconciliation.md#r04)

The production desktop MCP pairing control is visible only on Windows in this baseline. That restriction must not be generalized into a claim that all desktop functionality is Windows-only, or that MCP is certified on every matrix platform. No current application run or CI result was observed in AR-01. Historical successful CI observations remain historical. **[Q]** Official OS/runtime versions, distribution prerequisites and interactive startup support require explicit policy and appropriate execution evidence.

## 2.7 Architectural Boundaries

**[F/I]** Core has no GUI/MCP project dependency. That supports reuse but does not prove complete host independence: variables and output remain shared, syntax diagnostics include static state, and operations can access files or request host interaction. InterpreterExecution coordinates ownership and state access; it does not allocate one variable dictionary per runner. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R07](../evidence/ar-01-baseline-reconciliation.md#r07), [R12](../evidence/ar-01-baseline-reconciliation.md#r12), [R13](../evidence/ar-01-baseline-reconciliation.md#r13), [R14](../evidence/ar-01-baseline-reconciliation.md#r14)

The host now centralizes reset/gating of syntax diagnostics and async execution in WorkbenchExecutionPipeline. The GUI/core boundary remains broader than an execution-only facade because host services also capture/present variable state. On execution failure, previously completed effects can remain; capture success is separate from script success. A capture failure marks presentation stale and does not authorize replay. Assignment target replacement and individual file/snapshot publication contracts must not be presented as whole-script rollback. [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

**[Q]** Long-term client isolation policy, comprehensive platform support and external behavior of delegated help hyperlinks remain separate investigations. **[R]** Preserve the distinction between actual coordination and promised isolation, and between package, process and logical boundaries.

[Architecture documentation](../README.md)
