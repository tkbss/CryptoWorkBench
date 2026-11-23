# AR-01 bilingual validation — 2026-11-06

Current results and limitations are recorded in [the AR-01 publication report](../AR-01-publication-validation.md). The current edition uses the pinned implementation `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` and publishes Chapters 1–4 in English and German. Each edition has its own generated PDF, manifest and validation result. The shared test suite contains the thirteen retained regression tests and nine bilingual tests. Source, parity, repeatability and visual checks are documented in that report; independent technical and linguistic acceptance remains pending.

The historical review/ZIP records below remain unchanged. Their page counts, baseline, package contents and test results apply to their original inputs, not to the AR-01 edition. Current authored sources are uncommitted for review; their manifests record exact input and tooling hashes.

# Updated technical review ZIP

Refreshed on **2026-10-15T16:30:05.866925+00:00** directly from the actual Architecture Recovery worktree
`C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Architecture`. Branch: `docs/architecture-recovery`;
HEAD: `523bf99751fe56aaff8ad4687d9f9de2321d811a`. No commit, push or merge.

Archive: `docs/architecture/generated/CryptoScript-Architecture-PDF-Generator-Review.zip`.
It contains **26 actual worktree files plus `REVIEW-PACKAGE.json`**: all thirteen
authored tooling files (including `markdown.mjs`, `content-checks.mjs` and
`regression.test.mjs`), all eleven authoritative Markdown/Mermaid inputs, the
architecture `.gitignore`, and the final validated 51-page PDF. Package/lockfile,
styles, configuration, installation/use documentation, this validation history
and unchanged `REVIEW-METADATA.md` are included. Repository-relative paths are
preserved. No dependency directories, caches, temporary fixtures, HTML/SVG/PNG
intermediates or older PDFs are included.

Standalone current-source validation passed again before packaging: 51 A4 pages,
63 bookmarks, 152 internal/186 external annotations and 1,127 checked source
blocks with zero detected omissions. The earlier two-generation verification
and 13 passing regression tests remain applicable; generator runtime, tests,
dependency pins, styles and architecture inputs have not changed since those
checks. Only this packaging addendum and the tooling README's package status
were updated for the ZIP refresh. PDF SHA-256:
`d762c12b50dbe46db5b963d81aa6140f474d384252ced601e22e6a9d8a8603c7`.

Every archived worktree file is compared byte-for-byte and by SHA-256 against
its live original after writing. ZIP CRC and complete/unique inventory checks
pass. `REVIEW-PACKAGE.json` records the worktree, branch, HEAD, working-tree
status, per-file sizes/hashes, PDF validation and repeatability evidence. Source
files are read directly from this worktree, never from temporary review copies.
The prior review ZIP is retained in `generated/technical-review-before/`.

The reports below are preserved as stage-specific history. Their statements
that the earlier ZIP had not yet been refreshed are superseded by this addendum.
**Stop for final independent review.**

---

# PDF generator technical review corrections

Validated on **2026-10-15**, in the actual Architecture Recovery worktree
`C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Architecture`. Branch: `docs/architecture-recovery`.
HEAD remains `523bf99751fe56aaff8ad4687d9f9de2321d811a`.
The 51-page layout, diagram presentation and review metadata remain approved.
This revision addresses technical findings M1/M2/L1 and is submitted for final
independent technical review. No commit, push or merge was performed.

## Corrections

