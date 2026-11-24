# AR-01 architecture integration report

Date: 2026-11-06. Authority: accepted AR-01/A0 and explicit AR-01/A1 implementation request.

## Baselines and isolated merge

- Implementation: `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`.
- Architecture source: `abbf7b7b2f22f1bbc06c5c8b28702831099e097f`.
- Merge-base: `5c4b15d8487348ff904a04e9907caf9e00e868a0`; 68 master-only and four architecture-only commits before integration.
- Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/ar-01`.
- Branch: `codex/ar-01-baseline-reconciliation`.
- Authorized integration merge: `3c6c8e9292a1e091bbbd3c571c08d46746ce3218`; parents are the architecture and pinned implementation tips.

Only README conflicted. Explicit reconciliation retained Chapters 1–4, their review/baseline history, the partial Chapter 9 entry, master's G03 report/reference links and the original working-tree observation. No production conflicts occurred. Automatic approval review rejected an initial combined regex-resolution/commit command because selecting one side could discard master information; that rejected command did not run. The conflict was then reconciled explicitly, inspected, and the authorized isolated merge was committed.

`git diff --name-only fbbadae2 HEAD -- . ':(exclude)docs/architecture'` returned no paths after the merge. Every non-architecture tracked file therefore matches the pinned implementation tree, including production code, project/build declarations and other documentation. Imported whitespace findings from `git diff --cached --check` were retained rather than editing baseline production/history files. They are not new authored AR-01 changes.

## Preservation

The original architecture branch, master and all other branch tips are unchanged by this work. Existing GUI, Prism and assignment worktrees were not modified. They are separate active work areas; their locally changing status is not part of the pinned baseline.

All master implementation reports/evidence, partial Chapter 9 and original evidence files remain byte-for-byte preserved in Git. Original Chapters 1–4, README and all seven diagrams are additionally retained byte-for-byte from abbf7b7b under `evidence/history/abbf7b7b`. Original L01–L22 evidence remains in that historical Chapter 4. Current R01–R16 source identities are recorded independently against fbbadae2. Navigable historical source references use immutable GitHub commit URLs in the current evidence appendix; archived relative links belong to the original tree.

## Authored scope

English Chapters 1–4 and seven diagrams are reconciled. Complete matching German sources are under `de/`. The shared publication configuration selects only those chapters, localized evidence and README. Current source identities include 109 file objects and five explicitly absent paths. The tooling adds language selection, localized PDF furniture, independent output directories, source/translation checks and repeatability validation while retaining Markdown and content-integrity protections.

MCP coverage is restricted to implemented project/process/host boundaries. No historical design proposal is used as implementation proof. Unfinished GUI and Prism worktree changes are excluded. No production changes, push, merge into master or update to the original architecture branch is performed. Authored documentation/tooling remains uncommitted for independent review.

## Verification references and limits

The publication/validation report records automated gates, PDF checks and visual QA. The exact changed-file inventory and final status are in `AR-01-changed-files.json`. Application builds/tests are not rerun for this documentation-only package; inspected regression definitions are not claimed as fresh passing runtime results. Technical and linguistic acceptance remains a separate human review.
