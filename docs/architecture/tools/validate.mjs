import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { getDocument } from 'pdfjs-dist/legacy/build/pdf.mjs';
import { assertCurrentSources, assertSourceBlocks } from './content-checks.mjs';

import { language, outputDirectory } from './publication.mjs';
import { validateLanguageSources } from './bilingual-checks.mjs';
const normalize = text => text.normalize('NFC').replace(/[\s\u00ad\u200b]/g, '');
export async function validate(directory, expectedLanguage = 'en') {
  // A previous pass must never survive failed current-source validation.
  await fs.rm(path.join(directory, 'validation.json'), { force: true });
  const manifest = JSON.parse(await fs.readFile(path.join(directory, 'generation-manifest.json'), 'utf8'));
  assert.equal(manifest.language, expectedLanguage, 'Publication language mismatch');
  const architecture = path.resolve(directory, expectedLanguage === 'de' ? '../..' : '..');
  await assertCurrentSources(manifest, architecture);
  const sourceIntegrity = await validateLanguageSources(architecture, expectedLanguage);
  for (const tool of manifest.tooling) {
    const current = await fs.readFile(path.join(architecture, 'tools', tool.file));
    assert.equal(crypto.createHash('sha256').update(current).digest('hex'), tool.sha256, `Tooling differs from manifest: ${tool.file}`);
  }
  const layout = JSON.parse(await fs.readFile(path.join(directory, 'layout-check.json'), 'utf8'));
  assert.equal(layout.failures.length, 0, 'DOM layout overflow');
  assert.equal(layout.broken.length, 0, 'Broken HTML destinations');
  assert.equal(layout.splitWords.length, 0, 'Inappropriate mid-word breaks in tables or finding records');
  assert.equal(layout.diagramPages.length, manifest.diagrams.length, 'Exactly one complete page per diagram');
  const bytes = new Uint8Array(await fs.readFile(path.join(directory, 'CryptoScript-Workbench-Architecture.pdf')));
  assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'), manifest.pdfSha256, 'PDF differs from generation manifest');
  const loading = getDocument({ data: bytes, useSystemFonts: false });
  const pdf = await loading.promise;
  try {
    assert.equal(pdf.numPages, manifest.layoutPages, 'PDF/pagination page count');
    const text = [];
    const bodyText = [];
    const annotations = [];
    const pageSizes = [];
    const textOutsidePage = [];
    for (let n = 1; n <= pdf.numPages; n++) {
      const page = await pdf.getPage(n);
      const content = await page.getTextContent();
      text.push(content.items.map(i => i.str || '').join(' '));
      const [x0, y0, x1, y1] = page.view;
      bodyText.push(content.items.filter(i => i.transform && i.transform[5] > 43 && i.transform[5] < y1 - 35).map(i => i.str || '').join(' '));
      assert.ok(content.items.some(i => i.str === String(n) && i.transform?.[5] < 43), `Footer page number ${n}`);
      const sides = [x1 - x0, y1 - y0].sort((a,b) => a-b);
      assert.ok(Math.abs(sides[0] - 595.28) < 2 && Math.abs(sides[1] - 841.89) < 2, `Page ${n} is not A4`);
      pageSizes.push({ page: n, width: x1 - x0, height: y1 - y0 });
      for (const item of content.items) if (item.str?.trim() && (item.transform[4] < -1 || item.transform[5] < -1 || item.transform[4] + item.width > x1 + 2 || item.transform[5] > y1 + 2)) textOutsidePage.push({ page: n, text: item.str });
      annotations.push(...(await page.getAnnotations()).filter(a => a.subtype === 'Link').map(a => ({ ...a, page: n })));
    }
    assert.equal(textOutsidePage.length, 0, `Text outside physical page: ${JSON.stringify(textOutsidePage.slice(0,5))}`);
    const allText = normalize(bodyText.join(' '));
    const missing = manifest.sourceBlocks.filter(b => !allText.includes(normalize(b.text)));
    assert.equal(missing.length, 0, `Source blocks missing from PDF: ${JSON.stringify(missing.slice(0,5))}`);
    assert.ok(Array.isArray(manifest.sourceSections), 'Source-scoped accounting required; regenerate older snapshots');
    const sourceText = {};
    for (const section of manifest.sourceSections) {
      assert.ok(section.startPage >= 1 && section.endPage <= pdf.numPages && section.startPage <= section.endPage, 'Source section page bounds');
      assert.ok(!Object.hasOwn(sourceText, section.source), 'Unique source section');
      sourceText[section.source] = bodyText.slice(section.startPage - 1, section.endPage).join(' ');
    }
    const accounting = assertSourceBlocks(manifest.sourceBlocks, sourceText, 'PDF');
    const flatten = entries => entries.flatMap(e => [e, ...flatten(e.items || [])]);
    const outline = flatten(await pdf.getOutline() || []);
    const expected = manifest.headings.filter(h => h.level <= 2);
    assert.equal(outline?.length, expected.length, 'Bookmark count');
    for (const [i, entry] of outline.entries()) {
      const dest = typeof entry.dest === 'string' ? await pdf.getDestination(entry.dest) : entry.dest;
      assert.ok(dest, `Bookmark destination: ${entry.title}`);
      const number = await pdf.getPageIndex(dest[0]) + 1;
      assert.equal(number, expected[i].page, `Bookmark page: ${entry.title}`);
      assert.equal(entry.title, expected[i].title, 'Bookmark title');
      assert.ok(normalize(text[number - 1]).includes(normalize(expected[i].title)), `Heading absent from bookmark page: ${entry.title}`);
    }
    const internal = annotations.filter(a => a.dest);
    const external = annotations.filter(a => a.url);
    assert.ok(internal.length >= expected.length, 'TOC and internal link annotations');
    for (const link of internal) {
      const dest = typeof link.dest === 'string' ? await pdf.getDestination(link.dest) : link.dest;
      assert.ok(dest, 'Internal link resolves');
      link.destinationPage = await pdf.getPageIndex(dest[0]) + 1;
      assert.ok(link.destinationPage <= pdf.numPages, 'Internal link page exists');
      if (dest[1]?.name === 'XYZ') {
        const target = pageSizes[link.destinationPage - 1];
        if (typeof dest[2] === 'number') assert.ok(dest[2] >= -1 && dest[2] <= target.width + 1, 'Internal destination X inside page');
        if (typeof dest[3] === 'number') assert.ok(dest[3] >= -1 && dest[3] <= target.height + 1, 'Internal destination Y inside page');
      }
    }
    for (const link of manifest.links.filter(l => l.type === 'external')) assert.ok(external.some(a => a.url === link.target || a.unsafeUrl === link.target), `Missing external annotation: ${link.target}`);
    // Verify every TOC entry has its printed page number and an annotation to that page.
    const contentsPages = manifest.headings[0].page - 1;
    for (const h of expected) {
      assert.ok(internal.some(a => a.page > 1 && a.page <= contentsPages && a.destinationPage === h.page), `TOC annotation destination: ${h.title}`);
      const line = normalize(`${h.title}${h.page}`);
      assert.ok(text.slice(1, contentsPages).some(t => normalize(t).includes(line)), `TOC page number: ${h.title}`);
    }
    for (const diagram of manifest.diagrams) {
      const heading = manifest.headings.find(h => h.id === `source-${diagram.file.replace(/[^a-zA-Z0-9]/g, '-')}`);
      assert.ok(heading, `Diagram heading: ${diagram.file}`);
      const svg = await fs.readFile(path.join(directory, 'diagrams', path.basename(diagram.file, '.mmd') + '.svg'), 'utf8');
      assert.ok(svg.includes('<svg') && svg.includes('<path'), 'Vector diagram');
      assert.ok(!svg.includes('CryptoWorkBenchConsole'), 'Historical Console excluded from diagrams');
      assert.equal(crypto.createHash('sha256').update(svg).digest('hex'), diagram.svgSha256, 'SVG hash');
      assert.equal(diagram.detailTiles, 0, 'Cropped diagram tiles are forbidden');
      assert.ok(diagram.minimumLabelPoints >= 8, 'Complete diagram labels must be at least 8pt');
      assert.equal(diagram.splitWords.length, 0, 'Diagram identifier words stay whole');
      assert.equal(diagram.nodeCrossings.length, 0, 'No labels over nodes or arrows through unrelated nodes');
      const diagramPages = [text[heading.page - 1]];
      const size = pageSizes[heading.page - 1];
      assert.equal(size.width > size.height, diagram.orientation === 'landscape', 'Diagram sheet orientation');
      for (const label of [...diagram.vertices.map(v => v.label), ...diagram.edges.map(e => e.label), ...diagram.boundaries.map(b => b.label)].filter(Boolean)) {
        const needle = normalize(label.replace(/<br\s*\/?\s*>/gi, ''));
        assert.ok(diagramPages.some(t => normalize(t).includes(needle)), `Diagram label not readable as PDF text: ${diagram.file}: ${label}`);
      }
    }
    const report = { passed: true, language: expectedLanguage, sourceValidation: 'current', sourceBlockGroupsChecked: accounting.groups, sourceOrderChecked: true, pages: pdf.numPages, portraitPages: pageSizes.filter(p => p.height > p.width).length, landscapePages: pageSizes.filter(p => p.width > p.height).length, chapterCount: manifest.chapters.length, evidenceCount: manifest.evidence.length, diagramCount: manifest.diagrams.length, diagramDetailPages: 0, inappropriateWordBreaks: layout.splitWords, repeatedTableHeaders: layout.repeatedTableHeaders, bookmarkCount: outline.length, internalLinkAnnotations: internal.length, externalLinkAnnotations: external.length, sourceBlocksChecked: manifest.sourceBlocks.length, sourceBlockOmissions: missing.length, textOutsidePage, pageSizes, warnings: manifest.warnings };
    await fs.writeFile(path.join(directory, 'validation.json'), JSON.stringify(report, null, 2));
    console.log(`Validation passed: ${pdf.numPages} A4 pages, ${outline.length} bookmarks, ${internal.length} internal / ${external.length} external links, ${manifest.sourceBlocks.length} source blocks.`);
    return report;
  } finally { await loading.destroy(); }
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) { const lang = language(); await validate(outputDirectory(path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..'), lang), lang); }