| Technical finding | Correction | Negative verification |
| --- | --- | --- |
| M1: unrestricted raw HTML | Shared Markdown factory uses `html: false`, retaining Markdown-it executable-link validation and the generator's existing link policy. Raw HTML becomes visible literal text; no HTML allowlist is needed by existing sources. | Real Chromium tests cover raw scripts, image/SVG event handlers, raw anchors with executable/data/entity-encoded URLs, and dangerous Markdown links. No injected elements, links or script execution. Safe Markdown formatting, tables, fenced code and ordinary links still render. |
| M2: stale publication after failure | Delete the previous PDF, generation manifest and pass report before optional dependency imports, Git metadata or input parsing. An outer failure handler removes all three again, including post-write failures. Diagnostic HTML/SVG output may remain and is not a publication. | Disposable fixture changes an input, adds a duplicate chapter and runs the real generation CLI: command fails, all publication files are absent, and standalone validation cannot pass. Separate dependency-load and post-publication failure tests pass; the late fault occurs after new PDF/manifest and a simulated pass report exist. |
| M2: independent validation | Standalone validation checks current source inventory, SHA-256 and byte lengths before accepting the manifest/PDF. Added, removed or changed inputs fail. Clear the prior pass report before checks. Report explicitly records `sourceValidation: current`. | Changed README bytes fail through the actual validation CLI and remove its old pass report. Added/removed source inventory tests also fail as expected. Older manifests lacking source-section accounting require regeneration; there is no unchecked snapshot mode. |
| L1: repeated block accounting | Keep all existing substring checks and add source-scoped non-overlapping occurrence counts at prepared HTML, paginated HTML and PDF stages. Require prose/code blocks in source order. PDF ranges exclude the cover, contents, diagrams, appendix introductions and neighboring sources. Table-cell multiplicity is checked without imposing an order on the approved finding-record presentation. | Missing identical prose occurrences fail at each of the three stages. Repeated table cells, overlapping matches, prose reordering and borrowing text from another source fail. The real PDF validation CLI also rejects a manifest expecting a second identical block that the PDF lacks. |

The generator uses explicit publication invalidation, rather than retaining the
last successful PDF at the canonical output path. After any failed run, regenerate
before distributing that output. Dependency imports occur inside the cleanup
boundary. No application build, test or GitHub Actions change is involved.

## Commands and results

From the repository root:

```sh
node docs/architecture/tools/generate.mjs
node --test docs/architecture/tools/regression.test.mjs
node docs/architecture/tools/generate.mjs
node docs/architecture/tools/validate.mjs
```

Both final generation executions and independent current-source validation passed.
The regression suite reports **13 tests passed, zero failed/cancelled/skipped**
(20.70 seconds in the final run). Fixture documents are disposable copies beneath
ignored `generated/`; actual chapter/evidence/diagram files were never edited.
All temporary regression fixtures were removed. The first test run exposed an
overly literal assertion about HTML entity display: Markdown correctly decoded
the entity into inert displayed text. The expectation was corrected; the active
element/script checks were retained. All final regression checks pass.

| Final PDF verification | Result |
| --- | --- |
| Physical sheets | **51 A4: 48 portrait, 3 landscape** |
| Chapters and source completeness | Chapters 1–3 complete, all three evidence records, complete README and four diagrams; all 11 authoritative hashes unchanged from the approved edition |
| Source-block accounting | **1,127 blocks**, **1,029 source/text groups**; zero detected omissions; source-scoped multiplicity and prose/code order pass at all three stages |
| Diagrams | Four complete vector diagrams, pages **8, 15, 16, 25**, zero detail tiles, zero raster image XObjects on diagram pages |
| Graph preservation | Same nodes, edges, boundaries, labels, directions, styles, renderer choices, viewBoxes, minimum label sizes and orientations as the approved edition |
| Bookmarks | All **63** hierarchical bookmarks resolve to the expected heading/page |
| Navigation | **152 internal** and **186 external** link annotations; TOC page numbers, target coordinates and source targets validate |
| Source links | Immutable baseline-addressed repository paths verified against local Git objects; external reachability/account access is not exercised |
| Tables/findings | Zero inappropriate mid-word breaks or page overflow; 14 repeated continuation headers; approved presentation retained |
| Review metadata | `Reviewed` retained and checked for Chapters 1/2/3 on pages 4/9/17 and README on pages 48–49 |
| Visual inspection | All 51 pages rendered by Poppler and surveyed; all four diagram pages individually inspected; finding/table pages 42/50 additionally inspected at full-page resolution |

