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
