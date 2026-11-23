# 3. Project Structure and Dependencies

[Architecture documentation](../README.md)

| Metadata | Value |
| --- | --- |
| Status | AR-01 draft; independent technical and linguistic review pending |
| Implementation baseline | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| Date | 2026-11-06 |
| Verification | Static source inspection; no new application tests |

## 3.1 Repository Organization

**[F]** Production/test projects remain siblings at repository root rather than a src/tests hierarchy. CryptoScript owns Antlr, Model/Ast, Operations, CryptoAlgorithm, Variables, ErrorListner, Documentation and InfoDocs, with additional persistence, secret-sharing and file-operation areas. The GUI owns Views, ViewModels, Models, Services, styles and assets. These are directories inside projects, not separately compiled layers. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02), [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15)

The eight root-level projects comprise five production libraries/host projects and three test projects. Additional tracked projects are TestInfrastructure/Avalonia.Headless.Backport and eng/mcp/native/B3NativeProbe. The retained backport declares Avalonia 11.3.20; the active GUI tests instead declare Avalonia.Headless 12.1.4. Inventory presence must not imply active production use. Prepared upstream Prism sources under .deps are a separate dependency category. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02), [R03](../evidence/ar-01-baseline-reconciliation.md#r03)

ScriptBooks contains examples. .github holds CI and validation scripts; eng/prism holds preparation/build policy and locks; docs contains architecture and implementation evidence. docs/architecture now exists in the implementation baseline as well as the integrated recovery tree. CryptoWorkBenchConsole and its reflection-based ConsoleSemanticDiagnosticTests fixture are removed.

## 3.2 Solution Structure

**[F]** The root solution includes CryptoScript, CryptoScriptUnitTest, Automation.Abstractions, Mcp and Mcp.Tests. It does not include the GUI or desktop host. The Avalonia solution includes CryptoScript, GUI, Desktop, GUI tests, Automation.Abstractions, Mcp and Mcp.Tests, plus four prepared Prism projects: Prism.Avalonia, Prism.Core, Prism.Events and Prism.DryIoc.Avalonia. It does not include the core test project. ScriptBooks remains a virtual solution folder, not a project. [R02](../evidence/ar-01-baseline-reconciliation.md#r02)

Both solutions retain Debug/Release configurations. Solution membership differs from transitive project reachability and from product scope. Neither solution alone is a complete repository inventory. **[Q]** A comprehensive developer-entry solution remains a policy question; explicit build scripts provide the current build entry rather than requiring a speculative solution redesign.

## 3.3 Production Projects and Responsibilities

| Project | Output and responsibility |
| --- | --- |
| CryptoScript | Library; language, evaluation, cryptography, runtime state, diagnostics and help |
| CryptoWorkBench.Automation.Abstractions | Library; automation interfaces and DTO/contracts without project references |
| CryptoWorkBench.Mcp | Library; transport/proxy adaptation to automation interfaces |
| CryptoWorkBenchAvalonia | Library; GUI, Prism composition, host/application coordination and presentation |
| CryptoWorkBenchAvalonia.Desktop | WinExe; platform startup, desktop MCP attachment and --mcp-stdio startup branch |

**[F]** These projects target net10.0. App derives from PrismApplication. The GUI implements/registers automation services; Desktop composes the transport with that implementation. The pipeline, rather than the old direct editor-only parser/runner path, owns shared parse/execute/presentation handling. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05)

**[I]** Startup, host adaptation and core reuse are distinct responsibilities. Language processing and cryptography still share one core assembly, so their directory organization does not enforce independent layers.

## 3.4 Project Dependencies

| Referencing project | Direct ProjectReference targets |
| --- | --- |
| CryptoScript | None |
| Automation.Abstractions | None |
| Mcp | Automation.Abstractions |
| GUI | CryptoScript; Automation.Abstractions; prepared Prism.Avalonia and Prism.DryIoc.Avalonia |
| Desktop | GUI; Mcp |
| Core tests | CryptoScript |
| GUI tests | GUI; Mcp |
| MCP tests | Mcp |
| B3NativeProbe | Desktop; GUI tests |

**[F]** The [product graph](../diagrams/01-project-references.mmd) shows direct first-party production references; upstream Prism is declared separately above. Desktop reaches CryptoScript transitively through GUI, and reaches Automation.Abstractions through GUI and Mcp. Mcp has no direct GUI or CryptoScript reference. GUI tests use core types transitively. The native probe's direct Desktop reference must not be confused with an ordinary test-project reference. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02)

**[I]** The first-party graph has no project cycle. Interface sharing enables host adaptation; it does not by itself prove runtime isolation, authorization or separately deployed services.

## 3.5 External Package Dependencies

**[F]** These are direct declarations, not a restored transitive package audit. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02), [R03](../evidence/ar-01-baseline-reconciliation.md#r03)

| Area | Declared packages / policy |
| --- | --- |
| Core | Antlr4.Runtime.Standard 4.13.1; BouncyCastle.Cryptography 2.7.0; Newtonsoft.Json 13.0.4 |
| GUI framework | Avalonia, Markup.Xaml.Loader, Themes.Fluent and Fonts.Inter 12.1.4 |
| UI controls | AvaloniaEdit 12.0.0; DataGrid 12.1.2; FluentIcons 2.1.344; Markdown.Avalonia 12.0.2; MessageBox.Avalonia 12.0.0; DialogHost 0.12.3; UVtools 5.2.0 |
| Diagnostics / desktop | DiagnosticsSupport 2.2.3 in Debug only; Avalonia.Desktop 12.1.4 |
| MCP | ModelContextProtocol.AspNetCore [2.3.0]; Microsoft.AspNetCore.App framework reference |
| Core / GUI tests | Test SDK 18.9.0; NUnit 4.6.1; adapter 6.3.0; FluentAssertions 8.10.0; analyzers 4.14.0; coverlet 6.0.4 |
| Additional tests | GUI: Avalonia.Headless 12.1.4; MCP: JsonSchema.Net 9.4.1, Test SDK/NUnit/adapter |

Prism's former NuGet references have become prepared source references. Directory.Build.targets requires the temporary Prism environment. eng/prism policy scopes lockfiles by configuration and publish RID and enables locked restore. NuGet.Config selects nuget.org. The central AvaloniaVersion is now 12.1.4, although project declarations remain explicit. Nullable is enabled; GUI uses latest language version and explicitly retains reflection-binding defaults. The historical claim of no locks/NuGet configuration is superseded.

ANTLR generated headers still identify 4.13.2 against runtime 4.13.1. Helpers invoke external antlr4 without pinning the generator. This is a reproducibility question, not established incompatibility. Different UI package version numbers are also not proof of incompatibility.

## 3.6 Build and Test Project Organization

**[F]** The workflow runs on pushes/PRs for master and retains four runner/RID pairs: windows-latest/win-x64, ubuntu-latest/linux-x64, macos-26/osx-arm64 and macos-26-intel/osx-x64. It uses Invoke-PrismCi for Debug/Release builds, native checks, Core/Gui/Coordinator/F1/C1/Mcp suites and RID publication. The wrapper checks exact SDK 10.0.401, enters the prepared Prism environment, checks exit codes and requires discovered passing tests rather than trusting stale TRX files. [R03](../evidence/ar-01-baseline-reconciliation.md#r03)

Publication is framework-dependent with apphost, locked RID restore and explicit output validation. The validator checks required assets, runtime/dependency manifests, RID, native assets, executable permission where applicable and packaged InfoDocs. Core imports native file-publication build targets; macOS publication requires an actual macOS host/toolchain rather than an assumed Windows cross-publish. The [coverage diagram](../diagrams/03-build-and-test-dependency-coverage.mmd) separates CI selection from build references.

Historical master CI run 38710123715 at efa81d6 remains evidence that the earlier missing desktop-build coverage was resolved there. It is not a result for fbbadae2. No current CI run or application test was executed for AR-01; configured checks are not observed success. Native file checks do not establish every graphical interaction, installer behavior or OS support promise. Generated ANTLR sources remain committed and are not regenerated by the inspected application build path.

## 3.7 Dependency Boundaries and Constraints

**[F/I]** Core project independence, host/GUI startup separation and shared automation contracts are real code boundaries. They do not eliminate mutable global state, host callbacks, static output events or local file access. Friend-assembly visibility is test access, not a reverse project dependency. No general assembly-layer redesign is implied by the new directories or contracts. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R12](../evidence/ar-01-baseline-reconciliation.md#r12), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

The console test dependency and the old unconditional GUI diagnostics-package observation are superseded. Remaining questions include a comprehensive developer solution, authoritative ANTLR generation, future Prism packaging policy and official platform support. Ongoing Prism/GUI worktree changes are excluded. **[R]** Keep current declarations, historical validation and future recommendations distinct; do not rewrite production dependencies as part of architecture recovery.

[Architecture documentation](../README.md)
