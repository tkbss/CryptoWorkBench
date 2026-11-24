# 3. Project Structure and Dependencies

[Architecture documentation](../README.md) | [Previous: System Overview](02-system-overview.md) | [Baseline and evidence](../evidence/03-project-structure-and-dependencies-baseline.md) | [Next: Language Architecture](04-language-architecture.md)

| Metadata | Value |
| --- | --- |
| Status | Reviewed |
| Implementation baseline | `efa81d6ca282ad7a2b8959d0f406f741c87006b0` |
| Earlier implementation baseline | `d3099c80af3891ad54444e544f528c9c5ed6b93f` — retained for Chapters 1 and 2 |
| Documentation starting point | `19c79fe39698c0d833b206abd600495d6eb508ef`, branch `docs/architecture-recovery` |
| Documentation date | 2026-10-15 |
| Verification | Static inspection of commit-addressed sources and independent inspection of master Actions run 38710123715; no local application build or test execution |

This chapter continues the existing recovery. **[F]** marks verified source declarations or observed CI metadata, **[O]** owner decisions, **[I]** interpretations, **[Q]** open questions and **[R]** recommendations. Source identifiers C01–C12 and run observations V01–V03 resolve through the [evidence record](../evidence/03-project-structure-and-dependencies-baseline.md#repository-evidence).

**[O]** The strategic product consists of the desktop host, Avalonia GUI and reusable CryptoScript library. The historical development client remains outside all product architecture diagrams. Complete repository inventories retain its existence and test dependency. The [Chapter 2 decisions O01–O05](../evidence/02-system-overview-baseline.md#project-owner-decisions-and-historical-context) remain binding.

The new baseline changes only the CI workflow and adds its publish validator. Project files, Solutions, application sources, tests and package declarations are unchanged from the earlier implementation baseline. **[F: C01]** To preserve the documentation branch without commits or merges, the updated baseline is incorporated through fetched Git objects and immutable source references; the checkout's implementation files remain at the earlier baseline. CI links below therefore address the new commit explicitly. See [baseline incorporation](../evidence/03-project-structure-and-dependencies-baseline.md#baseline-incorporation-and-preservation).

## 3.1 Repository Organization

**[F: C02, C03]** Projects are siblings at repository root; there is no separate `src/` and `tests/` hierarchy.

| Directory or root file | Responsibility |
| --- | --- |
| `CryptoScript/` | Language processing, execution, cryptographic operations, variables, diagnostics and documentation facilities |
| `CryptoWorkBenchAvalonia/` | GUI, application composition, presentation and application services |
| `CryptoWorkBenchAvalonia.Desktop/` | Desktop startup, platform selection, manifest and host assets |
| `CryptoScriptUnitTest/` | Core unit, script integration, documentation-contract and regression tests |
| `CryptoWorkBenchGuiTest/` | GUI model/presentation and view integration tests, with an existing historical-client test dependency |
| `CryptoWorkBenchConsole/` | Historical development client; repository inventory only, outside the product model |
| `ScriptBooks/` | Example script books; selected files are exposed as Solution Items |
| `.github/workflows/` | GitHub Actions workflow |
| `.github/scripts/` | Publish-output validator at the new implementation baseline; absent from the older checkout |
| `docs/architecture/` — documentation branch only | Architecture chapters, evidence and Mermaid sources in the Architecture Recovery worktree on `docs/architecture-recovery`; absent from implementation baseline `efa81d6ca282ad7a2b8959d0f406f741c87006b0` |
| `Directory.Build.props` | Shared Nullable setting and an Avalonia version property |
| `global.json` | SDK selection policy |
| `CryptoWorkBench.sln` | Root Solution; membership is documented in Section 3.2 |

Within CryptoScript, `Antlr/` holds grammars, generated C# sources and helper scripts; `Model/` and `Model/Ast/` hold language/runtime models, mapping and evaluation; `Operations/`, `CryptoAlgorithm/`, `Variables/`, `ErrorListner/`, `Documentation/` and `InfoDocs/` group their respective responsibilities. The GUI uses `Views/`, `ViewModels/`, `Models/`, `Services/`, `Styles/` and `Assets/`. **[F: C02, C06]**

**[I]** These directories organize code within projects; they do not define separately compiled layers. Core-internal dependency analysis belongs to the later language, runtime and cryptographic chapters.

## 3.2 Solution Structure

**[F: C03]** The repository contains two Solution files. They select overlapping project sets:

| Project | [Root Solution](../../../CryptoWorkBench.sln) | [Avalonia Solution](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln) |
| --- | --- | --- |
| CryptoScript | Included | Included |
| CryptoWorkBenchAvalonia | — | Included |
| CryptoWorkBenchAvalonia.Desktop | — | Included |
| CryptoScriptUnitTest | Included | — |
| CryptoWorkBenchGuiTest | — | Included |
| CryptoWorkBenchConsole — historical inventory | Included | Included |

Both Solutions declare Debug and Release configurations for Any CPU, with build mappings for all included projects. The Avalonia Solution also has a virtual `ScriptBooks` folder containing four files from the root `ScriptBooks/` directory. It is a Solution folder, not a .NET project. **[F: C03]**

**[I]** Neither Solution represents the complete repository or combines the desktop host with both test suites. Solution membership is distinct from the project-reference graph and product scope. The current CI selects explicit project roots, so its coverage no longer depends on either Solution's membership.

The membership table supplies the needed repository/Solution comparison. A second diagram of the same membership would add little information.

## 3.3 Production Projects and Responsibilities

**[F: C04, C06]** All six repository projects use `Microsoft.NET.Sdk` and target `net10.0`. The three product projects divide responsibilities as follows:

| Product project | Output declaration | Implemented responsibility |
| --- | --- | --- |
| [CryptoScript](../../../CryptoScript/CryptoScript.csproj) | No explicit `OutputType`; SDK library default | Parsing and AST mapping, script execution, operation/mechanism dispatch, cryptography, variables, diagnostics and help access |
| [CryptoWorkBenchAvalonia](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj) | No explicit `OutputType`; SDK library default | Views, ViewModels, editor integration, Prism composition, navigation and application services |
| [CryptoWorkBenchAvalonia.Desktop](../../../CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj) | `WinExe` | Configures Avalonia and starts the classic desktop application lifetime |

The host's `Program.Main` and `BuildAvaloniaApp` configure `App`, platform detection, Inter fonts and trace logging. GUI `App` derives from `PrismApplication` and registers the shell, regions, views, ViewModels and services through Prism/DryIoc. The editor ViewModel directly constructs the parser and invokes the CryptoScript runner. **[F: C06; reused S02 and S04]**

**[I]** This is a startup boundary between host and GUI, and a reusable-library boundary between GUI and Core. The GUI also contains application coordination; it is not limited to passive visual controls. Parsing, evaluation and cryptography share one Core project rather than separate assemblies.

The full repository inventory additionally contains one historical `Exe` project and two test projects declaring `IsPackable=false`. Their inventory presence does not expand the strategic product model. **[F: C04; O05]**

## 3.4 Project Dependencies

**[F: C04]** The complete repository graph has six direct, unconditional `ProjectReference` declarations and no project cycle:

| Referencing project | Direct project references | Additional transitively reachable projects |
| --- | --- | --- |
| CryptoScript | None | None |
| CryptoWorkBenchAvalonia | CryptoScript | None |
| CryptoWorkBenchAvalonia.Desktop | CryptoWorkBenchAvalonia | CryptoScript |
| CryptoScriptUnitTest | CryptoScript | None |
| CryptoWorkBenchGuiTest | CryptoWorkBenchAvalonia; CryptoWorkBenchConsole | CryptoScript through both paths |
| CryptoWorkBenchConsole — historical inventory | CryptoScript | None |

The product-only graph remains:

`CryptoWorkBenchAvalonia.Desktop → CryptoWorkBenchAvalonia → CryptoScript`

Reuse the existing [Product Project References diagram](../diagrams/01-project-references.mmd). Its two arrows represent direct project references. Desktop-to-Core is transitive and must not be presented as a third direct edge. The original diagram baseline remains valid because all project files are unchanged at the new baseline. **[F: C01, C04]**

GUI tests use Core types through transitive references; they do not declare a direct Core reference. No test project references the desktop host. The historical test reference is real: a test fixture loads that assembly and invokes non-public methods through reflection. These are repository/testing facts, not a reason to add that client to product architecture diagrams. **[F: C04, C07]**

## 3.5 External Package Dependencies

**[F: C05]** The following direct package versions are declared in the project files:

| Project | Package | Declared version |
| --- | --- | --- |
| CryptoScript | Antlr4.Runtime.Standard | 4.13.1 |
| CryptoScript | BouncyCastle.Cryptography | 2.7.0 |
| CryptoScript | Newtonsoft.Json | 13.0.4 |
| Desktop | Avalonia.Desktop | 11.3.20 |
| GUI | Avalonia; Avalonia.Themes.Fluent; Avalonia.Fonts.Inter | 11.3.20 each |
| GUI | Avalonia.AvaloniaEdit | 11.4.1 |
| GUI | Avalonia.Controls.DataGrid | 11.3.12 |
| GUI | AvaloniaUI.DiagnosticsSupport | 2.1.1 |
| GUI | FluentIcons.Avalonia | 2.0.319 |
| GUI | Markdown.Avalonia | 11.0.3 |
| GUI | MessageBox.Avalonia | 3.3.1.1 |
| GUI | Prism.Avalonia; Prism.DryIoc.Avalonia | 9.0.537.11130 each |
| GUI | UVtools.AvaloniaControls | 4.1.0 |
| Both test projects | FluentAssertions | 8.10.0 |
| Both test projects | Microsoft.NET.Test.Sdk | 18.9.0 |
| Both test projects | NUnit | 4.6.1 |
| Both test projects | NUnit3TestAdapter | 6.3.0 |
| Both test projects | NUnit.Analyzers | 4.14.0 |
| Both test projects | coverlet.collector | 6.0.4 |

The historical project has no direct PackageReference. Core packages support parsing, cryptographic primitives and JSON handling; GUI packages support presentation and composition. Actual source use of ANTLR, BouncyCastle, .NET cryptography and Newtonsoft.Json is already recorded in S04, S08 and S09. Framework APIs such as `System.Security.Cryptography` are distinct from direct NuGet declarations.

**[F: C05, C08]** Versions are literals in individual project files. No repository-owned `Directory.Packages.props`, package lock file or NuGet configuration was found. `Directory.Build.props` defines `AvaloniaVersion=11.0.2`, but repository-owned PackageReference versions do not use that property. Its possible consumption by imported third-party targets was not inspected. Both test projects repeat the same test-package versions; analyzers and the coverage collector declare `PrivateAssets=all`.

`global.json` selects SDK `10.0.401`, `latestPatch` roll-forward and no prerelease SDKs. CI reads that file rather than repeating a literal SDK version. Nullable is enabled centrally and in projects; the GUI additionally uses `LangVersion=latest`. Core and Core-test project files suppress selected compiler warnings. **[F: C08]**

**[I/Q]** Distributed package versions and the separate Avalonia property create maintenance ambiguity. Different version numbers across different UI packages do not by themselves establish incompatibility. Generated ANTLR lexer/parser files identify generator 4.13.2 while the declared runtime is 4.13.1; no incompatibility has been established. The successful master workflow is evidence for its exercised build/test paths, not a general compatibility guarantee.

This table is not a restored transitive NuGet graph or an audit of deployed package versions. The CI validator checks assets listed by the generated dependency manifest without recording a complete package inventory in this chapter.

## 3.6 Build and Test Project Organization

**[F: C09]** [PR #1](https://github.com/tkbss/CryptoWorkBench/pull/1) replaced the former single-Ubuntu workflow with a four-entry GitHub-hosted runner matrix. Pushes to `master` and pull requests targeting `master` trigger the workflow; `fail-fast=false` allows the remaining matrix jobs to continue after a failure.

| Environment | Runner label | Publish RID |
| --- | --- | --- |
| Windows x64 | `windows-latest` | `win-x64` |
| Linux x64 | `ubuntu-latest` | `linux-x64` |
| macOS ARM64 | `macos-26` | `osx-arm64` |
| macOS Intel x64 | `macos-26-intel` | `osx-x64` |

The [workflow at the new baseline](https://github.com/tkbss/CryptoWorkBench/blob/efa81d6ca282ad7a2b8959d0f406f741c87006b0/.github/workflows/dotnet.yml) uses PowerShell for commands, read-only contents permission and checkout without persisted credentials.

Each matrix job performs these declared operations. **[F: C09]**

1. Set up the SDK from `global.json`; restore the desktop project and both test-project roots. Their transitive project references cover all six repository projects.
2. Explicitly build CryptoScript, the Avalonia GUI and the desktop host in Release with `--no-restore`.
3. Build both test projects in Release, then run both complete suites with `--no-build --no-restore`. Write separate TRX files beneath `artifacts/test-results/`.
4. Publish the desktop host for the matrix RID using `--self-contained false -p:UseAppHost=true`. This is framework-dependent publication and requires a matching shared .NET runtime. RID-specific publish performs its own restore.
5. Validate the publish layout and documentation, then upload `test-results-<RID>` containing the TRX files. Missing test-result files are an error.

The second suite may run after a first-suite failure when test builds succeeded and the job was not cancelled. Publish may run after test failure when the desktop build succeeded; validation requires publish success. These diagnostics do not turn a failed test job into a successful job. **[F: C09]**

The [publish validator](https://github.com/tkbss/CryptoWorkBench/blob/efa81d6ca282ad7a2b8959d0f406f741c87006b0/.github/scripts/Validate-DesktopPublish.ps1) checks: **[F: C10]**

- Exact-case presence and non-empty content of the apphost, host DLL, dependency/runtime manifests, GUI DLL and Core DLL.
- Executable permission through `test -x` on Linux and macOS.
- A shared `Microsoft.NETCore.App` 10.0 runtime requirement and the expected RID in the dependency target.
- Presence of managed, native and satellite-resource assets enumerated in the dependency manifest, including at least one native asset.
- Every source InfoDocs file at its expected publish path, with exact casing, non-empty content, matching hash and successful text-file reading.

The new [Build and Test Dependency Coverage diagram](../diagrams/03-build-and-test-dependency-coverage.mmd) distinguishes CI execution/selection from direct project references. It omits the historical client under O05; Section 3.4 preserves the full graph. The two existing production edges are included only to explain coverage reached from CI roots.

**Observed master result. [F: V01–V03]** [Run 38710123715](https://github.com/tkbss/CryptoWorkBench/actions/runs/38710123715), Actions run number **91**, attempt **1**, was triggered by a push to `master` at exactly `efa81d6ca282ad7a2b8959d0f406f741c87006b0`. It completed with conclusion **success** on 2026-10-15. All four jobs reported successful production builds, both test suites, publish validation and test-result upload. Four non-expired test-result artifacts were listed when checked. This result was inspected independently of the owner-reported PR run #90.

**Historical finding resolved.** At `d3099c8`, CI selected the root Solution and GUI-test project and did not reach the desktop host. PR #1 adds an explicit desktop build and RID-specific publish validation in every matrix job. The implementation and observed master result resolve that missing-coverage finding. The [finding history](../evidence/03-project-structure-and-dependencies-baseline.md#finding-history-and-current-observations) retains the earlier evidence.

**Limits. [F/I: C07, C09, C10, V01–V03]** Tests include ViewModel and view integration code, but CI does not start the published graphical application. File/readability and permission checks do not prove window creation, native library loading during launch, user interaction, installer behavior or support on every OS version. Publish directories are not uploaded by this workflow; the retained artifacts are test results. No explicit coverage-collection option is configured despite the collector package. Artifact contents and individual TRX test counts were not inspected in this recovery.

ANTLR generated sources remain committed. Helper scripts invoke an external `antlr4` command without pinning its generator version; project and CI declarations do not regenerate the grammar. **[F: C11]**

## 3.7 Dependency Boundaries and Constraints

| Boundary | Verified observation | Interpretation or constraint |
| --- | --- | --- |
| Core independence | **[F: C04]** No Core reference to GUI, host or test projects | **[I]** Supports reuse independently of Avalonia at project level |
| GUI/Core interface | **[F: C06]** Direct parser/runner calls, variable access and mutation, static output-event subscriptions | **[I]** A broad source-level interface; not exclusively a narrow execution facade |
| Host/GUI boundary | **[F: C06]** Startup in host; Prism composition in GUI | **[I]** Separates platform startup from application composition |
| Core-internal organization | **[F: C02, C04]** Language, evaluation and cryptography share one project | **[I]** Directory boundaries do not enforce assembly-level separation |
| Test visibility | **[F: C07]** Core grants `InternalsVisibleTo("CryptoScriptUnitTest")` | **[I]** Intentional access to internal types for testing; not a reverse ProjectReference |
| Runtime environment | **[F: reused S06/S07]** Static variable/output state, standard-output calls and local help-file access | **[I]** Project independence does not imply complete host/environment independence |
| Automated verification | **[F: C09/C10, V01–V03]** Four successful build/test/publish paths | **[I]** Stronger build/distribution evidence while graphical startup remains untested |

**[F: C12]** No general project-layer enforcement was found in the inspected repository-owned build/test declarations. The existing `PinBlockArchitectureTests` concern mechanism contracts inside Core rather than the project-reference direction.

The remaining observations are: **[I/Q]** overlapping but incomplete Solutions; version-property ambiguity and duplicated test-package versions; an unpinned ANTLR generation path; and the retained historical dependency in the GUI-test graph. The GUI project's comment about removing diagnostics in Release has no matching condition on the visible PackageReferences; actual package/output behavior was not audited for that specific question. These observations do not establish a currently failing build.

**[Q]** Which Solution is intended as the comprehensive developer entry point? What package-version convention and ANTLR generation version are authoritative? What lifecycle is intended for the historical test dependency? Which OS/runtime versions and installation prerequisites are officially supported, and what graphical startup validation is required? The existing Chapter 2 questions about session isolation and the long-term Core interface remain open.

**[R]** Keep the complete repository inventory separate from the scoped product graph, preserve the resolved CI finding's history, and maintain the distinction between configured operations, observed CI success and verified graphical behavior. Decisions on package policy, test ownership and distribution support need explicit follow-up; this chapter introduces no implementation changes or GUI redesign.

[Previous: System Overview](02-system-overview.md) | [Next: Language Architecture](04-language-architecture.md) | [Return to architecture documentation](../README.md)
