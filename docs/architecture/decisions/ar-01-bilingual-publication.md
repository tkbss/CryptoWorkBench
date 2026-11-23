# AR-01 bilingual publication decision

Status: implemented for review; independent technical and linguistic acceptance pending.
Authority: accepted AR-01/A0 and explicit AR-01/A1 implementation instruction, 2026-11-06.

## Source and publication layout

English retains existing chapter/diagram paths and is the CLI default. German uses matching filenames under `de/`. One `publication.json` names exactly Chapters 1–4, seven diagrams, the localized current evidence appendix and README. The partial Chapter 9 remains untouched and excluded from both editions. Historical reports and original evidence remain outside the current-edition input set; this is publication selection, not deletion.

The source register is language-neutral: R01–R16 identify 110 immutable source objects, with SHA-256 and line counts. Removed paths have explicit absence checks. Both editions use the same source URLs and evidence IDs. Historical chapter/diagram/README bytes are retained under `evidence/history/abbf7b7b`; their original relative links belong to the original tree. Navigable immutable originals are linked in both current evidence appendices.

## Localization and terminology

| English | German | Decision |
| --- | --- | --- |
| Architecture recovery | Architekturrekonstruktion | Describes recovery of implemented structure, not disaster recovery |
| Baseline | Implementierungsstand | Use full immutable commit in metadata; preserve historical commits |
| Assignment | Zuweisung | Distinct from Deklaration; target must already exist |
| Evaluation | Auswertung | Distinct from Parsing and mapping/Abbildung |
| Publication (runtime state) | Publikation | Atomic visibility of a prepared value; not a documentation release |
| Publication (document) | Veröffentlichung / Ausgabe | Context-specific document generation |
| Host admission | Hostzulassung | Entry coordination; does not invent a security guarantee |
| Interpreter ownership | Interpreterzuständigkeit | Existing ownership scope; does not imply per-session storage |
| Lease | Lease | Preserve technical term where it names the established coordination concept |
| Partial state | Teilzustand | Actual state after earlier effects; not failed all-or-nothing restoration |
| Snapshot / callback / runner | Snapshot / Callback / Runner | Established developer terms; exact identifiers never translated |
| Pinning / pinned proxy | Proxy mit Zertifikat-Pinning | PinnedMcpStdio compares the peer certificate's SHA-256 fingerprint with the configured pin; detailed security analysis remains AR-MCP-01 |

Code examples, identifiers, numbers, source/evidence identities and Mermaid topology remain identical. Prose, captions, legends, navigation and PDF furniture are localized. Structural tests compare section numbering, heading levels, code examples, reference sequences, paragraph/list/table counts and graph structure. These checks cannot prove semantic translation equivalence; a fluent technical reviewer must do that separately.

## One toolchain and failure policy

`--lang en|de` selects an edition; no flag means English. English keeps the established PDF path. German output and QA intermediates are isolated under `generated/de/`. Generation invalidates only the selected publication before loading dependencies/content and again on failure. The other edition is never a cleanup target. Standalone validation requires the requested language and current input hashes.

Raw HTML remains disabled. Markdown executable-scheme checks, renderer link policy, offline rendering, source-scoped occurrence/order checks, complete SVG checks, minimum 8 pt diagram labels, overflow rejection and stale-output rejection are preserved. New content does not authorize relaxing these gates.

## Reproducibility contract

Use exact locked dependencies, matching Playwright Chromium, embedded fonts and a fixed `SOURCE_DATE_EPOCH`. Compare source/tooling digests, baselines, page/heading structure, links, graph content, chosen layout and minimum labels between consecutive generations. PDF/SVG serialization identity is not a cross-platform promise. Different language page counts are expected; content/section/topology parity is required.

The authored changes remain uncommitted pending review. Only the authorized local baseline integration merge is committed; no original branch is updated and no push is performed.
