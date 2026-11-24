# AR-01/F1 Correction Report

Date: 2026-11-10. All three confirmed R1 findings have been corrected. The required validation gates pass; this package is ready for R2 rereview, not final owner acceptance.

## Baseline and scope

Worktree: [AR-01 worktree](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/). Branch: codex/ar-01-baseline-reconciliation. Integration HEAD remains 3c6c8e9292a1e091bbbd3c571c08d46746ce3218, with parents abbf7b7b2f22f1bbc06c5c8b28702831099e097f and fbbadae2f68b015ccedfea92ce8bba4cd7737ecb. Production claims below use the latter immutable baseline.

The complete [original R1 report](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-r1/AR-01-R1-independent-review.md) was read before editing. Its complete evidence directory remains unchanged. F1 changes are limited to the three confirmed findings, their regression/evidence support and required reports/inventory. The shared bilingual publication design, English default paths, chapter selection, partial Chapter 9 and historical material are preserved. No nonblocking R1 recommendation was implemented.

## Corrections and source-level verification

### F1-01 / F-R1-01: restored Chapter 4 constraints

[English §4.6](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/chapters/04-language-architecture.md:62) and [German §4.6](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/de/chapters/04-language-architecture.md:62) now explain that redeclaration replaces an existing identifier entry through VariableDictionary.Add, validates against its own TypeName, and differs from assignment's existing-target/type-preservation/prepared-update semantics. This does not promise assignment's stronger failure guarantees for redeclaration.

Evidence: [VariableDictionary.Add](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/CryptoScript/Variables/VariableDictionary.cs:26) removes an existing entry before storing the declaration. [VariableDeclarationEvaluator](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/CryptoScript/Model/VariableDeclarationEvaluator.cs:111) registers parameter/function results (also line 127) and delegates literal registration through [Expression.BuildVariable](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/CryptoScript/Expressions/Expression.cs:20). [AssignmentEvaluator](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/CryptoScript/Model/AssignmentEvaluator.cs:14) requires an existing target; lines 100–103 retain its type and publish through Update.

[English §4.9](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/chapters/04-language-architecture.md:106) and [German §4.9](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/de/chapters/04-language-architecture.md:105) now document ASCII ScriptBook serialization, possible alteration of non-ASCII strings/path text, and the implemented loading path. [WorkbenchSaveCommit.WriteSnapshot](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs:167) skips blank/whitespace-only lines, calls Encoding.ASCII.GetBytes at line 173 and writes a newline. [CryptoScriptEditViewModel](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:319) uses File.ReadAllLinesAsync(filePath, pipeline.ExecutionToken) without an explicit encoding argument and passes the lines into ExecuteBookAdmittedAsync. The text explicitly states that loading does not restore characters lost during saving.

R05 now includes WorkbenchSaveCommit with its pinned Git blob SHA-256, line count and immutable URL, and both localized evidence appendices include that source. Existing declaration, assignment, dictionary and loader references remain. The register now contains 110 source objects under the existing R01–R16 identities, plus the unchanged five absent-path checks.

Two [focused retention regressions](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/tools/bilingual.test.mjs:48) verify the restored explanations in the current English/German §4.6 and §4.9, including their evidence references and save/load identifiers. Each also removes the redeclaration paragraph and the persistence paragraph in memory and confirms that the check fails. This catches the omission that bilingual parity alone could miss. These tests do not claim to establish semantic truth or production behavior.

### F1-02 / F-R1-02: syntax-rejection execution flow

Both [English](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/diagrams/04-source-to-operation-sequence.mmd) and [German](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/de/diagrams/04-source-to-operation-sequence.mmd) diagrams now route SyntaxError, explicitly without runner execution, into FinalizePresentation and attempted variable-state capture. Separate terminal nodes show CaptureFailed=false with variables or CaptureFailed=true with the same script outcome and stale display. Chapter 4 §4.8 step 2 explains that SyntaxError remains the original outcome even if capture fails.

The graph follows [WorkbenchExecutionPipeline](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs:72): syntax rejection returns through FinalizePresentation before the runner is created; [capture and failure handling](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs:153) attempts capture once and sets CaptureFailed separately. The presentation-status property at line 22 supplies the stale-display message. Source and rendered topology parity pass. Both corrected diagrams appear on PDF page 26.

### F1-03 / F-R1-03: certificate-pinning terminology

