# Bilingual architecture publication

The F1 correction to the D1 edition includes Chapters 1–4, 8 and 9. The publication manifest associates each chapter with its primary source register and any explicitly declared update. AR-01 retains its immutable register. A1 adds E01–E30. Markdown and localized Mermaid sources feed the shared PDF generator.

## Current installation and commands

Install the exact locked dependencies with pnpm 11.25.0, dependency scripts disabled, then the matching Playwright Chromium. The package supports Node >=22.16. The original Chapters 1–3 build used 24.19.0 and AR-01 used 24.22.1. D1 and F1 verification used 24.22.3 on Windows, pnpm lockfile dependencies and Playwright 1.64.0. No .NET build is required for publication.

```sh
pnpm --dir docs/architecture/tools install --frozen-lockfile --ignore-scripts
node docs/architecture/tools/node_modules/playwright/cli.js install chromium
node docs/architecture/tools/bilingual-checks.mjs
node docs/architecture/tools/generate.mjs
node docs/architecture/tools/generate.mjs --lang de
node docs/architecture/tools/validate.mjs --lang en
node docs/architecture/tools/validate.mjs --lang de
node --test docs/architecture/tools/regression.test.mjs docs/architecture/tools/bilingual.test.mjs
node docs/architecture/tools/repeatability.mjs
```

Set `SOURCE_DATE_EPOCH` to fixed Unix seconds for a controlled generation timestamp; the repeatability command defaults to `1793923200` (2026-11-06 00:00:00 UTC). Generation blocks HTTP(S); setup requires network access. Git and all recorded baseline objects must be available locally. The generator requires the repository's GitHub origin and verifies source links against local immutable Git objects; it does not test remote account permissions or URL availability.

English output remains `docs/architecture/generated/CryptoScript-Workbench-Architecture.pdf`. German output is `docs/architecture/generated/de/CryptoScript-Workbench-Architecture.pdf`. Each directory holds its own HTML, SVG, layout, manifest and validation files. Failure invalidates only the selected PDF/manifest/pass report. Invalid language arguments fail without selecting or deleting either edition. A changed translation invalidates that edition; shared metadata/tooling changes require revalidation/regeneration as appropriate.

The manifest records source and tooling hashes, language, chapter baselines and updates, actual documentation HEAD, dirty working-tree state, source sections, diagrams and navigation. Authored review sources need not be committed: hashes identify their exact bytes. Application links point to the pinned implementation; historical links point to their original commits. Standalone validation checks current inputs, source Git identities and current tooling as well as the PDF.

The bilingual gate checks numbered sections, code examples, evidence/reference sequences, paragraph/list/table structure and Mermaid topology. Repeatability compares two complete generations per language and the rendered graph topology across languages. This does not substitute for independent technical or linguistic review. See `../decisions/ar-01-bilingual-publication.md`, `../AR-01-integration-report.md` and `../AR-01-publication-validation.md`.

## Registering a chapter

1. Add the same numbered filename to `chapters/` and `de/chapters/`. In each chapter's opening metadata table, use the localized primary and supplementary labels shown in Chapter 1. Set their values to the exact commit hashes for the chapter.
2. Add the filename to the ordered `publication.json` chapter list. Add its `provenance` entry with a primary baseline key and an ordered list of supplementary keys in `updates`. A numbered chapter present on disk but absent from the manifest must be explicitly excluded or publication fails.
3. Reuse a registered baseline where applicable. A new baseline requires a commit, a shared register, and a localized appendix at the declared relative path in both editions. Give each group a unique evidence ID. Cite its own appendix and anchor. For a register with reviewed ranges, pin the canonical range digest to its independently reviewed input. Historical documentary exceptions specify the chapter, numbered section, exact immutable location, purpose and localized link text.
4. For a figure, provide both Mermaid files, register the shared filename in `diagrams` and add its title to both language maps. Update the chapter links, localized navigation and the edition subtitle when changing chapter coverage. Published inventory and shared inputs are recorded in the generation manifests.
5. Update the independently expected, ordered `expectedChapterNumbers` in `bilingual.test.mjs` against the authorized edition list. Keep this assertion independent of `publication.json`.
6. Run the bilingual source gate, both generators, both standalone validators, tests and repeatability command above. Inspect the rendered pages in both languages.

