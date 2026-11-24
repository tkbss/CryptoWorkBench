# CryptoScript Workbench Architecture

AR-01 reconciles Chapters 1–4 at implementation baseline `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`. Status: draft for independent technical and linguistic review; earlier approvals do not transfer. English is the default; German uses the same chapter/section numbers, evidence identities and technical content.

## Product Scope

Reusable CryptoScript core, Avalonia desktop, composition, application services, presentation, language, runtime, cryptography, diagnostics, documentation and verification infrastructure remain in scope. Current automation/MCP boundaries are included; detailed MCP analysis remains AR-MCP-01. Unfinished GUI redesign and other worktree changes are excluded.

## Table of Contents

| Chapter | Title | Status |
| --- | --- | --- |
| 1 | [Introduction](chapters/01-introduction.md) | AR-01 draft |
| 2 | [System Overview](chapters/02-system-overview.md) | AR-01 draft |
| 3 | [Project Structure and Dependencies](chapters/03-project-structure-and-dependencies.md) | AR-01 draft |
| 4 | [Language Architecture](chapters/04-language-architecture.md) | AR-01 draft |
| 5 | Runtime Architecture | Planned; not analyzed |
| 6 | Cryptographic Architecture | Planned; not analyzed |
| 7 | Diagnostics and Error Handling | Planned; not analyzed |
| 8 | Interfaces and Integration | Planned; not analyzed |
| 9 | [Data Flows and Execution Sequences](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/docs/architecture/chapters/09-data-flows-and-execution-sequences.md) | Historical partial-state contract; excluded from this edition |
| 10 | GUI Architecture | Planned; not analyzed |
| 11 | Testing and Quality Assurance | Planned; not analyzed |
| 12 | Architecture Assessment | Planned; not analyzed |
| 13 | Architecture Evolution and Roadmap | Planned; not analyzed |

## Evidence Conventions

**[F]** verified fact; **[O]** owner decision; **[I]** interpretation; **[Q]** open question; **[R]** recommendation; **[P]** plan. Static inspection does not establish passing tests, certification or platform support. Current sources are in the [evidence register](evidence/ar-01-baseline-reconciliation.md).

## Publication

One shared toolchain uses publication.json and produces separate editions. Chapter 9 is preserved but excluded. Markdown and Mermaid are authoritative. PDF/SVG/HTML/QA outputs remain ignored beneath generated/; dependencies beneath tools/node_modules/. No automatic commit is performed.

```sh
node docs/architecture/tools/generate.mjs
node docs/architecture/tools/generate.mjs --lang de
node docs/architecture/tools/validate.mjs --lang en
node docs/architecture/tools/validate.mjs --lang de
```

English sources retain their existing paths. German sources are under de/. Output locations: generated/CryptoScript-Workbench-Architecture.pdf and generated/de/CryptoScript-Workbench-Architecture.pdf. Failure invalidates only the selected language.

## Preserved History

Chapters 1–2 used d3099c8, Chapter 3 efa81d6 and Chapter 4 29b6b36. Analysis dates were 13–16 October 2026. The earlier architecture checkout and its observation of excluded local application changes remain historical metadata. Chapters 1–3 were reviewed; Chapter 4 was a draft. Historical CI run 38710123715 and the 51-page PDF review concerned their original inputs. Partial Chapter 9 has its own baseline and pending documentation review. All imported implementation reports and evidence remain preserved.

[Original documentation entry point](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/README.md) | [G03](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/docs/architecture/G03-report.md) | [G03 reference checks](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/docs/architecture/evidence/G03/run-reference.ps1)