Final generations: `2026-10-15T16:20:14.311Z` and
`2026-10-15T16:22:06.980Z`. Source/tool hashes, baselines, headings/pages,
link records, source sections/blocks and complete diagram graph/rendering fields
match across runs. The same fields for layout/content also match the approved
pre-correction edition. Generation timestamps and PDF/SVG serialization IDs may
differ; byte identity is not claimed. Final PDF SHA-256:
`d762c12b50dbe46db5b963d81aa6140f474d384252ced601e22e6a9d8a8603c7`.

Runtime and dependency versions are unchanged: Node 24.19.0, pnpm 11.25.0,
Chromium 156.0.8078.4; exact package pins and lock integrity remain
as previously recorded. `package.json` gains only the tooling `test` command.
`print.css`, `pnpm-lock.yaml` and `pnpm-workspace.yaml` hashes are unchanged.
Windows was tested; Linux/macOS and PDF/UA are not newly certified.

## Exact authored changed-file list for this technical correction

Modified existing tooling files:

1. `docs/architecture/tools/generate.mjs`
2. `docs/architecture/tools/validate.mjs`
3. `docs/architecture/tools/package.json`
4. `docs/architecture/tools/README.md`
5. `docs/architecture/tools/VALIDATION.md`

Created tooling source/test files:

6. `docs/architecture/tools/markdown.mjs`
7. `docs/architecture/tools/content-checks.mjs`
8. `docs/architecture/tools/regression.test.mjs`

The final PDF was regenerated at
`docs/architecture/generated/CryptoScript-Workbench-Architecture.pdf`.
Ignored generation diagnostics/manifests were regenerated as usual; latest
repeatability evidence is `generated/technical-repeatability.json`, final PNGs and
contact sheets are in `generated/technical-review-qa/`, and the previous approved
edition is preserved in `generated/technical-review-before/`. These verification
artifacts are not authored source files or staged outputs. The earlier review ZIP
is preserved as a previous-stage snapshot and has not been repackaged; review the
actual worktree files and latest PDF for this correction.

## Repository and boundaries

All 11 architecture input hashes match the approved pre-correction manifest.
`REVIEW-METADATA.md` is byte-for-byte unchanged from the prior review package.
No chapters, evidence, diagrams, architecture entry-point metadata, historical
statements, application/cryptography/GUI code, application tests or CI files were
modified by this technical correction. The three tracked metadata changes already
present at task entry remain exactly as before. The tools directory now contains
13 untracked authored files; nothing is staged. The generated PDF is ignored.

The two retained publication notices disclose historical `19c79fe` analysis text
and the already approved uncommitted metadata changes. They are not validation
failures. **Stop here for final independent review. Do not commit, push or merge.**

---

## Earlier finalization and validation history

The previous reports below are retained verbatim. Their source-code review and
packaging statements describe their earlier stages; current technical-correction
results and the exact changed-file list are above.

# Architecture PDF generator finalization

Finalized on **2026-10-15** in the actual Architecture Recovery worktree:
`C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Architecture`. Branch: `docs/architecture-recovery`. HEAD:
`523bf99751fe56aaff8ad4687d9f9de2321d811a`. The project owner confirmed that the revised
51-page PDF layout is independently approved and M1/M2 are resolved. Generator
source independent technical review remains pending. No commit, push or merge
was performed; nothing is staged. Generated outputs remain ignored.

## Approved metadata corrections

Chapter 2 and Chapter 3 current `Status` rows now read exactly `Reviewed`.
Chapter 1 already had that value. The architecture README records Chapters 1,
2 and 3 as `Reviewed`, updates both chapter contents statuses, and identifies
the available generator, installation instructions, generation command and output.
`REVIEW-METADATA.md` records the owner's approval and retains the original proposal
verbatim as historical verification evidence. Historical starting points, analysis
dates, implementation baselines, findings, all four Mermaid sources and all three
evidence records are preserved. Only three authoritative input hashes changed:
README and the current status rows of Chapters 2 and 3. Chapter diffs were checked
to contain exactly one status-row replacement each.