The A1 range anchor was derived from the original AR-MCP-01-A1 REPORT.md (SHA-256 `e50ccfecd69c35d1f4ef9d7c114c398381285689733f007560a2cf7fc87793f5`). For each source link under an E01–E30 heading, serialize the group ID, full commit, repository path, first line and last line as tab-separated fields. Sort the rows lexicographically, terminate each with LF and hash the UTF-8 bytes with SHA-256. The historical AR-01 register retains its stored line counts. GitHub ranges address physical source lines.

## Retained pre-AR-01 tooling history

The material below records the original Chapters 1–3 toolchain and its review history. Its initial branch/HEAD, cumulative discovery, page counts and pending-review statements describe that historical version. The current commands, edition selection and validation results above and in the AR-01 reports take precedence for this package. Original approval evidence is not rewritten.

# Architecture PDF generation

Generate the cumulative reading edition from the current checkout's authoritative
Markdown and Mermaid sources. This tool does not analyze the implementation or
change architecture content. Run it on `docs/architecture-recovery`, initially at
`523bf99751fe56aaff8ad4687d9f9de2321d811a` (Chapters 1–3).

## Installation

Use Node.js **24.19.0** and pnpm **11.25.0** to reproduce the validated environment.
The generator supports Node.js >=22.16. Git must be on PATH, the checkout must have
a GitHub `origin`, and its recorded baseline commits must be available locally.
Windows, Linux and macOS use the same generation command. Only Windows has been
executed in this initial validation.

From the repository root:

```sh
npm install --global pnpm@11.25.0
pnpm --dir docs/architecture/tools install --frozen-lockfile --ignore-scripts
node docs/architecture/tools/node_modules/playwright/cli.js install chromium
```

On Linux, provision Chromium's system libraries using Playwright's documented
`install --with-deps chromium` instead of `install chromium`. This may require
system administrator privileges. No application build or .NET installation is
needed for documentation generation.

All direct versions are exact in `package.json`; `pnpm-lock.yaml` locks transitive
dependencies and integrity hashes. Playwright 1.64.0 supplies its matching Chromium
revision. Fonts are pinned Noto Sans, Noto Sans Mono and Noto Sans Symbols 2
packages. `pnpm-workspace.yaml` identifies two unnecessary legacy postinstall
scripts from Paged.js' dependency tree; installation disables dependency scripts.
Internet access is needed for setup; generation uses local scripts and embedded
fonts and blocks HTTP(S) requests in the rendering browser.

## One generation command

From the repository root:

```sh
node docs/architecture/tools/generate.mjs
```

The command generates and validates:

```text
docs/architecture/generated/CryptoScript-Workbench-Architecture.pdf
```

It returns a nonzero exit status on missing links, missing source text, failed
diagram rendering, layout overflow, invalid source Git objects or PDF validation
failure. The previous PDF, generation manifest and success report are removed before
dependency loading, Git inspection or input parsing. Any caught failure removes
all three again, including failures after a new PDF has been written. Diagnostic
HTML/SVG files may remain for investigation; they are not a valid publication.
Only use an output from a successful command. No output is staged or committed.

Validate the existing output against the current source checkout without regenerating:

```sh
node docs/architecture/tools/validate.mjs
```

Standalone validation checks the complete current input inventory and every source
SHA-256/byte length against the manifest before accepting the PDF. Changed, added
or removed inputs require regeneration. A failed validation removes the old pass
report. This command does not offer unchecked snapshot validation; older manifests
without source-section accounting also require regeneration.