[German Chapter 2 §2.3](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/de/chapters/02-system-overview.md:32) now uses “Proxy mit Zertifikat-Pinning”. The [terminology decision](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/decisions/ar-01-bilingual-publication.md:27) uses the same wording and briefly identifies the certificate fingerprint comparison. [PinnedMcpStdio](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/CryptoWorkBench.Mcp/PinnedMcpStdio.cs:40) parses the configured pin; line 53 compares it with SHA256.HashData(peer.RawData) using FixedTimeEquals. English content remains unchanged. Detailed MCP security analysis remains outside F1.

## Exact authored-file inventory

The F1 delta is **13 authored files: 12 existing files updated and one new correction report**. The complete uncommitted A1+F1 package is **58 files: 20 modified tracked files and 38 untracked files**. Nothing is staged. The [machine-readable inventory](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/AR-01-changed-files.json) lists every package path/hash and a separate F1 before/after delta; its own hash is recorded in preservation evidence rather than recursively inside itself.

| File | F1 change |
| --- | --- |
| [chapters/04-language-architecture.md](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/chapters/04-language-architecture.md) | Restore redeclaration and ASCII save/load constraints; clarify syntax-rejection finalization. |
| [de/chapters/04-language-architecture.md](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/de/chapters/04-language-architecture.md) | Equivalent German corrections with the same numbering and references. |
| [diagrams/04-source-to-operation-sequence.mmd](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/diagrams/04-source-to-operation-sequence.mmd) | Continue rejection into capture; distinguish capture success and failure. |
| [de/diagrams/04-source-to-operation-sequence.mmd](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/de/diagrams/04-source-to-operation-sequence.mmd) | Localize the same corrected graph. |
| [de/chapters/02-system-overview.md](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/de/chapters/02-system-overview.md) | Use Proxy mit Zertifikat-Pinning. |
| [decisions/ar-01-bilingual-publication.md](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/decisions/ar-01-bilingual-publication.md) | Correct pinning terminology; reflect the additional source object. |
| [evidence/ar-01-source-register.json](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/evidence/ar-01-source-register.json) | Add immutable WorkbenchSaveCommit source identity to R05. |
| [evidence/ar-01-baseline-reconciliation.md](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/evidence/ar-01-baseline-reconciliation.md) | Add save-path evidence and clarify R05 coverage. |
| [de/evidence/ar-01-baseline-reconciliation.md](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/de/evidence/ar-01-baseline-reconciliation.md) | Equivalent German R05 evidence entry. |
| [tools/bilingual.test.mjs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/tools/bilingual.test.mjs) | Add two focused retention tests, including deletion-detection checks. |
| [AR-01-publication-validation.md](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/AR-01-publication-validation.md) | Record F1 results while retaining the complete original A1 report below them. |
| [AR-01-changed-files.json](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/AR-01-changed-files.json) | Refresh full-package hashes and record the exact F1 delta. |
| [AR-01-F1-correction-report.md](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/AR-01-F1-correction-report.md) | New correction and handoff report. |

Both PDFs and the existing eleven inventoried generated artifacts were refreshed under ignored generated output paths. The inventory records their exact names and hashes. The pre-F1 authored/generated bytes were saved under [F1 before snapshot](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/before); original R1 artifacts were never used as an output destination. F1-only scripts, logs and page renders are under [F1 evidence directory](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/) and are not additional authored repository sources.

## Validation results

All **24 publication regression tests pass**, with zero failures, cancellations or skips: the prior 22 tests plus two retention tests. The separate focused run passes 2/2. Source validation, four-chapter/seven-diagram parity, standalone EN/DE validation and two generations per language all pass. Markdown safety, failure cleanup, output isolation and source-integrity tests remain intact.

| Check | English | German |
| --- | ---: | ---: |
| Pages | 36 | 36 |
| Portrait / landscape pages | 30 / 6 | 30 / 6 |
| Chapters / diagrams | 4 / 7 | 4 / 7 |
| Bookmarks | 68 | 68 |
| Internal / external link annotations | 215 / 157 | 215 / 150 |
| Source blocks checked / omitted | 462 / 0 | 462 / 0 |
| Source/local links checked | 267 | 267 |
| Immutable source objects checked | 110 | 110 |
| Minimum diagram label size, all diagrams (pt) | 9.54 | 8.89 |
| Corrected execution-flow label minimum (pt) | 14.22 | 12.86 |
| Two-run semantic/layout repeatability | PASS | PASS |
| Byte identity within repeated pair | true | false |

