# AR-01 bilingual publication and validation report: F1 update

Date: 2026-11-10. Status: READY FOR R2 INDEPENDENT REREVIEW. Final owner acceptance is not claimed.

The F1 corrections resolve the three confirmed R1 findings: current Chapter 4 redeclaration/ASCII persistence constraints in both languages; syntax rejection followed by presentation finalization and attempted state capture, with separate capture failure; and accurate German certificate-pinning terminology. The approved bilingual design and shared generator remain unchanged. See the [complete F1 correction report](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/AR-01-F1-correction-report.md) and [refreshed exact inventory](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/docs/architecture/AR-01-changed-files.json).

## Current F1 validation results

All 24 publication tests pass, including the two new focused retention checks; the separate retention-only run passes 2/2. Both standalone PDF validators pass, both languages pass two-run semantic/layout repeatability, and source plus rendered diagram parity pass. All 110 source objects and five absent historical paths were checked against the immutable baseline. Each language resolves 267 source/local links.

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

No selected source blocks are omitted. Bookmark/destination, annotation, input/tool hash, text-boundary and diagram-layout checks pass. Fresh pypdf inspection confirms counts and language tags. The English edition has one more page than A1; German's page total is unchanged. Source blocks increased by three in each edition. Unequal annotation totals are reported as observed; actual link targets are validated independently.

Windows; Node v24.22.1; pnpm 11.25.0; locked Playwright 1.64.0; existing Chromium 156.0.8078.4 (revision 1248). SOURCE_DATE_EPOCH=1794268800 fixes the publication timestamp to 2026-11-10T00:00:00Z. No dependency, lockfile, publication-manifest or generator implementation changed.

English repeated bytes are identical. German repeated bytes differ; semantic/layout and rendered-topology comparisons pass. Binary identity is not claimed for German or for other environments.

Fresh Poppler renders were inspected at individual-page resolution in both editions: pages 1–3 (cover and contents), 9 (Chapter 2), 20–23 (restored constraints, assignment context, code and syntax rejection), and 26 (corrected diagram). The restored English persistence paragraph is on page 22; its German counterpart is on page 23. The German redeclaration paragraph continues from page 20 to page 21 without content loss. No clipping, missing glyphs, text overlap or unreadable corrected labels was observed. Umlauts and ß were checked visually and through pypdf extraction. PDF catalog languages are en/de. This is focused F1 visual coverage, not a fresh full-page review of every unchanged page.

Final PDF SHA-256:

- EN: b50722417fc9628e33d11af4cf66485e8dfc66d28d04c0672aac0e2cba9513d0
- DE: c66f76ba61931a97c03805b84d6c18cce6f0f8ba9230ed908816a35236fa5ad4

Evidence: [source parity](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/source-parity.json); [retention-only run](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/retention-results.txt); [full regression run](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/regression-results.txt); [standalone EN validation](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/standalone-en.txt); [standalone DE validation](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/standalone-de.txt); [repeatability run](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/repeatability-run.txt); [independent PDF inspection](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/pdf-inspection.json); [preservation checks](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/preservation.json). Current summaries are also copied to the established generated/source-validation.json, generated/regression-results.txt and generated/standalone-en.txt / standalone-de.txt locations.

Two execution issues were resolved without source/toolchain changes. The sandbox's default Playwright cache was absent; the existing revision-1248 browser was located in the application's LocalCache and selected with PLAYWRIGHT_BROWSERS_PATH. The initial full-suite run overlapped standalone validation, which temporarily removes its success report; 11 fixture-copy checks therefore failed before their assertions. After standalone validation completed, a sequential rerun passed all 24 tests. The initial log is retained as [first-attempt evidence](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01/artifacts/ar-01-f1/regression-first-attempt.txt). Generation, regression and standalone validation should be sequenced when sharing these output paths.

Production preservation, unchanged Git history/branch tips, 12 exact historical copies, 87 preserved paths and 276 unchanged R1 files/symlinks are checked in the F1 preservation evidence. No unrelated R1 recommendation was implemented. No production test, remote-URL availability or final human acceptance is implied by these publication results.

