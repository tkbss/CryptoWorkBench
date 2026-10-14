# 2. System Overview

[Architecture documentation](../README.md) | [Previous: Introduction](01-introduction.md) | [Baseline and evidence](../evidence/02-system-overview-baseline.md)

| Metadata | Value |
| --- | --- |
| Status | Initial documentation draft; awaiting final review |
| Implementation baseline | `d3099c80af3891ad54444e544f528c9c5ed6b93f` |
| Documentation baseline | `5c4b15d8487348ff904a04e9907caf9e00e868a0` |
| Documentation date | 2026-10-14 |
| Verification | Static repository inspection; no application build, test run or runtime observation |

This chapter describes the implemented system at the recorded baseline. **[F]** identifies verified implementation facts, **[O]** project-owner decisions or explicitly attributed historical context, **[I]** architectural interpretations, **[Q]** open questions and **[R]** recommendations. Owner statements are not implementation evidence. See the [evidence classifications](../evidence/02-system-overview-baseline.md#evidence-classifications).

## 2.1 System Context

**[F]** The desktop application combines an Avalonia GUI project and a desktop host with the reusable CryptoScript library. The GUI accepts script input, displays results and diagnostics, exposes variable and parameter editing, and opens or saves local ScriptBook files. See [S01 and S02](../evidence/02-system-overview-baseline.md#repository-evidence).

**[I]** CryptoScript Workbench is modeled as one software system with one desktop application process and an embedded scripting and cryptography library. The contextual actor **Workbench User** abstracts the implemented GUI interactions; it does not assert authentication, authorization or separate administrator, auditor or key-custodian roles. The context view is the [System Context diagram](../diagrams/02-system-context.mmd).

**[O]** CryptoScript is a reusable core, and the Avalonia desktop application is the active application client. Long-term reuse by other applications is an architectural objective. See [O01 and O02](../evidence/02-system-overview-baseline.md#project-owner-decisions-and-historical-context).

The system boundary follows the binding [scope definition in Chapter 1](01-introduction.md#13-scope-and-boundaries).

**[F]** The inspected paths establish local file access and core `Console.WriteLine` calls. The scoped source search did not establish a business-system integration through HTTP, RPC, databases, HSMs or payment-system connections. Mechanism names such as EMV, DUKPT, ep2 and TR-31 are not evidence of communication with cards, terminals or remote systems. This is a bounded inspection result, not a claim about every possible behavior of third-party libraries. See [S05, S06, S07 and S11](../evidence/02-system-overview-baseline.md#repository-evidence).

**[O]** A future web application is a long-term possibility outside the current development and architecture-recovery scope. No web architecture is defined here. This possibility is excluded from both current-state diagrams; detailed future planning belongs in Chapter 13, Architecture Evolution and Roadmap. See [O04](../evidence/02-system-overview-baseline.md#project-owner-decisions-and-historical-context).

## 2.2 System Responsibilities

| Classification | Responsibility | Baseline evidence |
| --- | --- | --- |
| **[F]** | Accept GUI script lines and parse them through the shared language frontend | S02, S04 |
| **[F]** | Map parser statements to AST nodes and evaluate declarations and function calls | S04 |
| **[F]** | Resolve operations, validate applicable mechanism contracts and dispatch cryptographic mechanisms | S05 |
| **[F]** | Maintain variables, keys and parameters in the in-process runtime model | S06 |
| **[F]** | Collect syntax and semantic diagnostics and expose them to GUI presentation | S02, S06 |
| **[F]** | Read local Markdown help, deliver output events and invoke `Console.WriteLine` in core output operations | S07 |
| **[F]** | Save ScriptBooks and read and execute their script lines when opening them | S02 |
| **[O]** | Use the Avalonia desktop application as the active client of the reusable core | O01, O02 |

All S references resolve through the [repository evidence table](../evidence/02-system-overview-baseline.md#repository-evidence).

Opening a ScriptBook is an execution action: `OpenScriptBook` reads lines and passes them to `ExecuteScriptBook`, which invokes the line execution path for lines other than recorded `out:` output. It is not solely a passive editor import. **[F: S02]**

The dispatch includes AES and Triple-DES mechanisms, MACs, hashes, key derivation, TR-31 wrapping and PIN-block handling. This is a description of inspected dispatch paths, not a guarantee that every argument, operation or mechanism conforms to an external standard or executes successfully. **[F: S05]**

## 2.3 Architectural Overview

**[I]** The product architecture combines one Avalonia desktop application with the reusable CryptoScript library used inside its process. The GUI directly invokes the parser and runner. No client/server boundary is established by these calls. See [S01, S02 and S04](../evidence/02-system-overview-baseline.md#repository-evidence).

| Concept | Meaning in this system |
| --- | --- |
| .NET project | **[F]** A build definition. Three production projects participate in the product architecture: CryptoScript, the GUI and the desktop host. The complete repository inventory is recorded separately in S01. |
| Assembly | **[I]** A code packaging boundary implied by the project organization. No emitted assembly, publish output or resolved dependency graph was inspected. |
| Application process | **[I]** The runtime boundary associated with starting the desktop host, which declares `WinExe`. No process was started during recovery. |
| Logical component | **[I]** A responsibility grouping such as parsing, evaluation, cryptography or diagnostics within the desktop application process. |
| C4 application container | **[I]** An executable application context. The desktop application is the single application container in this view. |
| Embedded library | **[F/I]** CryptoScript is directly referenced by the GUI and called within the desktop application context. It is not a separate executable service. |

**[F]** The product project-reference chain is desktop host → GUI project → CryptoScript. **[I]** The GUI and desktop entry-point projects therefore participate in one desktop application context. See S01 and S02. The [project reference diagram](../diagrams/01-project-references.mmd) shows this build-time view; it is not a complete repository inventory.

The [Container Overview diagram](../diagrams/02-container-overview.mmd) shows a single desktop process containing GUI composition, application services and presentation, together with the in-process CryptoScript library. Local file resources and standard output are distinguished from the application container.

## 2.4 Major System Components

| Component | Implemented responsibility | Evidence |
| --- | --- | --- |
| Desktop startup and GUI composition | Start Avalonia; compose the shell, views, view models and services through Prism | S02 |
| Application services and presentation | Register notification, history and documentation services; present editor output, variables, help and diagnostics through views and view models | S02, S06, S07 |
| Language frontend and AST mapping | Construct lexer, token stream and parser; map declarations and calls to own AST nodes | S04 |
| Execution coordination and evaluation | Evaluate statements in order and return statements and execution diagnostics | S04, S06 |
| Operation and mechanism dispatch | Resolve functions and arguments; call the selected cryptographic implementation | S05 |
| Variables, keys and parameters | Hold runtime declarations and expose them through the singleton variable dictionary | S06 |
| Diagnostics | Maintain syntax error state and execution-owned semantic diagnostic contexts | S06 |
| Output and local documentation | Raise output events, call `Console.WriteLine` and load local Markdown through the documentation provider | S07 |
| GUI file handling and history | Open and save ScriptBooks and retain line and information history in memory | S02 |

These components describe responsibilities inside the desktop application; they do not imply separately deployed services. Detailed internal structure belongs in the later language, runtime, cryptographic, diagnostics and GUI chapters. See the [source positions](../evidence/02-system-overview-baseline.md#repository-evidence).

## 2.5 External Dependencies

**[F]** The following are declared framework or package dependencies. Versions identify project declarations at the baseline, not restored, published or observed runtime versions. Examples of actual source use are recorded in S04, S08 and S09.

| Dependency | System-level role |
| --- | --- |
| .NET 10 | Target framework of the three product projects and both test projects; the complete six-project repository inventory is recorded in S01 |
| `Antlr4.Runtime.Standard` 4.13.1 | Lexer, token stream and parser runtime |
| `BouncyCastle.Cryptography` 2.7.0 | Cryptographic primitives; the AES-CMAC implementation uses `CMac` and `AesEngine` |
| `System.Security.Cryptography` | .NET cryptographic APIs; AES-CBC calls `Aes.Create()` |
| `Newtonsoft.Json` 13.0.4 | Key model JSON serialization and deserialization |
| Avalonia and Avalonia.Desktop 11.3.20 | GUI framework and desktop startup |
| Prism.Avalonia and Prism.DryIoc.Avalonia 9.0.537.11130 | Application composition, dependency registration and region navigation |
| Avalonia.AvaloniaEdit, Markdown.Avalonia and other declared UI packages | Editor, Markdown presentation, tables, icons, dialogs and additional controls |
| NUnit, Microsoft.NET.Test.Sdk, FluentAssertions and coverage tooling | Development and test dependencies, outside the production system context |

See [S01, S08, S09 and S10](../evidence/02-system-overview-baseline.md#repository-evidence). Package dependencies are not modeled as external business systems or independent application containers. GitHub Actions belongs to the development environment, not to a proven runtime integration.

## 2.6 Execution Environments

| Environment | Verified configuration | Interpretation limit |
| --- | --- | --- |
| Desktop application | **[F]** `StartWithClassicDesktopLifetime` and `UsePlatformDetect()` in the desktop entry point | Defines the desktop startup path; does not prove successful execution on any operating system |
| SDK selection | **[F]** `global.json` specifies SDK `10.0.401`, `latestPatch` roll-forward and no prerelease SDKs | Configuration only; no SDK invocation or restore was performed |
| Ubuntu CI | **[F]** Workflow declares `ubuntu-latest`, restore, build, tests and a separate GUI-test-project invocation | Shows configured CI intent; no workflow result or desktop GUI launch was verified |
| Windows | **[F]** Desktop project references a Windows manifest containing a Windows 10 compatibility identifier | Manifest metadata is not an executed compatibility test |
| macOS and other desktop targets | **[I]** Platform-neutral TFM and platform detection suggest portability intent; a project comment mentions Windows, macOS and Linux | No verified macOS execution or authoritative support matrix was established |

See [S01, S02 and S10](../evidence/02-system-overview-baseline.md#repository-evidence).

**[F]** CI invokes restore, build and tests for the root solution, followed by a separate GUI-test-project invocation. The GUI tests reference the GUI project but not the desktop entry-point project. The complete solution membership and test references are retained in [S01 and the repository inventory](../evidence/02-system-overview-baseline.md#repository-inventory-and-model-scope). **[I]** The CI declaration does not establish a successful desktop launch. See S10.

**[Q]** Supported operating-system versions, runtime architectures and distribution prerequisites remain to be confirmed with explicit project policy and execution or publication evidence. Keep configured environments, intended targets and validated support distinct.

## 2.7 Architectural Boundaries

| Boundary | Observation and interpretation |
| --- | --- |
| Application process | **[F/I]** The GUI calls the library directly within the desktop process. No separate CryptoScript server is established by the inspected code. |
| Library reuse | **[F]** The GUI project references CryptoScript. **[O]** Wider reuse is a long-term objective. **[I]** This is a code reuse boundary, not evidence of a network or security boundary. |
| Runtime state | **[F]** `VariableDictionary` and output events are static, while semantic diagnostics have execution-owned context. **[I]** Constructing a new runner does not by itself establish an isolated variable session. |
| GUI and core | **[F]** The GUI invokes the runner and also reads and edits variables directly. **[I]** It does not exclusively use a narrow execution-only interface. |
| Local files | **[F]** ScriptBooks are user-selected files. Help is read by default from `AppContext.BaseDirectory/InfoDocs`. **[I]** These have different ownership and lifecycle roles. |
| Output | **[F]** Core operations raise events and call `Console.WriteLine`. **[I]** Output is not fully delegated to a host-neutral presentation boundary. |
| Automated testing | **[F]** Unit tests reference CryptoScript; GUI tests reference the GUI project. **[I]** These projects and configured CI belong to development and verification infrastructure rather than the desktop runtime. Complete test references are retained in S01 and the repository inventory. |

See [S01, S02, S06 and S07](../evidence/02-system-overview-baseline.md#repository-evidence).

**[Q]** The intended isolation between multiple runners or future sessions, the long-term GUI/core interface, the distribution of local help, and the externally visible behavior of delegated Markdown links remain unresolved. They are recorded as [Q01 through Q04](../evidence/02-system-overview-baseline.md#open-questions).

**[R]** Preserve the distinction between runtime process boundaries, code packaging and logical responsibilities in subsequent diagrams. Future clients, integration APIs and additional architectural layers are outside this recovery step.

[Previous: Introduction](01-introduction.md) | [Return to architecture documentation](../README.md)
