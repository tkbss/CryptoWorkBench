# Project Structure and Dependencies: Baseline and Evidence

[Architecture documentation](../README.md) | [Chapter 3](../chapters/03-project-structure-and-dependencies.md)

## Analysis Baselines

| Item | Record |
| --- | --- |
| Chapter 3 implementation baseline | `efa81d6ca282ad7a2b8959d0f406f741c87006b0` |
| Earlier implementation baseline | `d3099c80af3891ad54444e544f528c9c5ed6b93f` |
| Documentation starting HEAD | `19c79fe39698c0d833b206abd600495d6eb508ef` |
| Branch and worktree | `docs/architecture-recovery`; `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Architecture` |
| Analysis/documentation date | 2026-10-15 |
| Method | Reuse E01/E02 and S01–S10; compare Git trees; inspect sources at the new commit; query GitHub run, job and artifact metadata |
| Owner authority | Current task and preserved Chapter 2 decisions O01–O05 |
| Local application execution | None: no restore, build, test, publish or GUI startup performed |
| External execution evidence | Existing master Actions run 38710123715, inspected independently of PR run #90 |

**[F]** The new implementation baseline differs from the earlier one in exactly two paths: modified `.github/workflows/dotnet.yml` and added `.github/scripts/Validate-DesktopPublish.ps1`. All application, test, project, Solution and package files are unchanged. The fetched PR record identifies PR #1 as merged at 2026-10-15T09:03:24Z with merge commit `efa81d6ca282ad7a2b8959d0f406f741c87006b0`.

The [Chapter 1 evidence](01-introduction-baseline.md) and [Chapter 2 evidence](02-system-overview-baseline.md) retain their original baselines, evidence identifiers and owner decisions. Their older CI statements describe their recorded baseline. C01–C12 below supplement them; they do not rewrite historical results.

## Baseline Incorporation and Preservation

**[F]** Initial inspection found branch `docs/architecture-recovery` at the expected documentation HEAD, no tracked changes and the known untracked `docs/architecture.zip`. No repository AGENTS.md was found in the inspected worktree or checked ancestor paths.

The documentation branch and updated master diverge from `d3099c8`. A merge or rebase would conflict with this task's prohibition on merges/commits. The non-destructive incorporation procedure was therefore:

1. Inspect status, branch and HEAD before writing.
2. Run `git fetch origin master` from the architecture worktree. This makes the supplied commit locally addressable and updates the remote-tracking ref without changing either checkout or the documentation branch.
3. Compare `d3099c8` with `efa81d6` and read the new baseline with `git show <commit>:<path>`.
4. Base Chapter 3 on the new commit; use immutable GitHub source links for the changed workflow and newly added validator. Reuse local links only for unchanged sources.
5. Limit authored changes to Chapter 3, its evidence and diagram, and architecture README navigation/metadata.

No checkout integration, merge, rebase, reset, stash, commit or push was performed. The branch HEAD remains `19c79fe39698c0d833b206abd600495d6eb508ef`; this is source-baseline incorporation, not a claim that the branch contains the new master in its ancestry. No unexpected conflicts were encountered because no history integration was attempted. The main development checkout was not used or modified.

The existing architecture chapters, evidence and three diagrams remain intact. `.codex/config.toml` was not read or modified. The known ZIP archive was not opened or modified. Fetch updates shared Git metadata; it does not update the main worktree's files or local branch.

To reproduce the source comparison:

```text
git diff --name-status d3099c80af3891ad54444e544f528c9c5ed6b93f efa81d6ca282ad7a2b8959d0f406f741c87006b0
git show efa81d6ca282ad7a2b8959d0f406f741c87006b0:.github/workflows/dotnet.yml
git show efa81d6ca282ad7a2b8959d0f406f741c87006b0:.github/scripts/Validate-DesktopPublish.ps1
```

## Evidence Classifications

**[F]** identifies verified source facts or separately identified CI observations. **[O]** identifies owner decisions/context, **[I]** derived interpretations, **[Q]** unanswered questions and **[R]** recommendations. The existing **[P]** planning convention remains applicable. Static declarations alone do not prove executed behavior; CI observations establish only the reported paths at the recorded commit.

## Repository Evidence

All C entries refer to `efa81d6ca282ad7a2b8959d0f406f741c87006b0` unless an earlier baseline is explicitly identified. Relative links resolve to unchanged checkout sources; line positions were checked against source declarations. CI sources use immutable commit links because the checkout intentionally retains the earlier implementation.