The existing `docs/architecture/.gitignore` excludes `generated/`. This directory
also contains self-contained SVGs, the HTML reading source, source/tool SHA-256
hashes and Git metadata in `generation-manifest.json`, pagination diagnostics in
`layout-check.json`, and PDF validation in `validation.json`. Keep these together
when retaining a review snapshot. The PDF itself includes the evidence records;
it does not require a companion directory to read the evidence.

For a controlled generation timestamp set the standard `SOURCE_DATE_EPOCH`
environment variable to Unix seconds before running the same command. Otherwise,
the actual generation time is recorded in UTC. The PDF metadata uses that time.
The manifest records the platform, Node version and Chromium version. Reproducible
installation and repeatable content/layout are the goal; byte-identical output
across OS/browser/runtime environments is not promised.

## Content and navigation policy

- Include every existing `chapters/NN-*.md`, sorted by numeric prefix. Duplicate
  chapter numbers fail. Add the completed Chapter 4 source normally; the command
  discovers it without a generator change. No files for future chapters are made.
- Include every `diagrams/*.mmd`, rendered directly by Mermaid. Place diagrams
  after the matching chapter in first-reference order, then any remaining sources.
  Preserve authored nodes, relationships, directions, styles, legends and labels.
- Append all `evidence/*.md` in Appendix A and the full source README in Appendix B.
  The README's planning table remains source material; it is not technical content
  for unimplemented chapters or the PDF's generated contents list.
- Keep all source paragraphs, tables, classifications, code blocks and headings.
  Markdown tokens are compared to HTML, paginated text and extracted PDF text.
  Each source must supply enough distinct occurrences of every identical block;
  prose/code blocks must also retain source order. Cover/contents text, diagrams
  and other documents cannot supply missing occurrences. Table cells are counted
  within their source, with order exempted for the approved finding-record layout.
- Namespace heading anchors by source file. Relative links to included documents
  and Mermaid files become internal PDF destinations. External URLs are preserved.
  Relative implementation links become GitHub URLs at the referring document's
  recorded implementation baseline, retaining line fragments and link labels.
  Referenced repository paths are checked against those local Git objects.
  Documentation-only source links use the documentation revision. Network reachability
  and GitHub access permissions are not tested.
- Record the actual documentation HEAD separately from each chapter's implementation
  baseline. Working-tree source changes are disclosed and hashed. Never rewrite
  historical baseline or review metadata as part of publication.

The restrained A4 layout uses portrait text pages and either portrait or landscape
diagram pages. It has running headers, physical page numbers,
repeating table headers and chapter boundaries. Contents entries include H1/H2
headings with clickable page numbers; PDF bookmarks follow that hierarchy.
Unicode is retained, including the original arrows and dashes. Noto fonts are
embedded in the PDF and standalone diagram SVGs.

Each Mermaid graph occupies exactly one page, with its full viewBox and preserved
vectors. Cropped tiles and redundant detail pages are forbidden. The generator
compares Mermaid's bundled Dagre and ELK layouts, font sizes 16/20/22/23/24/32px,
and proportional wrapping widths 280/400/500px at 16px font size. It selects the
most readable complete A4 orientation after rejecting split identifier words,
labels overlapping nodes, and arrows crossing unrelated nodes. The chosen labels
must be at least 8pt in the PDF; an unsuitable new diagram fails for deliberate
layout review. Source nodes, edge types/directions, boundaries, styles, legends and
label text are preserved, checked and recorded. No Mermaid source is edited.

Mixed orientation requires both Paged.js' internal page boxes and Chromium's
physical print sheets to have matching dimensions. The generator synchronizes
their wrappers after pagination and adds native named A4 print rules, preserving
browser-generated PDF links and vectors without replacing or rasterizing pages.

Tables use normal word wrapping without arbitrary mid-word breaks. Presentation
break opportunities are added at path/member separators (`/`, `.`, `:`, `(`),
without inserting visible characters. Full finding-name tables (`Fnn-nn ...`)
are presented as labeled records using all original cells and column headings.
This avoids narrow identifier columns. Repeated headers remain on ordinary tables.
DOM range checks reject words split across lines in tables and finding records.