## Final verification

The repository-root command `node docs/architecture/tools/generate.mjs` succeeded
twice, at `2026-10-15T15:49:24.163Z` and
`2026-10-15T15:50:20.222Z`. Both executions validated their own PDFs.

| Check | Final result |
| --- | --- |
| PDF pages | **51 A4: 48 portrait, 3 landscape** |
| Complete source set | Chapters 1–3, three evidence records, full README, four diagrams; 11 authoritative Markdown/Mermaid inputs |
| Source content | **1,127** source blocks checked; zero detected omissions |
| Diagrams | Exactly one complete vector graph on pages **8, 15, 16, 25**; zero detail tiles |
| Diagram readability | Minimum labels 15.02pt, 13.64pt, 8.05pt and 10.84pt respectively; identical approved layout, vertices, edges, boundaries and label text |
| Tables and findings | Zero inappropriate mid-word breaks, layout overflow, missing headers or off-page text; 14 repeated continuation headers |
| Bookmarks | **63** hierarchical bookmarks; titles, actual heading pages and destinations verified |
| Internal navigation | **152** link annotations; contents page numbers and destination page coordinates validated |
| External/source links | **186** annotations; preserved targets, baseline-addressed repository paths and local Git objects validated |
| Reviewed metadata | Verified in source and extracted PDF: Chapter 1 page 4, Chapter 2 page 9, Chapter 3 page 17; README summary page 48 and chapter statuses page 49 |
| Repeatability | Source/tool hashes, baselines, heading pages, links, page count, complete graph content and rendering selections match across both runs |
| Rendering | All 51 pages rendered successfully with Poppler; every page surveyed and every diagram inspected individually |

Detailed visual inspection covered diagram pages 8/15/16/25, current metadata on
9/17/48/49, directory tables on 17–18, finding records on 42–43, and README tables
on 48–51. All labels, arrows, nodes, boundaries and legends remain visible.
No layout or architecture diagram changes were needed for this finalization.

Repeatability concerns content and layout; generation timestamps and PDF/SVG
serialization IDs can differ. Final PDF SHA-256:
`3e3a8fbc041e89401a4cc48a64ac5fd168d0b189d706d9e11450f2d29fc150b5`.

Machine-readable checks remain under ignored `generated/`: `validation.json`,
`generation-manifest.json`, `layout-check.json`, `diagram-layout-check.json`, and
`repeatability.json`. Final page PNGs and contact sheets are in
`generated/finalization-qa/`; the pre-finalization PDF/manifest/checks are preserved
in `generated/finalization-before/`. The earlier layout validation report below
is retained verbatim and describes the state before this approved finalization.

## Dependencies and environment

Validated on Windows using Node.js **24.19.0**, pnpm **11.25.0**, and Playwright's
Chromium **156.0.8078.4**. Supported Node engine: `>=22.16`.
All direct dependencies are exactly pinned; the pnpm lockfile pins transitive
versions and integrity hashes. No dependency or generator logic changed.

| Dependency | Pinned version |
| --- | --- |
| `@fontsource/noto-sans` | `5.3.0` |
| `@fontsource/noto-sans-mono` | `5.3.0` |
| `@fontsource/noto-sans-symbols-2` | `5.3.0` |
| `markdown-it` | `15.0.2` |
| `mermaid` | `12.2.0` |
| `pagedjs` | `0.4.3` |
| `pdf-lib` | `1.17.1` |
| `pdfjs-dist` | `6.4.299` |
| `playwright` | `1.64.0` |

## Exact finalization file changes

Edited in this finalization:

1. `docs/architecture/chapters/02-system-overview.md`
2. `docs/architecture/chapters/03-project-structure-and-dependencies.md`
3. `docs/architecture/README.md`
4. `docs/architecture/tools/README.md`
5. `docs/architecture/tools/REVIEW-METADATA.md`
6. `docs/architecture/tools/VALIDATION.md`

Regenerated final deliverable:
`docs/architecture/generated/CryptoScript-Workbench-Architecture.pdf`.
Created final review archive:
`docs/architecture/generated/CryptoScript-Architecture-PDF-Generator-Review.zip`.
All other generated manifests, diagnostics, snapshots and review PNGs are ignored
verification artifacts. No application, cryptography, GUI, test or CI file changed.

The ten authored tooling files remain untracked and ready for source review:
`.gitignore`, `README.md`, `REVIEW-METADATA.md`, `VALIDATION.md`, `generate.mjs`,
`package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`, `print.css`, `validate.mjs`.
Their complete paths and tracked status follow:

```text
M docs/architecture/README.md
 M docs/architecture/chapters/02-system-overview.md
 M docs/architecture/chapters/03-project-structure-and-dependencies.md
?? docs/architecture/tools/.gitignore
?? docs/architecture/tools/README.md
?? docs/architecture/tools/REVIEW-METADATA.md
?? docs/architecture/tools/VALIDATION.md
?? docs/architecture/tools/generate.mjs
?? docs/architecture/tools/package.json
?? docs/architecture/tools/pnpm-lock.yaml
?? docs/architecture/tools/pnpm-workspace.yaml
?? docs/architecture/tools/print.css
?? docs/architecture/tools/validate.mjs
```

## Technical review ZIP

