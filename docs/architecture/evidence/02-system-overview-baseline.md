# System Overview: Baseline, Evidence and Owner Context

[Architecture documentation](../README.md) | [Chapter 2](../chapters/02-system-overview.md)

## Analysis Baselines

| Item | Record |
| --- | --- |
| Implementation baseline | `d3099c80af3891ad54444e544f528c9c5ed6b93f` |
| Documentation baseline | `5c4b15d8487348ff904a04e9907caf9e00e868a0` |
| Documentation branch | `docs/architecture-recovery` |
| Documentation date | 2026-10-14 |
| Method | Static inspection of source, project, solution and CI configuration files |
| Checkout verification | The initial Chapter 2 inspection recorded a clean architecture worktree and application files matching the implementation baseline. The scope correction began with an existing README modification and four untracked Chapter 2 documentation files; those drafts were revised in place. |
| Excluded material | Uncommitted changes in other worktrees; future integrations; runtime assumptions unsupported by source |
| Application execution | No build, restore, test run, publication or application startup performed |

Relative source links point to the checkout. File positions below refer to the implementation baseline; they are meaningful because the inspected application files match that commit. To reproduce a finding independently of future checkout changes, read the exact path at that commit:

```text
git show d3099c80af3891ad54444e544f528c9c5ed6b93f:CryptoWorkBenchAvalonia.Desktop/Program.cs
```

## Evidence Classifications

| Label | Classification | Evidence rule |
| --- | --- | --- |
| **[F]** | Verified implementation fact | Supported by repository declarations or calls at the implementation baseline; static inspection does not establish successful execution |
| **[O]** | Project-owner decision | Supplied and approved by the project owner in this architecture-recovery conversation on 2026-10-14; historical context is explicitly identified as owner-supplied context |
| **[I]** | Architectural interpretation | A model or inference derived from cited facts; not an additional implementation fact |
| **[Q]** | Open question | Requires evidence, policy or a decision not established by this inspection |
| **[R]** | Recommendation | Advice about documentation or further investigation, separate from the recovered implementation |

The existing **[P] Plan** convention remains applicable to roadmap material. It does not turn an owner decision or historical account into implemented behavior.

## Repository Evidence

All S entries describe static inspection at the implementation baseline. S01 is a repository inventory, and S03 records an inspected entry point outside the product architecture. These sources are retained for traceability; their presence does not add that client to the model. S11 retains the original search scope, which was broader than the modeled product.