## Preserved A1 report (2026-11-06)

The following original report is retained verbatim as historical evidence. Its status, counts, hashes and validation observations describe A1, not the corrected F1 outputs above.

---

# AR-01 bilingual publication and validation report

Date: 2026-11-06. Status: READY FOR INDEPENDENT REVIEW; independent human acceptance pending.

## Delivered publication

English keeps the original chapter and diagram paths. German has complete corresponding sources under `de/`, with the same section numbering, code examples, evidence identities and graph structure. Both editions publish Chapters 1–4, seven diagrams, a current-baseline evidence appendix and the documentation entry point. Partial Chapter 9 remains preserved and excluded. The shared `publication.json` and one generator/validator select the edition; English remains the default.

The English reconciliation covers removed Console/RSA placeholders, the current project and test graph, MCP/automation boundaries, Avalonia 12 and prepared Prism builds, assignment syntax/AST/mapping/evaluation, structured TR-31 headers, typed KEY construction and host/runtime execution boundaries. Current claims use R01–R16 against `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`; historical approvals and test results are explicitly historical. Detailed MCP analysis remains AR-MCP-01. Unfinished GUI redesign is outside scope.

## Validation method

- Current source validation reads pinned Git objects, verifies all 109 registered source digests/line counts and five removed paths, and resolves each edition's source/local links and fragments. HTTP availability is not asserted; immutable Git object/path identity is checked locally.
- Bilingual validation checks chapter/section identities and levels, examples, evidence references, link sequences, paragraph/list/table counts, Mermaid graph source structure and rendered node/edge/boundary topology. It cannot prove semantic equivalence of translations.
- Publication regression tests retain the original thirteen safety, content-integrity and failure tests and add nine language/selection/parity/source-integrity/output-isolation tests.
- Generation and standalone PDF validation check current input/tool digests, ordered source-block completeness including duplicate occurrences, bookmark hierarchy/destinations, internal/external annotations, page sizes, text bounds, overflow, diagram labels and minimum size. Markdown HTML/executable-link restrictions remain in force.
- Each language is generated twice with `SOURCE_DATE_EPOCH=1793923200`, locked dependencies and the installed Playwright Chromium. Repeatability compares source/tool digests, baselines, pages/headings, links, source sections/blocks and graph layout. PDF byte identity is reported separately rather than claimed as a portability guarantee.
- PDF pages are rendered with Poppler for visual inspection, including contents/navigation, diagrams, wrapping and German characters. PDF catalog language and extraction of umlauts/ß are independently checked with pypdf.

## Findings and corrections during verification

Visual inspection identified a nearly empty extra contents page, overlap between a container boundary title and nearby diagram content, and a candidate layout in which the external proxy straddled the desktop frame. Contents spacing and diagram layout were corrected. The HTTPS endpoint label is now in its host node, the existing legend names the frame, and candidate selection rejects external-node/frame overlap, title/node/edge-label overlap and edge/title crossings. A wider candidate was rejected by the German minimum-font-size gate. Final full-size diagrams show the proxy outside the desktop frame and all text clear of conflicting content. These findings demonstrate why text-presence checks alone are insufficient for visual acceptance.

The first full regression run passed 21 of 22 tests. A nested Windows fixture triggered Git's ambiguous revision/path probing at an overlong pathname before the duplicate-occurrence assertion. Source lookup now resolves the actual Git worktree root and supplies the revision/path separator. This changes validation plumbing, not production code or source facts.

## Final results

All **22 publication regression tests pass**: 13 retained tests and 9 bilingual tests. Both standalone validators pass against the final on-disk sources/tooling/PDFs. Source/parity checks and rendered topology parity pass. No automated acceptance gate is outstanding.

