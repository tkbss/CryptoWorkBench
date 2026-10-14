# CryptoScript Workbench Architecture

This is the entry point for the architecture recovery documentation. It describes the implemented system using repository evidence and keeps interpretations, questions, recommendations and plans distinguishable from implementation facts.

| Metadata | Value |
| --- | --- |
| Documentation status | Initial draft for review; Chapter 1 completed |
| Analysis baseline | `d3099c80af3891ad54444e544f528c9c5ed6b93f` |
| Analysis date | 2026-10-13 |
| Evidence method | Static inspection of files at the baseline commit |
| Working tree | Local application changes were present and excluded from baseline findings |
| Source formats | Markdown for text; Mermaid for diagrams |
| Language | English for all documentation and diagram content |

See the [baseline and evidence index](evidence/01-introduction-baseline.md) for sources and analysis limitations.

## Table of Contents

The chapter order and titles follow the approved structure. Only completed chapters have navigation links. Planned source paths reserve filenames without creating unanalyzed chapter files.

| Chapter | Title | Source | Status |
| --- | --- | --- | --- |
| 1 | [Introduction](chapters/01-introduction.md) | `chapters/01-introduction.md` | Completed; awaiting review |
| 2 | System Overview | `chapters/02-system-overview.md` | Not yet analyzed |
| 3 | Project Structure and Dependencies | `chapters/03-project-structure-and-dependencies.md` | Not yet analyzed |
| 4 | Language Architecture | `chapters/04-language-architecture.md` | Not yet analyzed |
| 5 | Runtime Architecture | `chapters/05-runtime-architecture.md` | Not yet analyzed |
| 6 | Cryptographic Architecture | `chapters/06-cryptographic-architecture.md` | Not yet analyzed |
| 7 | Diagnostics and Error Handling | `chapters/07-diagnostics-and-error-handling.md` | Not yet analyzed |
| 8 | Interfaces and Integration | `chapters/08-interfaces-and-integration.md` | Not yet analyzed |
| 9 | Data Flows and Execution Sequences | `chapters/09-data-flows-and-execution-sequences.md` | Not yet analyzed |
| 10 | GUI Architecture | `chapters/10-gui-architecture.md` | Not yet analyzed |
| 11 | Testing and Quality Assurance | `chapters/11-testing-and-quality-assurance.md` | Not yet analyzed |
| 12 | Architecture Assessment | `chapters/12-architecture-assessment.md` | Not yet analyzed |
| 13 | Architecture Evolution and Roadmap | `chapters/13-architecture-evolution-and-roadmap.md` | Not yet analyzed |

## Documentation Areas

| Area | Purpose | Initial state |
| --- | --- | --- |
| `chapters/` | Numbered architecture chapters in Markdown | Chapter 1 available |
| `diagrams/` | Mermaid sources, accompanied by evidence and a legend | [Project reference overview](diagrams/01-project-references.mmd) available |
| `decisions/` | Architecture decision records with context, status and consequences | Reserved; create when a decision is documented |
| `evidence/` | Baselines, source references, verification notes and analysis limitations | [Introduction evidence](evidence/01-introduction-baseline.md) available |
| `assets/` | Authored supporting assets that cannot be expressed as Markdown or Mermaid | Reserved; create when content is available |
| `tools/` | Reproducible documentation generation and validation tools | Reserved; create when tooling is available |

Reserved areas do not need empty directories or placeholder files. Git records files; the table preserves the intended organization until content exists.

## Evidence Conventions

| Label | Meaning | Required treatment |
| --- | --- | --- |
| **[F] Verified implementation fact** | Directly supported by inspected repository files at a recorded commit | Cite the file and relevant declaration or method; state the verification method |
| **[I] Architectural interpretation** | An explanation of verified facts | Cite supporting facts and identify the inference |
| **[Q] Open question** | A matter that the available evidence does not settle | State what remains unknown and what evidence is needed |
| **[R] Recommendation** | A proposed improvement or future investigation | Keep it separate from the implemented architecture and record its rationale |
| **[P] Plan** | Intended future scope or behavior | Identify its planning source; do not present it as implemented functionality |

Static source verification establishes what the code declares or calls. It does not establish successful runtime execution, cryptographic conformance, deployment support or test results. Findings in later chapters must record any additional validation used.

## Source and Publication Policy

Markdown and Mermaid are the authoritative sources intended for version control in this repository. Keep authored evidence, decisions, assets and documentation tools under Git when added. No commit or staging is performed as part of this initialization.

Generated PDF, DOCX and rendered diagram files are reproducible outputs. Write them beneath `generated/`, which is excluded by the local [ignore policy](.gitignore). Commit such outputs only if a later publication policy explicitly requires it. No generated outputs or publishing tools are included in this initialization.

Use relative links with forward slashes and exact filename casing. New chapter, evidence and diagram filenames use lowercase kebab-case and two-digit chapter prefixes. `README.md` retains its conventional casing. References to application files preserve their existing casing. Do not introduce links to chapter files before those files exist.