| ID | Sources and inspected positions | Supported facts |
| --- | --- | --- |
| C01 | Git tree comparison above; [PR #1](https://github.com/tkbss/CryptoWorkBench/pull/1); [PR API](https://api.github.com/repos/tkbss/CryptoWorkBench/pulls/1) | Only two CI paths changed; PR is merged and its merge SHA matches the requested baseline. Earlier project/source findings remain applicable. |
| C02 | `git ls-tree -r --name-only efa81d6ca282ad7a2b8959d0f406f741c87006b0`; `git ls-tree -r --name-only 19c79fe39698c0d833b206abd600495d6eb508ef -- docs/architecture`; directory inventory; reused S02, S04–S07 | The implementation baseline contains six root-level projects, two Solutions, examples and CI areas. Core and GUI subdirectories group responsibilities within their projects. The `docs/architecture/` tree belongs to the separate Architecture Recovery documentation branch `docs/architecture-recovery`, verified at documentation HEAD `19c79fe39698c0d833b206abd600495d6eb508ef`; it is absent from implementation baseline `efa81d6ca282ad7a2b8959d0f406f741c87006b0`. |
| C03 | [Root Solution, lines 6–10 and 13–29](../../../CryptoWorkBench.sln#L6); [Avalonia Solution, lines 6–22 and 25–49](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln#L6) | Complete membership matrix, both Debug/Release Any CPU configurations and build mappings, virtual ScriptBooks folder and four Solution Items. |
| C04 | [Core project](../../../CryptoScript/CryptoScript.csproj#L1); [GUI, lines 3 and 39](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj#L3); [Desktop, lines 3–17](../../../CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj#L3); [Core tests, lines 4–8 and 35](../../../CryptoScriptUnitTest/CryptoScriptUnitTest.csproj#L4); [GUI tests, lines 4–8 and 27–28](../../../CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj#L4); [historical inventory, lines 4–11](../../../CryptoWorkBenchConsole/CryptoWorkBenchConsole.csproj#L4) | All use Microsoft.NET.Sdk/net10.0; output declarations; both tests non-packable; all six direct ProjectReferences. |
| C05 | [Core packages, lines 18–20](../../../CryptoScript/CryptoScript.csproj#L18); [GUI packages, lines 22–35](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj#L22); [Desktop package, line 13](../../../CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj#L13); [Core-test packages, lines 20–30](../../../CryptoScriptUnitTest/CryptoScriptUnitTest.csproj#L20); [GUI-test packages, lines 12–22](../../../CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj#L12) | Direct package inventory and literal versions; repeated test-package versions; PrivateAssets on analyzers and collector. Historical project has no direct package declaration. |
| C06 | [Desktop startup, lines 13–21](../../../CryptoWorkBenchAvalonia.Desktop/Program.cs#L13); [GUI composition, lines 36–72](../../../CryptoWorkBenchAvalonia/App.axaml.cs#L36); [editor events and execution, lines 47–100](../../../CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs#L47); [variable mutation, lines 21–49](../../../CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs#L21); reused S02/S04/S06/S07 | Host startup is separate from GUI composition; GUI calls parser/runner, edits Core variables and subscribes to Core output events. |
| C07 | [Core friend assembly, line 3](../../../CryptoScript/AssemblyInfo.cs#L3); [historical test dependency, lines 34–41](../../../CryptoWorkBenchGuiTest/ConsoleSemanticDiagnosticTests.cs#L34); [view integration, lines 14–30](../../../CryptoWorkBenchGuiTest/InfoViewIntegrationTests.cs#L14); C04 | Internal Core visibility for Core tests; actual reflection use of historical assembly; GUI suite includes Views, not just ViewModels; no test-to-Desktop reference. |
| C08 | [Shared properties, lines 2–4](../../../Directory.Build.props#L2); [SDK policy, lines 2–5](../../../global.json#L2); C04/C05; [GUI language version, line 5](../../../CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj#L5); Core/Core-test NoWarn groups | Nullable policy, literal AvaloniaVersion 11.0.2, SDK 10.0.401/latestPatch/no prerelease, GUI latest language version and selected warning suppression. Tree/configuration searches found no repository-owned central package file, lockfile or NuGet configuration and no PackageReference version using AvaloniaVersion. |
| C09 | [Workflow, lines 3–41](https://github.com/tkbss/CryptoWorkBench/blob/efa81d6ca282ad7a2b8959d0f406f741c87006b0/.github/workflows/dotnet.yml#L3), [restore/build/test, lines 43–85](https://github.com/tkbss/CryptoWorkBench/blob/efa81d6ca282ad7a2b8959d0f406f741c87006b0/.github/workflows/dotnet.yml#L43), [publish/validate/artifacts, lines 87–111](https://github.com/tkbss/CryptoWorkBench/blob/efa81d6ca282ad7a2b8959d0f406f741c87006b0/.github/workflows/dotnet.yml#L87) | Four hosted-runner/RID pairs; explicit project roots; all three product builds; both complete Release test suites; TRX artifacts; conditional diagnostic paths; framework-dependent publishing. No graphical startup, publish-artifact upload or explicit coverage collection is declared. |
| C10 | [Validator, file checks and Unix permission, lines 14–45](https://github.com/tkbss/CryptoWorkBench/blob/efa81d6ca282ad7a2b8959d0f406f741c87006b0/.github/scripts/Validate-DesktopPublish.ps1#L14), [runtime/RID/assets, lines 47–79](https://github.com/tkbss/CryptoWorkBench/blob/efa81d6ca282ad7a2b8959d0f406f741c87006b0/.github/scripts/Validate-DesktopPublish.ps1#L47), [InfoDocs, lines 81–95](https://github.com/tkbss/CryptoWorkBench/blob/efa81d6ca282ad7a2b8959d0f406f741c87006b0/.github/scripts/Validate-DesktopPublish.ps1#L81) | Exact-case non-empty layout/assets, test -x, shared .NET 10 framework/RID, native asset presence, all source InfoDocs matched by hash and read as text. Script explicitly does not launch the graphical application. |
| C11 | [Generated parser, lines 4 and 11](../../../CryptoScript/Antlr/CryptoScriptParser.cs#L4); [generated lexer, line 4](../../../CryptoScript/Antlr/CryptoScriptLexer.cs#L4); [shell generator, lines 6–9](../../../CryptoScript/Antlr/compile-grammar.sh#L6); [batch generator, line 1](../../../CryptoScript/Antlr/CompileGrammar.bat#L1); C05/C09 | Generated files name ANTLR 4.13.2, runtime package is 4.13.1; external antlr4 invocation has no version pin. Inspected project/workflow declarations do not regenerate grammar. |
| C12 | [PinBlockArchitectureTests, lines 34–95](../../../CryptoScriptUnitTest/PinBlockArchitectureTests.cs#L34); C04/C08/C09 and bounded searches of repository-owned project, build and test declarations | Named architecture tests concern Core mechanism contracts; no general project-layer enforcement was found. No claim is made about uninspected third-party targets. |

## CI Observation Record

The following records were queried through the GitHub connector on 2026-10-15:

- [Run API](https://api.github.com/repos/tkbss/CryptoWorkBench/actions/runs/38710123715)
- [Job API](https://api.github.com/repos/tkbss/CryptoWorkBench/actions/runs/38710123715/jobs?per_page=100)
- [Artifact API](https://api.github.com/repos/tkbss/CryptoWorkBench/actions/runs/38710123715/artifacts?per_page=100)

### V01 — Master Run Identity and Result

| Field | Observed value |
| --- | --- |
| Run ID / Actions run number | 38710123715 / 91 |
| Attempt | 1 |
| Event / branch | push / master |
| Head SHA | `efa81d6ca282ad7a2b8959d0f406f741c87006b0` |
| Workflow | `.github/workflows/dotnet.yml`, name `.NET` |
| Status / conclusion | completed / success |
| Started | 2026-10-15T09:03:27Z — 11:03:27 Europe/Zurich |
| Last updated | 2026-10-15T09:05:42Z — 11:05:42 Europe/Zurich |
| Human-readable run | [Master run #91](https://github.com/tkbss/CryptoWorkBench/actions/runs/38710123715) |

**[O]** The task reports successful PR run #90. **[F]** The master result above was queried directly at the requested run ID and matching head SHA; it was not inferred from PR results. PR #90 job details were not used to establish master success.

### V02 — Four Matrix Jobs

| RID | Observed runner label | Job ID | Status / conclusion |
| --- | --- | --- | --- |
| win-x64 | windows-latest | [116339149981](https://github.com/tkbss/CryptoWorkBench/actions/runs/38710123715/job/116339149981) | completed / success |
| linux-x64 | ubuntu-latest | [116339150063](https://github.com/tkbss/CryptoWorkBench/actions/runs/38710123715/job/116339150063) | completed / success |
| osx-arm64 | macos-26 | [116339149402](https://github.com/tkbss/CryptoWorkBench/actions/runs/38710123715/job/116339149402) | completed / success |
| osx-x64 | macos-26-intel | [116339149710](https://github.com/tkbss/CryptoWorkBench/actions/runs/38710123715/job/116339149710) | completed / success |

**[F]** Every job's step metadata reports success for SDK setup, restore, all three production-build steps, test-project builds, both complete-suite test steps, publish, layout/documentation validation and artifact upload. These relevant steps were not skipped. Job metadata confirms the four configured runner labels; exact image revisions, installed package inventories and individual test assertions were not audited.

### V03 — Test-Result Artifacts

| Observed artifact name | Artifact ID | Expired when inspected |
| --- | --- | --- |
| test-results-win-x64 | 11959259996 | false |
| test-results-linux-x64 | 11959658919 | false |
| test-results-osx-arm64 | 11960685247 | false |
| test-results-osx-x64 | 11959938742 | false |

**[F]** All four artifacts are non-empty according to API byte sizes and associated with the baseline head SHA. Their configured contents are `artifacts/test-results/*.trx`. Artifacts were not downloaded; individual test counts, skips and TRX contents are not claimed. Successful upload is separately corroborated by V02.

## Finding History and Current Observations

| ID | Earlier/current evidence | Status and documentation consequence |
| --- | --- | --- |
| F03-01 — Desktop host omitted from CI coverage | Earlier read-only Chapter 3 analysis, reused S01/S10 and [old workflow, lines 23–30](https://github.com/tkbss/CryptoWorkBench/blob/d3099c80af3891ad54444e544f528c9c5ed6b93f/.github/workflows/dotnet.yml#L23): root Solution and separate GUI tests did not reach Desktop | **Resolved by PR #1** at efa81d6. C09 explicitly builds Desktop and publishes/validates each RID; V01/V02 confirm all four master jobs succeeded. Preserve earlier evidence rather than delete the finding. |
| F03-02 — Graphical startup not validated | C09/C10; no published app launch in the workflow | **Open limitation.** Publish/readability/permission success does not establish GUI startup, window rendering, interaction or installer compatibility. |
| F03-03 — Incomplete overlapping Solutions | C03: neither Solution contains all six projects or both test suites plus Desktop | **Current observation.** Explicit CI project roots now cover the repository graphs, so this is not a current missing-CI-coverage finding. Developer entry-point policy remains unclear. |
| F03-04 — Distributed version conventions | C05/C08: literal versions, separate AvaloniaVersion, repeated test-package versions; GUI Release comment without matching visible condition | **Current observation/question.** Maintenance ambiguity; no incompatibility or erroneous Release output is established. |
| F03-05 — ANTLR generation reproducibility | C11: generator header 4.13.2, runtime 4.13.1, external command without version pin or CI regeneration | **Current observation/question.** Successful CI exercises committed sources; it does not reproduce grammar generation or prove version incompatibility. |
| F03-06 — Historical test dependency retained | C04/C07 | **Current inventory fact.** Preserve full graph/table, exclude client from product diagrams under O05; no removal or redesign is proposed here. |
| F03-07 — Broad Core interface and environment assumptions | Reused S06/S07 plus C06: variable mutation, static state/events, local help and standard output | **Current interpretation.** Project-level independence from Avalonia does not imply complete host independence. Existing Chapter 2 questions remain applicable. |

## Diagram Evidence

### D03-COVERAGE

Source: [Build and Test Dependency Coverage](../diagrams/03-build-and-test-dependency-coverage.mmd).

- **[F]** Four runner/RID choices and CI selection/execution paths: C09. A matrix job is instantiated once per row.
- **[F]** Product references and the two in-scope test references: C04. Arrow direction is referencing project to referenced project.
- **[F]** Publish and validation responsibilities: C09/C10. TRX generation/upload: C09. The diagram models declared CI structure; V01–V03 separately establish the inspected run result.
- **[O]** Scope: O05. The historical client and its two associated reference edges are omitted; the complete six-edge graph remains in Chapter 3 Section 3.4.
- Legend: solid arrows mean direct ProjectReferences; dashed arrows mean CI execution, project selection or generated-output flow. They are not runtime messages. Dashed connections summarize paths and gates, not an unconditional linear schedule.
- Invisible links position the two panels and legend; they are not architectural relationships.
- The product chain is repeated only as context for CI coverage; the existing D01 remains the authoritative compact production-reference diagram.
- Explicit note: no graphical application startup is performed.
- No repository/Solution diagram is added; the membership table supplies that comparison without duplicating existing views.

### D01 Reuse

[01-project-references.mmd](../diagrams/01-project-references.mmd) remains unchanged with its original baseline and evidence. C01/C04 revalidate the same three project nodes and two edges at efa81d6. Existing D02 diagrams and evidence remain unchanged.

## Open Questions and Recommendations

| ID | Classification | Question / recommendation and supporting evidence |
| --- | --- | --- |
| Q03-01 | **[Q]** | Which Solution is the comprehensive developer entry point? C03 shows overlapping subsets; CI no longer depends on that decision. |
| Q03-02 | **[Q]** | What shared package-version convention governs Avalonia and repeated test packages? C05/C08. Imported target use of AvaloniaVersion remains unexamined. |
| Q03-03 | **[Q]** | Which ANTLR generator version and reproducible generation procedure are authoritative? C11. |
| Q03-04 | **[Q]** | What lifecycle is intended for the historical test dependency? C04/C07; product exclusion does not itself authorize test changes. |
| Q03-05 | **[Q]** | What OS/runtime support and installation prerequisites are promised, and what graphical startup validation is required? C09/C10 and V01–V03 do not settle that policy. |
| R03-01 | **[R]** | Retain separate repository and product graphs, classify findings explicitly, and distinguish configured CI, observed success and graphical runtime behavior. |
| R03-02 | **[R]** | Reuse D01; add only D03-COVERAGE and use the Solution membership table for repository organization. |

Existing Chapter 2 Q01 (session isolation), Q03 (Core interface) and Q04 (delegated hyperlink behavior) remain unchanged. Q02's help-distribution evidence is strengthened by the four successful publish validations, while official support/prerequisite policy and graphical execution remain unresolved.

## Verification Limits

No local application operation was executed, no dependency was changed, and no workflow was edited. Existing CI was observed, not dispatched or rerun. The GitHub metadata observations report conclusions and step outcomes; they are not a review of every log line, test assertion, security property or cryptographic standard. No GUI launch or installer execution was observed. Documentation verification is recorded below separately from application validation.

## Documentation Verification — 2026-10-15

- Compared the baseline trees and confirmed that only the workflow and publish validator differ from d3099c8. Rechecked six project files, two Solutions, six direct project references and the acyclic reference graph.
- Compared all 28 direct PackageReference declarations against the chapter's package table, and compared the Solution membership table with both source Solutions.
- Checked the four runner/RID pairs against the commit-addressed workflow and independently inspected the matching master run, all four jobs and four artifact records.
- Validated 199 Markdown links across the architecture Markdown/Mermaid sources: local target existence, exact path casing, referenced Markdown headings, and 78 source-line anchors. Commit-addressed GitHub source paths and line bounds were checked against local Git objects.
- Parsed and rendered all four Mermaid sources with Mermaid 11.12.0. Visually inspected D03-COVERAGE and corrected title overlap; CI paths and project references have distinct arrow styles and separate panels. No historical client is included as a diagram node.
- Rechecked Chapter 3's seven required headings, evidence identifiers and English text/labels. Temporary validation code and rendered previews were kept outside the repository.
- Confirmed that the seven existing chapter/evidence/diagram Markdown or Mermaid files remain identical to documentation HEAD. Git status/diff also confirm no changes to application code, tests, projects, Solutions, workflows, dependencies or the existing architecture ignore file.
- The only authored files are the new chapter, new evidence, new coverage diagram and updated architecture README. The known untracked archive remains present and untouched. No staged changes, commits, pushes, merges or local application execution were performed.

Authored file inventory:

| File | Change |
| --- | --- |
| `docs/architecture/chapters/03-project-structure-and-dependencies.md` | New complete Chapter 3 |
| `docs/architecture/evidence/03-project-structure-and-dependencies-baseline.md` | New baseline, source evidence, observed CI result, finding history and verification record |
| `docs/architecture/diagrams/03-build-and-test-dependency-coverage.mmd` | New scoped build/test coverage view |
| `docs/architecture/README.md` | Updated navigation, chapter status, per-chapter baselines and source-baseline incorporation note |

Status: documentation draft complete and ready for independent review.