Archive: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Architecture/docs/architecture/generated/CryptoScript-Architecture-PDF-Generator-Review.zip`.
The archive contains **23 actual worktree files** plus `REVIEW-PACKAGE.json`:
all ten authored tooling files, all eleven authoritative input files, the existing
architecture `.gitignore`, and the final PDF. Repository-relative paths are
preserved. The extra input sources let the reviewer compare the exact published
metadata and architecture with the final PDF. Installation and use are documented
in `docs/architecture/tools/README.md`; generation still requires a Git checkout
with the recorded baseline objects, as documented there.

Archive construction reads each file directly from this Architecture Recovery
worktree. Every ZIP entry is compared byte-for-byte and by SHA-256 with the live
file after writing; CRC, unique entry names, complete expected inventory, and
PDF/source/tool manifest hashes are checked. `REVIEW-PACKAGE.json` records original
worktree path, branch, HEAD, status, per-file sizes/hashes and validation summary.
No files are sourced from temporary review directories. `node_modules`, caches,
HTML/SVG intermediates, PNGs, older PDFs and other temporary build files are excluded.

## Scope and remaining review

Historical `19c79fe` analysis/starting-HEAD text is intentionally retained. The
cover and manifests separately record actual HEAD and uncommitted source changes.
The two publication notices disclose these facts; they are not validation failures.
External URL reachability and account permissions are not exercised. Windows was
validated; Linux/macOS, cross-platform byte identity and PDF/UA are not claimed.
The approved layout is preserved. **Stop for independent generator technical
review; do not commit, push or merge.**

---

## Historical layout validation report (before owner approval and metadata corrections)

The report below is preserved verbatim. Statements that layout or metadata
approval is pending refer to that earlier validation stage and are superseded
by the owner's approved finalization recorded above.

# PDF layout revision after independent review

Validated on **2026-10-15**, on `docs/architecture-recovery` at documentation HEAD
`523bf99751fe56aaff8ad4687d9f9de2321d811a`, in:

```text
C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Architecture
```

This revision addresses M1 and M2 from the conditional NO-GO. It is submitted for
independent review; it does not declare final layout approval. No application,
architecture source, evidence, implementation baseline, test or CI file was edited.
No commit, push or merge was performed. Chapter 4 was not started.

## Result

**51 A4 pages, reduced from 59.** Chapters 1–3, three evidence records, the source
README and all four diagrams remain present. Eight cropped detail pages have been
removed. The ordinary text pages retain their portrait layout. There are **48
portrait pages and three landscape pages**, with continuous physical page numbers.

| Finding | Change | Verification |
| --- | --- | --- |
| M1: clipped diagram tiles and redundant pages | Exactly one complete vector diagram per page, with automatic portrait/landscape selection. No crops, no tile pages, no independent replacement graph. | Every final diagram page inspected visually; full viewBox and all source labels retained. Renderer candidates with split words, labels over nodes or arrows through unrelated nodes are rejected. |
| M2: mid-word table breaks | Normal word wrapping; presentation-only break opportunities at path/member/function separators. Long finding tables become full-width labeled records using every original cell and column heading. | DOM range checks across every table and finding record found zero split words. Cell and page overflow checks passed. Original problem areas inspected visually at their new page numbers. |
| L1: stale review status | Human approval evidence for Chapters 2 and 3 was located. Exact source metadata edits are proposed separately in `REVIEW-METADATA.md`. | No source status row, historical baseline or evidence record changed. The PDF retains current source text until a separate source edit is approved. |

## Diagram pages

| Page | Diagram | A4 orientation | Smallest label in PDF |
| --- | --- | --- | --- |
| 8 | Product project references | Landscape | 15.02pt |
| 15 | System context | Landscape | 13.64pt |
| 16 | Container overview | Portrait | 8.05pt |
| 25 | Build and test dependency coverage | Landscape | 10.84pt |

All four selected renderings use Mermaid's bundled ELK engine. The generator
compares ELK and Dagre with proportional font/wrapping settings; the unchanged
Mermaid bytes supply nodes, labels, subgraph boundaries, edges, directions and
styles. Dagre's container candidate was rejected because an arrow passed through
an unrelated node. Other candidates with label/node overlap or split words were
also rejected. The full candidate diagnostics are in the ignored
`diagram-layout-check.json` output.

Mixed sheets are printed directly by Chromium. Both Paged.js' page boxes and sheet
wrappers are synchronized with native named A4 print rules. No PDF page is
replaced with a raster image. All node, edge and boundary labels are verified in
the SVG and on that diagram's single PDF page. Historical Console remains absent
from the four diagram sources/renderings.

## Validation evidence

- **PDF opening/rendering:** PDF.js parses the complete output and Poppler renders
  all 51 pages successfully.
- **Content:** all **1,126** Markdown inline/fenced blocks are found in prepared
  HTML, paginated text and extracted PDF text; zero detected omissions. All **11**
  authoritative Markdown/Mermaid source hashes match the preceding 59-page edition
  and the unchanged Git sources. The finding-record transformation changes only
  layout and repeats the original column headings as field labels.
- **Traceability:** implementation baselines remain `d3099c80af3891ad54444e544f528c9c5ed6b93f`
  for Chapters 1–2 and `efa81d6ca282ad7a2b8959d0f406f741c87006b0` for Chapter 3.
  The cover records actual documentation HEAD separately.
- **Navigation:** **63 hierarchical bookmarks**, **152 internal link annotations**
  and **186 external link annotations** validate. Every bookmark resolves to its
  actual heading page; contents page numbers agree. Internal destination coordinates
  are inside the target sheet, including landscape destinations. External targets
  and immutable baseline-addressed source paths are retained and checked. Annotation
  counts include multiple rectangles for wrapped links and repeated table headers.
- **Tables:** **14 repeated continuation headers**; zero detected mid-word breaks,
  missing table headers, page overflow or text outside physical PDF sheets. Checks
  cover all tables, not only the examples called out by the reviewer.
- **Repeatability:** consecutive final generations succeed with matching source
  hashes, tooling hashes, baselines, heading pages, link targets, page count,
  diagram graph content, renderer choices, orientations and label sizes. Dates and
  PDF/SVG serialization identifiers may vary. See ignored `repeatability.json`.
- **Repository:** Git source comparisons confirm no tracked changes. Only the
  documentation tooling directory is untracked. Generated PDF/SVG/HTML/QA output
  remains beneath the existing ignored `generated/` directory.

The machine-readable output includes `generation-manifest.json`,
`layout-check.json`, `diagram-layout-check.json`, `validation.json` and
`repeatability.json` under `generated/`. Generated review PNGs are in
`generated/review-qa/`. The preceding PDF and manifest were preserved in
`generated/review-before/` for comparison.

## Visual review

Every diagram page (8, 15, 16, 25) was inspected at full-page review resolution,
including all arrows, node labels, classifications, boundaries and legends.
No label, node, arrow or legend is cropped. The container overview uses the
available portrait height; wide diagrams use landscape A4 without distorting their
aspect ratios. No redundant detail pages remain.

Table review included every rendered page in a contact-sheet survey and detailed
inspection of the Chapter 3 directory table (pages 17–18), project/package and
boundary tables (19–24), large evidence tables (31–33 and 39–40), CI observation
tables (41–42), finding records (42–43) and the README tables (48–50). Directory
components and identifiers stay intact; wrapping occurs at spaces or meaningful
punctuation. Finding names have full-width headings instead of narrow ID cells.
Title, contents, chapter boundaries, running headers and footers were also checked.

## Changes in this request

| Tool file | Change |
| --- | --- |
| `generate.mjs` | Complete diagram candidate selection, mixed A4 physical sheets, word/collision checks, finding records, safe table break opportunities and diagnostic output. |
| `print.css` | Landscape diagram sheets, compact diagram captions, normal table wrapping and finding-record styling. |
| `validate.mjs` | One-page diagram/orientation checks, boundary labels, collision/word diagnostics and PDF destination coordinate bounds. |
| `README.md` | Updated generation, presentation and validation instructions. The documented generation command is unchanged. |
| `VALIDATION.md` | This revised validation report. |
| `REVIEW-METADATA.md` | New separate approval evidence and exact proposed metadata edits for L1. No proposal applied to architecture content. |

No dependency versions, package lock, application build or GitHub Actions workflow
changed in this request. The ten untracked tooling files currently present are:

```text
?? docs/architecture/tools/.gitignore
?? docs/architecture/tools/README.md
?? docs/architecture/tools/REVIEW-METADATA.md
?? docs/architecture/tools/VALIDATION.md
?? docs/architecture/tools/generate.mjs
?? docs/architecture/tools/package.json
?? docs/architecture/tools/pnpm-lock.yaml
?? docs/architecture/tools/pnpm-workspace.yaml
?? docs/architecture/tools/print.css
?? docs/architecture/tools/validate.mjs
```

## Remaining review items and limits

1. **Final PDF layout approval remains with the independent reviewer.** M1/M2
   changes and their validation are complete; this report is the review submission.
2. **L1 source metadata:** Chapter 2's final corrections were explicitly accepted;
   Chapter 3 has an explicit Final GO confirming completed independent review.
   `REVIEW-METADATA.md` proposes status/README corrections for separate approval.
   The source PDF status rows therefore still show the old pending-review wording.
3. **Historical HEAD wording:** README and Chapter 3 evidence retain older
   `19c79fe` analysis/starting-HEAD text. The cover records actual HEAD `523bf99`.
   Historical implementation baselines and evidence must remain unchanged.
4. **Platform/format scope:** Windows was exercised. Linux/macOS remain untested.
   Fonts are embedded, including Windows fallbacks for a few Unicode glyphs;
   cross-platform byte identity and PDF/UA conformance are not claimed. External
   URL reachability and account permissions are not tested. New sources still
   require visual review; unsuitable oversized diagrams/rows fail rather than crop.

Stop here for independent review. Do not commit or push.