## Toolchain choice and limits

The repository has Markdown tables, fenced examples, cross-file evidence links
and four Mermaid graphs, with no existing documentation build. A separate Node
tool directory keeps this workflow outside .NET builds, tests and CI. Markdown-it
preserves the Markdown structure; Mermaid produces actual SVGs; Paged.js supplies
CSS pagination and page-numbered contents; Playwright prints the embedded vectors;
pdf-lib writes explicit hierarchical bookmarks; PDF.js independently reads and
checks the finished artifact. This avoids a platform TeX installation and gives
the renderer direct support for Mermaid SVG and pinned web fonts.

Useful upstream documentation:

- [Mermaid configuration](https://mermaid.js.org/config/usage.html)
- [Paged.js pagination](https://pagedjs.org/en/documentation/)
- [Paged.js contents page numbers](https://pagedjs.org/posts/en/build-a-table-of-contents-from-your-html/)
- [Playwright PDF printing](https://playwright.dev/docs/api/class-page#page-pdf)
- [Playwright browser installation](https://playwright.dev/docs/browsers)

This first version supports the sources currently present. New image tokens or
unsupported link schemes fail explicitly and need deliberate support; they are not
silently dropped. HTML and SVG are generated intermediates, not a published web
site. PDF tagging is requested from Chromium, but PDF/UA conformance is not claimed.
Tables must have rows shorter than a page; layout checks reject overflow rather
than hiding it. New chapters or diagrams should receive visual review after
generation even when automatic checks pass. Poppler's `pdftoppm` is useful for that
review but is not required by the generation command.

See `VALIDATION.md` for finalization results and the retained validation history.
`REVIEW-METADATA.md` records the owner-approved corrections applied to the current
source review statuses, together with the original proposal and approval evidence.
The generator publishes the source metadata without substituting status values.
The PDF layout is approved; generator source technical review remains pending.


## Independent technical review package

The final review ZIP is
`docs/architecture/generated/CryptoScript-Architecture-PDF-Generator-Review.zip`.
It preserves repository-relative paths for all authored tooling files, the final
PDF and the exact architecture input sources. `REVIEW-PACKAGE.json` records SHA-256
hashes, the originating worktree, branch, HEAD and verification results. Dependency
directories, caches and temporary build products are excluded. Use the files in
the documented Git checkout with the recorded baseline objects to regenerate;
the archive does not contain Git history. See `VALIDATION.md` for final checks and
the retained prior validation history. Source technical review remains pending.


## Markdown safety and regression tests

Raw HTML is disabled (`html: false`); no source HTML element is required by the
current architecture Markdown. HTML-looking source is rendered as literal text,
including script and anchor elements. Markdown-it's executable-link scheme checks
remain enabled, and the generator's explicit supported-link policy still applies.
Only generator-authored markup, local renderer scripts and strict Mermaid SVGs
enter the rendering document. No sanitizer allowlist or new dependency is needed.

Run the tooling regression suite from the repository root:

```sh
node --test docs/architecture/tools/regression.test.mjs
```

Or run `pnpm --dir docs/architecture/tools test`. The tests use the installed
Playwright browser and require an existing successfully generated PDF. They check
raw scripts/event handlers/dangerous links in a real browser, ordinary Markdown,
missing repeated blocks, source order and source boundaries, current-source hash
and inventory drift, and early/dependency/late generation failure cleanup. Test
copies live under ignored `generated/technical-regression-*` and are removed after
each test; the actual chapters, diagrams and evidence are never edited.

The review ZIP described above has been refreshed from the actual Architecture
Recovery worktree after the M1/M2/L1 technical corrections. It includes all thirteen
authored tooling files, the regression tests, the current architecture inputs and
the validated 51-page PDF. The previous ZIP is retained under
`generated/technical-review-before/` as historical review evidence.