The prior A1 counts were 35/36 pages and 459 source blocks. F1 adds two chapter paragraphs and one evidence-source entry per edition, giving 462 blocks. English grows to 36 pages; German remains at 36. Bookmark totals stay at 68. These values were measured anew, not assumed. All selected blocks are present; zero omissions, text-outside-page errors or invalid destinations were reported. pypdf independently agrees on page counts, bookmarks, link annotation counts and language tags.

Windows; Node v24.22.1; pnpm 11.25.0; locked Playwright 1.64.0; existing Chromium 156.0.8078.4 (revision 1248). SOURCE_DATE_EPOCH=1794268800 fixes the publication timestamp to 2026-11-10T00:00:00Z. No dependency, lockfile, publication-manifest or generator implementation changed.

Repeatability compares input/tool hashes, baseline references, page/heading layout, links, source sections/blocks and diagram records, plus rendered bilingual topology. The English pair is byte-identical; the German pair is not. F1 claims the demonstrated semantic/layout repeatability, not byte identity or portability across different environments. It does not newly attribute the German binary difference to a specific PDF object.

Final PDF SHA-256:

- EN: b50722417fc9628e33d11af4cf66485e8dfc66d28d04c0672aac0e2cba9513d0
- DE: c66f76ba61931a97c03805b84d6c18cce6f0f8ba9230ed908816a35236fa5ad4

Evidence: [source parity](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/source-parity.json); [retention-only run](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/retention-results.txt); [full regression run](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/regression-results.txt); [standalone EN validation](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/standalone-en.txt); [standalone DE validation](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/standalone-de.txt); [repeatability run](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/repeatability-run.txt); [independent PDF inspection](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/pdf-inspection.json); [preservation checks](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/preservation.json). The generated manifests/validation reports remain at their established language-specific paths.

### Visual inspection

Fresh Poppler renders were inspected at individual-page resolution in both editions: pages 1–3 (cover and contents), 9 (Chapter 2), 20–23 (restored constraints, assignment context, code and syntax rejection), and 26 (corrected diagram). The restored English persistence paragraph is on page 22; its German counterpart is on page 23. The German redeclaration paragraph continues from page 20 to page 21 without content loss. No clipping, missing glyphs, text overlap or unreadable corrected labels was observed. Umlauts and ß were checked visually and through pypdf extraction. PDF catalog languages are en/de. This is focused F1 visual coverage, not a fresh full-page review of every unchanged page.

### Resolved execution issues

Two execution issues were resolved without source/toolchain changes. The sandbox's default Playwright cache was absent; the existing revision-1248 browser was located in the application's LocalCache and selected with PLAYWRIGHT_BROWSERS_PATH. The initial full-suite run overlapped standalone validation, which temporarily removes its success report; 11 fixture-copy checks therefore failed before their assertions. After standalone validation completed, a sequential rerun passed all 24 tests. The initial log is retained as [first-attempt evidence](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/regression-first-attempt.txt). Generation, regression and standalone validation should be sequenced when sharing these output paths.

## Preservation and remaining limits

Final checks confirm every tracked file outside docs/architecture matches fbbadae2f68b015ccedfea92ce8bba4cd7737ecb, HEAD and all branch tips are unchanged since F1 started, and the index is empty. All 12 archived originals match abbf7b7b Git blobs. All 87 designated preserved tracked paths, including partial Chapter 9 and historical evidence/reports, remain byte-identical to the pre-F1 state. All 276 original R1 files/symlinks match the pre-F1 snapshot. No writes were made to GUI, Prism, master, the original documentation worktree or the assignment worktree. Read-only worktree observations are included in the refreshed inventory; they are observations, not a claim that unrelated active workstreams cannot change independently.

The shared generator/validator, publication selection, styles, dependencies and safety infrastructure remain unchanged; only the bilingual regression file gains focused tests. The original A1 publication report is retained verbatim after the current F1 results in the refreshed publication report, and the original integration report is unchanged. No commit, push, merge, rebase or branch switch was performed.

No confirmed F1 finding or required validation gate remains open. Production behavior was checked statically and left unchanged; no fresh application tests, cross-platform certification, cryptographic certification or detailed MCP security analysis is claimed. The retention checks and structural parity do not replace independent technical/German-language review. Nonblocking R1 recommendations remain deferred as requested. R2 independent rereview and final owner acceptance are separate steps.

READY FOR R2 INDEPENDENT REREVIEW