| ID | Sources and baseline positions | Supported observation |
| --- | --- | --- |
| S01 | [Core project, line 4](../../../CryptoScript/CryptoScript.csproj#L4); [console project, lines 4-11](../../../CryptoWorkBenchConsole/CryptoWorkBenchConsole.csproj#L4); [GUI project, lines 3 and 39](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj#L3); [desktop project, lines 3-17](../../../CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj#L3); [unit-test project, lines 4 and 35](../../../CryptoScriptUnitTest/CryptoScriptUnitTest.csproj#L4); [GUI-test project, lines 4 and 27-28](../../../CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj#L4); [root solution, lines 6-10](../../../CryptoWorkBench.sln#L6); [GUI solution, lines 6-22](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln#L6) | Six projects target net10.0. Console declares Exe; desktop declares WinExe. Desktop references GUI; GUI and console reference CryptoScript. GUI tests reference GUI and console; unit tests reference CryptoScript. The root solution includes core, console and unit tests. No emitted assembly was inspected. |
| S02 | [Desktop Program.Main, lines 13-21](../../../CryptoWorkBenchAvalonia.Desktop/Program.cs#L13); [App.CreateShell and registration, lines 36-72](../../../CryptoWorkBenchAvalonia/App.axaml.cs#L36); [editor view input handler, lines 78-90](../../../CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs#L78); [editor view model, lines 47-193](../../../CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs#L47); [sidebar file handling, lines 69-168](../../../CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs#L69); [history service, lines 11-34](../../../CryptoWorkBenchAvalonia/Services/HistoryService.cs#L11) | Desktop startup initializes Avalonia. Prism composes views, view models and services. Editor ExecuteLine calls parser and runner and transports diagnostics. SaveScriptBook writes lines; OpenScriptBook reads lines and ExecuteScriptBook evaluates them except recorded out: lines. Sidebar provides file pickers. History uses in-memory lists. |
| S03 | [Console Program.Main, lines 9-24; ExecuteProgram, lines 26-50; WriteDiagnostics, lines 53-57; CreateInput, lines 60-66](../../../CryptoWorkBenchConsole/Program.cs#L9) | Console execution uses four built-in examples. Main does not consume args for user-script input. It calls parser and runner, writes example text and formats diagnostics to Console.Error. Source inspection does not establish that every example succeeds. |
| S04 | [ParserBuilder.StringBuild, lines 8-27](../../../CryptoScript/Model/ParserBuilder.cs#L8); [CryptoScriptRunner.Execute and MapStatements, lines 31-70](../../../CryptoScript/Model/CryptoScriptRunner.cs#L31); [AntlrToStatement.Map, lines 7-16](../../../CryptoScript/Model/AntlrToStatement.cs#L7); [StatementEvaluator.EvaluateCore, lines 32-51](../../../CryptoScript/Model/StatementEvaluator.cs#L32); [FunctionCall.Call, lines 34-44](../../../CryptoScript/Model/FunctionCall.cs#L34) | Parser creation, mapping to own AST nodes, ordered statement evaluation and operation invocation are in-process calls. The runner returns a CryptoScriptProgram and execution diagnostics; it does not start an execution service. |
| S05 | [OperationFactory.CreateInvocationOperation, lines 10-45](../../../CryptoScript/Operations/OperationFactory.cs#L10); [CryptoOperations invocation overloads, lines 14-94](../../../CryptoScript/Operations/CryptoOperations.cs#L14); [AlgorithmFactory.Create, lines 6-75](../../../CryptoScript/CryptoAlgorithm/AlgorithmFactory.cs#L6) | Operations resolve and validate applicable arguments and mechanism contracts and dispatch to cryptographic classes. The factory includes AES, DES3, HASH, HMAC, key derivation, TR-31 and PIN-block paths. These are local code paths, not evidence of external payment-system or HSM connections. |
| S06 | [VariableDictionary, lines 8-49](../../../CryptoScript/Variables/VariableDictionary.cs#L8); [syntax listener state, lines 8-9](../../../CryptoScript/ErrorListner/SyntaxErrorListner.cs#L8); [SemanticDiagnosticExecution, lines 6-25](../../../CryptoScript/Model/SemanticDiagnosticExecution.cs#L6); [GUI variable editing, lines 21-49](../../../CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs#L21); [GUI diagnostic presentation, lines 22-33](../../../CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs#L22) | The variable dictionary and syntax status include static state. Semantic diagnostic context is execution-owned. GUI variable editing directly updates core objects. GUI diagnostic display is implemented separately from core diagnostic collection. |
| S07 | [OutputOperations, lines 10-44](../../../CryptoScript/Operations/OutputOperations.cs#L10); [FileInfoDocumentationProvider, lines 17-25 and 64-67](../../../CryptoScript/Documentation/FileInfoDocumentationProvider.cs#L17); [InfoViewModel.TryLoadDocument, lines 97-116](../../../CryptoWorkBenchAvalonia/ViewModels/InfoViewModel.cs#L97); [KeyMapViewModel.LoadInfo, lines 24-32](../../../CryptoWorkBenchAvalonia/ViewModels/KeyMapViewModel.cs#L24); [InfoView hyperlink delegation, lines 54-68](../../../CryptoWorkBenchAvalonia/Views/InfoView.axaml.cs#L54) | Core output uses static events and Console.WriteLine. Help defaults to AppContext.BaseDirectory/InfoDocs and File.ReadAllText. The GUI reads help through its provider and also invokes Info(keymap) through the runner. Non-internal Markdown links are delegated to the library's default command; its external behavior was not inspected. |
| S08 | [Core package and help-copy declarations, lines 17-31](../../../CryptoScript/CryptoScript.csproj#L17); [GUI package declarations, lines 21-35](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj#L21); [desktop package declaration, lines 12-17](../../../CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj#L12) | Core declares ANTLR runtime 4.13.1, BouncyCastle 2.7.0 and Newtonsoft.Json 13.0.4. GUI/desktop declare Avalonia 11.3.20; GUI declares Prism packages 9.0.537.11130 and other UI dependencies. Help-copy declarations exist, but no publish output was verified. |
| S09 | [AES_CMAC.ModeMac, lines 11-19](../../../CryptoScript/CryptoAlgorithm/AES/AES_CMAC.cs#L11); [AES_CBC.ModeDecryption, lines 68-94](../../../CryptoScript/CryptoAlgorithm/AES/AES_CBC.cs#L68); [key JSON serialization, lines 74-97](../../../CryptoScript/Variables/KeyVariableDeclaration.cs#L74) | Actual source use includes BouncyCastle CMac/AesEngine, .NET Aes.Create and Newtonsoft.Json serialization/deserialization. These observations do not establish standards conformance or all third-party runtime behavior. |
| S10 | [SDK policy, lines 2-5](../../../global.json#L2); [CI configuration, lines 15-30](../../../.github/workflows/dotnet.yml#L15); [Windows manifest, lines 3 and 14-15](../../../CryptoWorkBenchAvalonia.Desktop/app.manifest#L3); [desktop platform comment and target, lines 4-6](../../../CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj#L4) | SDK 10.0.401 is configured with latestPatch and no prerelease SDKs. CI declares Ubuntu and restore/build/test commands, including GUI tests. The Windows manifest declares a Windows 10 compatibility identifier. A project comment mentions Windows/macOS/Linux but supplies no executed platform evidence. |
| S11 | Scoped search of C# and project files in CryptoScript, CryptoWorkBenchConsole, CryptoWorkBenchAvalonia and CryptoWorkBenchAvalonia.Desktop; generated build directories excluded | Searched for application-level network, database, process-launch and HSM APIs. No corresponding external integration was established. The System.Net.Mime using directive is not a network call, and XAML namespace URLs are not service connections. This bounded negative finding does not audit all third-party implementation code. |

S11 used a read-only search for identifiers including HttpClient, WebClient, HttpListener, WebApplication, CreateHostBuilder, TcpClient, UdpClient, Socket, Grpc, DbContext, SqlConnection, Npgsql, Sqlite, ProcessStartInfo, Process.Start, DllImport, LibraryImport, Pkcs11, PKCS11, HSM, System.Net and System.Data. Matches were interpreted as code, not automatically treated as integrations.

## Repository Inventory and Model Scope

**[F]** S01 establishes the complete six-project repository inventory and all six direct `ProjectReference` declarations:

| Project file | Direct project references | Documentation treatment |
| --- | --- | --- |
| [CryptoScript](../../../CryptoScript/CryptoScript.csproj) | None | Reusable product core |
| [CryptoWorkBenchAvalonia](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj) | CryptoScript | GUI composition, application services and presentation |
| [CryptoWorkBenchAvalonia.Desktop](../../../CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj) | CryptoWorkBenchAvalonia | Desktop application startup |
| [CryptoWorkBenchConsole](../../../CryptoWorkBenchConsole/CryptoWorkBenchConsole.csproj) | CryptoScript | Historical development client; inventory only, excluded from product architecture |
| [CryptoScriptUnitTest](../../../CryptoScriptUnitTest/CryptoScriptUnitTest.csproj) | CryptoScript | Relevant automated testing infrastructure |
| [CryptoWorkBenchGuiTest](../../../CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj) | CryptoWorkBenchAvalonia; CryptoWorkBenchConsole | Relevant automated testing infrastructure; its reference to an excluded project remains an inventory fact |

**[F]** The root solution includes CryptoScript, CryptoWorkBenchConsole and CryptoScriptUnitTest. CI also invokes the GUI test project separately (S01, S10). These build and test declarations do not determine the modeled runtime boundary.

**[O]** O05 sets a narrower product architecture scope than this inventory. All architecture diagrams and later chapter plans follow that scope. Complete inventories retain excluded projects and existing references without expanding the product model.

## Project-Owner Decisions and Historical Context

These records come from the project owner's clarifications and documentation approval in this conversation on 2026-10-14. They are separate from the implementation baseline.

| ID | Classification | Owner-supplied statement and documentation consequence |
| --- | --- | --- |
| O01 | **[O] Decision / architectural objective** | CryptoScript should remain usable by other applications in the long term. Describe a fundamentally reusable core; do not claim a new integration interface or complete independence from current clients. |
| O02 | **[O] Decision** | The Avalonia desktop application is the current strategic application client. |
| O03 | **[O] Historical context and current status** | CryptoWorkBenchConsole is a historical development client with no current or planned use. |
| O04 | **[O] Scope decision / long-term perspective** | A future web application is only a long-term possibility and is outside current development and recovery scope. Do not design APIs, services or additional layers; do not include a future application in current-state diagrams. |
| O05 | **[O] Binding documentation scope decision** | The modeled product comprises the reusable CryptoScript core, the Avalonia desktop application, GUI composition, application services and presentation, language processing, runtime and cryptography, diagnostics and documentation facilities, and relevant automated testing infrastructure. Exclude CryptoWorkBenchConsole from product architecture, every architecture diagram and subsequent chapter planning. Repository inventories and source evidence remain complete. This decision replaces the earlier diagram-inclusion decision; implementation and documentation baselines are unchanged. |

## Diagram Evidence

### D02-CONTEXT

Source: [System Context](../diagrams/02-system-context.mmd).

| Element or relationship | Classification and evidence |
| --- | --- |
| Workbench User | **[I]** Abstraction of implemented GUI interactions in S02 and S06; not a role or permission model |
| CryptoScript Workbench | **[I]** Product software-system boundary derived from S01, S02 and S04-S07 under O05 |
| User → system: Uses the desktop workbench | **[F]** Editor input and file interactions in S02 |
| System → user: Shows results, variables, diagnostics and local help | **[F]** GUI presentation and provider paths in S02, S06 and S07 |
| Active desktop and reusable core note | **[O]** O01, O02 and O05; visually separate from interaction arrows |
| Scope note | S11; local files are resources, not asserted business-system integrations |

The visible title identifies C4 Level 1. The visible legend separates facts, interpretations and owner context. The system boundary covers the active desktop application and its reusable core under O05. No excluded client, future client or unverified external system is shown.

### D02-CONTAINERS

Source: [Container Overview](../diagrams/02-container-overview.mmd).

| Element | Classification and evidence |
| --- | --- |
| Desktop Workbench Application | **[I]** One application/process context containing desktop host, GUI and library; S01, S02 and S04. Active client status and scope are **[O]**, O02 and O05. |
| Avalonia UI, composition and application services | **[F]** Desktop startup and Prism registration of views, view models, notification, history and documentation services; S02, with presentation paths in S06 and S07 |
| CryptoScript library — in-process | **[F/I]** GUI project reference and direct parser/runner calls in S01, S02 and S04; no standalone library container |
| Packaged Help Files | **[F]** Local file dependency in S07 and copy declarations in S08; not a verified publication output or central file server |
| ScriptBook Files | **[F]** User-selected files accessed by GUI paths in S02; outside the desktop process frame |
| Standard output | **[F]** Core `Console.WriteLine` calls in S07; a stream resource, not another application |

| Relationship | Evidence |
| --- | --- |
| Desktop client → embedded core: parser/runner calls and variable access | S02, S04, S06 |
| Embedded core → desktop client: results, diagnostics and output events | S02, S04, S06, S07 |
| User → desktop client: edits, executes and inspects | S02, S06 |
| Desktop client → ScriptBooks: reads, executes and saves | S02 |
| Desktop client → help files: provider reads | S07, InfoViewModel.TryLoadDocument |
| Embedded core → help files: Info() reads | S07, OutputOperations and KeyMapViewModel |
| Embedded core → standard output: Console.WriteLine calls | S07 |

The invisible links in the Mermaid source position the legend only; they are not architectural relationships.

Only the desktop application frame is an executable application container. The embedded library box annotates code within that process. Solid arrows describe local interactions, not a network protocol or execution sequence. Core output API calls do not establish an attached visible terminal or a second application container.

The diagram follows O05 and is deliberately narrower than the repository inventory. Testing infrastructure is described in S01 and S10 and remains outside the production runtime diagram.

## Open Questions

| ID | Question | Evidence needed |
| --- | --- | --- |
| Q01 | What isolation is intended for multiple runners or future sessions? | Explicit session policy and focused evaluation of the static state in S06 |
| Q02 | Which operating systems, runtime architectures and installation prerequisites are supported, and how are local help files delivered? | Support policy plus successful execution/publication evidence; S08-S10 provide declarations only |
| Q03 | Is direct GUI editing of core variables intended as the long-term application boundary? | Owner decision on the interface boundary identified in S06; no new abstraction is designed here |
| Q04 | What external behavior does the delegated Markdown hyperlink command permit? | Inspection or observation of the relevant library behavior; S07 establishes delegation only |

## Verification Limits

**[F]** All implementation findings above come from static source and configuration inspection. No successful application execution, external integration, security certification, cryptographic conformance or platform compatibility is asserted.

**[R]** Future recovery findings should preserve commit, source path, position, symbol and verification method, and should distinguish code capability from actually exercised entry-point behavior.

## Scope Correction Verification — 2026-10-14

- Reviewed Chapters 1 and 2, both evidence records, all three diagrams and the architecture README against O05. The root README contains only the repository title and requires no correction.
- Kept the implementation baseline `d3099c80af3891ad54444e544f528c9c5ed6b93f` and documentation baseline `5c4b15d8487348ff904a04e9907caf9e00e868a0`. All original E01-E04 and S01-S11 source-evidence rows are unchanged.
- Compared the complete inventory with the project declarations: six projects and six direct project references, including the GUI-test reference to the excluded development client.
- Parsed and rendered all three Mermaid sources with Mermaid 11.12.0 and visually inspected the results. No parser or rendering errors remain; the container view has one desktop application process with an embedded CryptoScript library.
- Checked relative link targets, heading and source-line anchors, exact filename casing, diagram evidence paths and whitespace. Reviewed all affected documentation content for English language and consistent system boundaries.
- Compared file hashes before and after the correction: only the eight architecture Markdown/Mermaid files changed. Application code, tests, project and solution files, the root README and `.codex/config.toml` were unchanged; non-documentation tracked files also match the implementation baseline.
- No application build, test run, staging, commit or push was performed. The scope correction is ready for review.