| Check | English | German |
| --- | ---: | ---: |
| A4 pages | 35 | 36 |
| Portrait / landscape pages | 29 / 6 | 30 / 6 |
| Chapters / diagrams | 4 / 7 | 4 / 7 |
| PDF bookmarks | 68 | 68 |
| Internal link annotations | 212 | 212 |
| External link annotations | 154 | 156 |
| Checked source blocks | 459 | 459 |
| Omitted source blocks | 0 | 0 |
| Minimum diagram label size (pt) | 9.54 | 8.89 |
| Source/local links validated | 263 | 263 |
| Registered source objects checked | 109 | 109 |
| Repeated semantic/layout generation | PASS | PASS |
| Byte-identical PDFs in final repeated pair | false | false |

Zero detected text-outside-page errors, source-block omissions, invalid destinations, split diagram words, or diagram/node/label collisions. Full final PDFs were rendered and inspected, with a separate full-size inspection of the corrected container diagram. German umlauts, capitals and ß extract correctly and are visibly rendered; PDF catalog languages are `en` and `de`. Seven paired diagrams retain matching rendered graph identities and relationships.

Final PDF SHA-256:

- English: `99343ab2b175c29a192d2e0747a023fad1b5570557e2b70eac56ed474adc0985`.
- German: `c4f434e6590b1010872744995ef3a95eea0be2ae82f24a18a2b7a412272cfbb8`.

Repeatability is semantic/layout reproducibility with exact input and tool digests; binary serialization identity is recorded above for this final pair and is not guaranteed on another platform. The environment is Windows, Node 24.22.1, pnpm 11.25.0 and locked Playwright 1.64.0/Chromium v1248. No dependency version or lockfile change was needed.

Machine-readable evidence: `generated/source-validation.json`, `generated/repeatability.json`, both `generation-manifest.json` and `validation.json` files, `generated/regression-results.txt`, and `generated/standalone-en.txt` / `standalone-de.txt`. Visual contact sheets/page PNGs and extracted language/character observations remain ignored QA intermediates.

Re-run using the commands in [tools/README.md](tools/README.md). Generated PDFs and QA files remain ignored under the existing policy; authoritative sources/tooling and reports are the reviewable changes.

## Preservation and repository status

The isolated integration and exact baselines are recorded in [AR-01-integration-report.md](AR-01-integration-report.md). [AR-01-changed-files.json](AR-01-changed-files.json) inventories every authored tracked/untracked path, repository status, unchanged branch tips and generated artifacts. All authored paths are inside `docs/architecture`; the index remains empty. Every tracked file outside that directory matches the pinned implementation baseline.

The final audit identifies 57 authored changed/new files, including this report and the inventory itself. It verifies 87 preserved tracked historical evidence/report/Chapter 9/review-metadata paths, twelve exact archived originals, unchanged original branch tips, and a clean main worktree. Other development worktrees have their own active changes; the inventory records read-only status snapshots without claiming those externally changing work areas stayed clean or unchanged during this task.

Original README, Chapters 1–4 and seven diagrams are retained as twelve exact historical files under `evidence/history/abbf7b7b`. Original evidence, partial Chapter 9, imported implementation reports and review metadata are retained. The earlier generator review history remains below the current addendum in `tools/VALIDATION.md`. No authored documentation/tooling commit, push, merge into master or update to the original architecture branch has occurred.

## Remaining uncertainties and review limits

The authoritative ANTLR generation environment, intended public-contract status of preserved language edge cases, and long-term isolation/platform-support policies still require owner decisions. This package records what source implements; it does not resolve those policies. Historical GUI/build evidence is not a fresh application test result. Production application tests/builds were not rerun for this documentation-only package.

The complete German translation has structural and visual checks and an explicit terminology decision record, but no independent fluent human technical/linguistic review. Both editions remain review drafts; former chapter approval does not transfer automatically. Review should concentrate on assignment's target-only atomicity, interpreter/host ownership boundaries, contextual argument identity and translated terminology.

Validation was performed on Windows with the recorded Node/dependency/Chromium environment. Cross-platform PDF byte identity, PDF/UA accessibility conformance and live availability of external web links are not claimed. Different EN/DE page counts and wrapped-link annotation counts do not imply omitted content.

All automated acceptance gates pass. There is no blocked implementation work; independent human technical and linguistic review remains the separate acceptance step required by AR-01/A1.

**READY FOR INDEPENDENT REVIEW**
