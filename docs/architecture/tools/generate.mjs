import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
import crypto from 'node:crypto';

const tools = path.dirname(fileURLToPath(import.meta.url));
const architecture = path.resolve(tools, '..');
const repo = path.resolve(architecture, '../..');
const { language, outputDirectory } = await import('./publication.mjs');
const lang = language();
const generated = outputDirectory(architecture, lang);
const output = path.join(generated, 'CryptoScript-Workbench-Architecture.pdf');
// Invalidate before dependency loading, Git inspection and input parsing.
// Failed runs retain diagnostic intermediates, never a publication or pass report.
const invalidate = () => Promise.all(['CryptoScript-Workbench-Architecture.pdf', 'generation-manifest.json', 'validation.json'].map(file => fs.rm(path.join(generated, file), { force: true })));
await fs.mkdir(generated, { recursive: true });
await invalidate();
try {
  const { publication } = await import('./publication.mjs');
  const pub = await publication(architecture, lang);
  const { locales } = await import('./locales.mjs');
  const L = locales[lang];
  const { chromium } = await import('playwright');
  const { PDFDocument, PDFName, PDFNumber, PDFHexString } = await import('pdf-lib');
  const { validate } = await import('./validate.mjs');
  const { createMarkdown } = await import('./markdown.mjs');
  const { assertSourceBlocks } = await import('./content-checks.mjs');
  const git = (...args) => execFileSync('git', args, { cwd: repo, encoding: 'utf8' }).trim();
  const sha256 = value => crypto.createHash('sha256').update(value).digest('hex');
  const escape = value => String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
  const slug = text => text.toLowerCase().replace(/<[^>]*>/g, '').replace(/[^\p{L}\p{N}_\-\s]/gu, '').replace(/\s/g, '-');
  const docId = file => `source-${file.replace(/[^a-zA-Z0-9]/g, '-')}`;
  const normalize = text => text.normalize('NFC').replace(/\s+/g, '');
  const md = createMarkdown();
  md.renderer.rules.table_open = (tokens, index, options, env, renderer) => {
    const first = tokens.slice(index + 1).find(t => t.type === 'inline');
    if (first?.content === 'ID') tokens[index].attrSet('class', 'evidence-ids');
    return renderer.renderToken(tokens, index, options);
  };
  const revision = git('rev-parse', 'HEAD');
  const branch = git('branch', '--show-current');
  const epoch = process.env.SOURCE_DATE_EPOCH;
  if (epoch !== undefined && !/^\d+$/.test(epoch)) throw new Error('SOURCE_DATE_EPOCH must be Unix seconds.');
  const date = epoch ? new Date(Number(epoch) * 1000) : new Date();
  if (!Number.isFinite(date.getTime())) throw new Error('Invalid generation date.');
  const timestamp = date.toISOString();
  let remote = git('remote', 'get-url', 'origin').replace(/^git@github.com:/, 'https://github.com/').replace(/\.git$/, '');
  if (!remote.startsWith('https://github.com/')) throw new Error('Expected a GitHub origin for portable source links.');
  const { chapters, evidence, diagrams, readme, sources } = pub;
  const numbers = chapters.map(f => Number(path.basename(f).split('-')[0]));
  const original = new Map(await Promise.all(sources.map(async file => [file, await fs.readFile(path.join(architecture, file))])));
  const sourceStatus = git('status', '--porcelain', '--', ...sources.map(f => `docs/architecture/${f}`));
  const status = git('status', '--porcelain', '--untracked-files=normal');
  const warnings = [];
  const documents = new Map();
  const inlineText = token => (token.children || []).map(c => c.type === 'softbreak' || c.type === 'hardbreak' ? '\n' : c.content).join('');
  for (const file of [...chapters, ...evidence, readme]) {
    const text = original.get(file).toString('utf8');
    const tokens = md.parse(text, {});
    const counts = new Map();
    const headings = [];
    for (let i = 0; i < tokens.length; i++) if (tokens[i].type === 'heading_open') {
      const title = inlineText(tokens[i + 1]);
      const base = slug(title);
      const count = counts.get(base) || 0;
      counts.set(base, count + 1);
      const fragment = base + (count ? `-${count}` : '');
      const id = `${docId(file)}--${fragment}`;
      tokens[i].attrSet('id', id);
      headings.push({ id, fragment, title, level: Number(tokens[i].tag.slice(1)) });
    }
    const baseline = pub.config.baseline;
    documents.set(file, { file, text, tokens, headings, baseline, id: docId(file) });
  }
  if (sourceStatus) warnings.push('Authoritative source files have working-tree changes; source hashes identify the rendered bytes.');
  const linkRecords = [];
  const checkedSourceTargets = new Set();
  function rewriteLink(href, document) {
    if (/^[a-z][a-z0-9+.-]*:/i.test(href)) {
      if (!/^(https?:|mailto:)/i.test(href)) throw new Error(`Unsupported link scheme in ${document.file}: ${href}`);
      linkRecords.push({ source: document.file, original: href, target: href, type: 'external' });
      return href;
    }
    const [relative, fragment = ''] = href.split('#');
    const targetFile = relative ? path.posix.normalize(path.posix.join(path.posix.dirname(document.file), decodeURIComponent(relative))) : document.file;
    let result;
    if (documents.has(targetFile)) {
      const target = documents.get(targetFile);
      const heading = fragment ? target.headings.find(h => h.fragment === decodeURIComponent(fragment)) : null;
      if (fragment && !heading) throw new Error(`Missing internal heading: ${document.file}: ${href}`);
      result = `#${heading ? heading.id : target.id}`;
    } else if (diagrams.includes(targetFile)) result = `#${docId(targetFile)}`;
    else {
      const repoPath = path.posix.normalize(path.posix.join('docs/architecture', targetFile));
      if (repoPath.startsWith('../')) throw new Error(`Link escapes repository: ${href}`);
      const commit = repoPath.startsWith('docs/architecture/') ? revision : document.baseline || revision;
      const object = `${commit}:${repoPath}`;
      if (!checkedSourceTargets.has(object)) { git('cat-file', '-e', object); checkedSourceTargets.add(object); }
      result = `${remote}/blob/${commit}/${repoPath.split('/').map(encodeURIComponent).join('/')}${fragment ? `#${fragment}` : ''}`;
    }
    linkRecords.push({ source: document.file, original: href, target: result, type: result.startsWith('#') ? 'internal' : 'external' });
    return result;
  }
  function visit(tokens, fn) { for (const token of tokens) { fn(token); if (token.children) visit(token.children, fn); } }
  for (const document of documents.values()) visit(document.tokens, token => {
    if (token.type === 'link_open') token.attrSet('href', rewriteLink(token.attrGet('href'), document));
    if (token.type === 'image') throw new Error(`Image in ${document.file}: add explicit local image handling before publication.`);
  });
  const body = file => `<section class="document ${file.includes('evidence/') ? 'evidence' : ''}" id="${docId(file)}" data-source="${file}">${md.renderer.render(documents.get(file).tokens, md.options, {})}</section>`;
  const font = async (family, name) => `data:font/woff2;base64,${(await fs.readFile(path.join(tools, 'node_modules', '@fontsource', family, 'files', name))).toString('base64')}`;
  const fontCss = `@font-face{font-family:Document;src:url('${await font('noto-sans', 'noto-sans-latin-400-normal.woff2')}')}@font-face{font-family:Document;src:url('${await font('noto-sans', 'noto-sans-latin-700-normal.woff2')}');font-weight:700}@font-face{font-family:Document;src:url('${await font('noto-sans', 'noto-sans-latin-400-italic.woff2')}');font-style:italic}@font-face{font-family:Code;src:url('${await font('noto-sans-mono', 'noto-sans-mono-latin-400-normal.woff2')}')}@font-face{font-family:MathSymbols;src:url('${await font('noto-sans-symbols-2', 'noto-sans-symbols-2-math-400-normal.woff2')}')}@font-face{font-family:Symbols;src:url('${await font('noto-sans-symbols-2', 'noto-sans-symbols-2-symbols-400-normal.woff2')}')}`;
  await fs.mkdir(path.join(generated, 'diagrams'), { recursive: true });
  await fs.rm(path.join(generated, 'validation.json'), { force: true });
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1600, height: 1200 } });
    // Authoritative text is local. Generation never fetches fonts, scripts or URLs.
    await page.route(/^https?:/, route => route.abort());
    await page.setContent(`<style>${fontCss}body{font-family:Document}</style><div id="render"></div>`);
    await page.evaluate(async () => { await Promise.all(['16px Document', '700 16px Document', '16px MathSymbols', '16px Symbols', '16px Code'].map(f => document.fonts.load(f))); await document.fonts.ready; });
    await page.addScriptTag({ path: path.join(tools, 'node_modules/mermaid/dist/mermaid.min.js') });
    const diagramRecords = [];
    const diagramCandidates = [];
    const diagramHtml = new Map();
    for (const [index, file] of diagrams.entries()) {
      let rendered;
      let bestScore = -Infinity;
      const candidates = [];
      for (const algorithm of ['dagre', 'elk']) for (const fontSize of [16, 20, 22, 23, 24, 32]) for (const baseWrappingWidth of [280, 400, 500]) {
        const wrappingWidth = baseWrappingWidth * fontSize / 16;
        const candidate = await page.evaluate(async ({ source, id, algorithm, wrappingWidth, fontSize }) => {
          mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme: 'default', layout: algorithm, fontSize, look: 'classic', deterministicIds: true, htmlLabels: false, markdownAutoWrap: false, flowchart: { useMaxWidth: false, wrappingWidth, rankSpacing: 8, nodeSpacing: 24, padding: 4, subGraphTitleMargin: { top: 12, bottom: 24 } }, themeVariables: { fontFamily: 'Document, MathSymbols, Symbols', fontSize: `${fontSize}px` } });
          const { svg } = await mermaid.render(id, source);
          document.getElementById('render').innerHTML = svg;
          const el = document.querySelector('#render svg');
          const v = el.viewBox.baseVal;
          const fonts = [...el.querySelectorAll('text,tspan')].filter(e => e.textContent.trim()).map(e => parseFloat(getComputedStyle(e).fontSize));
          const graph = await mermaid.mermaidAPI.getDiagramFromText(source);
          const boundaries = graph.db.getSubGraphs().map(n => ({ id: n.id, label: n.title, members: n.nodes }));
          const boundaryIds = new Set(boundaries.map(n => n.id));
          const vertices = [...graph.db.getVertices().values()].filter(n => !boundaryIds.has(n.id)).map(n => ({ id: n.id, label: n.text }));
          const edges = graph.db.getEdges().map(e => ({ from: e.start, to: e.end, label: e.text, type: e.type, stroke: e.stroke }));
          const labels = [...vertices.map(n => n.label), ...edges.map(e => e.label), ...boundaries.map(n => n.label)].filter(Boolean).map(t => t.replace(/<br\s*\/?\s*>/gi, '').replace(/\s+/g, ''));
          const visible = el.textContent.replace(/\s+/g, '');
          for (const label of labels) if (!visible.includes(label)) throw new Error(`Diagram label omitted: ${label}`);
          const nodeElements = [...el.querySelectorAll('.node')];
          const splitWords = [];
          for (const vertex of vertices) {
            const element = nodeElements.find(n => n.id.includes(`flowchart-${vertex.id}-`));
            if (!element) throw new Error(`Missing node ${vertex.id}; rendered IDs: ${nodeElements.map(n => n.id).join(', ')}`);
            const rows = [...element.querySelectorAll('tspan.text-outer-tspan.row')].map(t => t.textContent);
            for (const word of vertex.label.replace(/<[^>]*>/g, ' ').match(/[\p{L}\p{N}]+/gu) || []) if (rows.length && !rows.some(row => row.includes(word))) splitWords.push(word);
          }
          const collisions = [];
          // An external process must not straddle or appear inside a process frame.
          for (const boundary of boundaries) {
            const cluster = [...el.querySelectorAll('.cluster')].find(n => n.id === boundary.id || n.id === `${id}-${boundary.id}`);
            if (!cluster) throw new Error(`Missing boundary ${boundary.id}`);
            const frame = cluster.querySelector('rect').getBoundingClientRect();
            for (const vertex of vertices.filter(n => !boundary.members.includes(n.id))) {
              const node = nodeElements.find(n => n.id.includes(`flowchart-${vertex.id}-`));
              const box = node.getBoundingClientRect();
              if (Math.min(box.right, frame.right) - Math.max(box.left, frame.left) > 2 && Math.min(box.bottom, frame.bottom) - Math.max(box.top, frame.top) > 2)
                collisions.push(`External node ${vertex.id} overlaps boundary ${boundary.id}`);
            }
          }
          // Boundary titles are architectural content, not decorative whitespace.
          // A title hidden behind a node still exists in SVG text; reject that layout.
          for (const label of el.querySelectorAll('.cluster-label')) {
            if (!label.textContent.trim()) continue;
            const box = label.getBoundingClientRect();
            for (const node of [...nodeElements, ...el.querySelectorAll('.edgeLabels .label')]) {
              const other = node.getBoundingClientRect();
              if (Math.min(box.right, other.right) - Math.max(box.left, other.left) > 2 && Math.min(box.bottom, other.bottom) - Math.max(box.top, other.top) > 2)
                collisions.push(`Boundary label overlaps ${node.id || node.dataset.id || 'edge label'}`);
            }
          }
          for (const label of el.querySelectorAll('.edgeLabels .label')) {
            const box = label.getBoundingClientRect();
            for (const vertex of vertices) {
              const node = nodeElements.find(n => n.id.includes(`flowchart-${vertex.id}-`));
              const other = node.getBoundingClientRect();
              if (Math.min(box.right, other.right) - Math.max(box.left, other.left) > 2 && Math.min(box.bottom, other.bottom) - Math.max(box.top, other.top) > 2) collisions.push(`Edge label ${label.dataset.id} overlaps ${vertex.id}`);
            }
            const edge = edges.find(e => label.dataset.id?.startsWith(`L_${e.from}_${e.to}_`));
            const rows = [...label.querySelectorAll('tspan.text-outer-tspan.row')].map(t => t.textContent);
            for (const word of (edge?.label || '').replace(/<[^>]*>/g, ' ').match(/[\p{L}\p{N}]+/gu) || []) if (rows.length && !rows.some(row => row.includes(word))) splitWords.push(word);
          }
          for (const line of el.querySelectorAll('.edgePaths path')) {
            if (getComputedStyle(line).stroke === 'none' || getComputedStyle(line).opacity === '0') continue;
            const edge = edges.find(e => line.getAttribute('data-id')?.startsWith(`L_${e.from}_${e.to}_`));
            if (!edge) continue;
            for (const label of el.querySelectorAll('.cluster-label')) {
              if (!label.textContent.trim()) continue;
              const box = label.getBoundingClientRect();
              for (let distance = 0; distance < line.getTotalLength(); distance += 5) {
                const p = line.getPointAtLength(distance); const pt = new DOMPoint(p.x, p.y).matrixTransform(line.getScreenCTM());
                if (pt.x > box.left + 2 && pt.x < box.right - 2 && pt.y > box.top + 2 && pt.y < box.bottom - 2) { collisions.push(`${edge.from}->${edge.to} crosses boundary title`); break; }
              }
            }
            for (const vertex of vertices.filter(n => n.id !== edge.from && n.id !== edge.to)) {
              const node = nodeElements.find(n => n.id.includes(`flowchart-${vertex.id}-`));
              const box = node.getBoundingClientRect();
              const length = line.getTotalLength();
              for (let distance = 0; distance < length; distance += 5) {
                const p = line.getPointAtLength(distance); const pt = new DOMPoint(p.x, p.y).matrixTransform(line.getScreenCTM());
                if (pt.x > box.left + 2 && pt.x < box.right - 2 && pt.y > box.top + 2 && pt.y < box.bottom - 2) { collisions.push(`${edge.from}->${edge.to} crosses ${vertex.id}`); break; }
              }
            }
          }
          return { svg, viewBox: [v.x, v.y, v.width, v.height], minFont: Math.min(...fonts), vertices, edges, boundaries, splitWords, collisions };
        }, { source: original.get(file).toString('utf8'), id: `diagram-${index}`, algorithm, wrappingWidth, fontSize });
        const [, , w, h] = candidate.viewBox;
        const score = Math.max(Math.min(178 / w, 248 / h), Math.min(277 / w, 178 / h)) * 96 / 25.4 * candidate.minFont * 72 / 96;
        candidates.push({algorithm, fontSize, wrappingWidth, w, h, score, splitWords: candidate.splitWords, collisions: candidate.collisions});
        if (!candidate.splitWords.length && !candidate.collisions.length && score > bestScore) { rendered = { ...candidate, algorithm, fontSize, wrappingWidth }; bestScore = score; }
        }
      diagramCandidates.push({ file, candidates });
      await fs.writeFile(path.join(generated, 'diagram-layout-check.json'), JSON.stringify(diagramCandidates, null, 2));
      if (!rendered) throw new Error(`No complete diagram layout without split words or node crossings: ${file}. See generated/diagram-layout-check.json.`);
      rendered.svg = rendered.svg.replace(/(<svg\b[^>]*>)/, `$1<style>${fontCss}</style>`);
      await fs.writeFile(path.join(generated, 'diagrams', path.basename(file, '.mmd') + '.svg'), rendered.svg);
      const [, , width, height] = rendered.viewBox;
      // One complete SVG per page. Pick the A4 orientation that maximizes labels.
      const portrait = { orientation: 'portrait', width: 178, height: 248 };
      const landscape = { orientation: 'landscape', width: 277, height: 178 };
      const fitFor = box => Math.min(box.width * 96 / 25.4 / width, box.height * 96 / 25.4 / height);
      const box = fitFor(landscape) > fitFor(portrait) ? landscape : portrait;
      const minPoints = rendered.minFont * fitFor(box) * 72 / 96;
      if (minPoints < 8) throw new Error(`Diagram labels below 8pt on complete A4 page: ${file} (${minPoints.toFixed(2)}pt). See generated/diagram-layout-check.json.`);
      const image = rendered.svg.replace(/<svg\b[^>]*>/, root => root.replace(/\s(?:width|height|viewBox|style|preserveAspectRatio)="[^"]*"/g, '').replace(/>$/, ` viewBox="${rendered.viewBox.join(' ')}" preserveAspectRatio="xMidYMid meet" style="width:100%;height:${box.height}mm">`));
      const html = `<section class="diagram-page ${box.orientation}" id="${docId(file)}"><h1>${L.diagram}: ${escape(pub.edition.diagramTitles[path.basename(file)])}</h1><p class="source-caption">${L.source}: <code>${escape(file)}</code></p><div class="diagram-graphic">${image}</div></section>`;
      diagramHtml.set(file, html);
      diagramRecords.push({ file, viewBox: rendered.viewBox, algorithm: rendered.algorithm, fontSize: rendered.fontSize, wrappingWidth: rendered.wrappingWidth, minimumLabelPoints: minPoints, orientation: box.orientation, detailTiles: 0, splitWords: rendered.splitWords, nodeCrossings: rendered.collisions, vertices: rendered.vertices, edges: rendered.edges, boundaries: rendered.boundaries, svgSha256: sha256(rendered.svg) });
    }
    const headings = [];
    let content = '';
    const remaining = new Set(diagrams);
    for (const file of chapters) {
      content += body(file);
      headings.push(...documents.get(file).headings);
      const number = Number(path.basename(file).split('-')[0]);
      const referenced = linkRecords.filter(l => l.source === file && l.original.endsWith('.mmd')).map(l => path.posix.normalize(path.posix.join(path.posix.dirname(file), l.original)));
      const chapterDiagrams = [...new Set([...referenced, ...remaining])].filter(d => remaining.has(d) && Number(path.basename(d).split('-')[0]) === number);
      for (const diagram of chapterDiagrams) {
        content += diagramHtml.get(diagram);
        headings.push({ id: docId(diagram), title: `${L.diagram}: ${pub.edition.diagramTitles[path.basename(diagram)]}`, level: 1 });
        remaining.delete(diagram);
      }
    }
    for (const file of remaining) { content += diagramHtml.get(file); headings.push({ id: docId(file), title: `${L.diagram}: ${pub.edition.diagramTitles[path.basename(file)]}`, level: 1 }); }
    content += `<section class="appendix-start" id="appendix-evidence"><h1>${L.evidence}</h1><p>${L.evidenceNote}</p></section>`;
    headings.push({ id: 'appendix-evidence', title: L.evidence, level: 1 });
    for (const file of evidence) { content += body(file); headings.push(...documents.get(file).headings); }
    content += `<section class="appendix-start" id="appendix-entry"><h1>${L.entry}</h1><p>${L.entryNote}</p></section>` + body(readme);
    headings.push({ id: 'appendix-entry', title: L.entry, level: 1 }, ...documents.get(readme).headings);
    const baselines = chapters.map(file => `<tr><td>${escape(documents.get(file).headings[0].title)}</td><td><code>${escape(documents.get(file).baseline || 'Not recorded in chapter metadata')}</code></td></tr>`).join('');
    const toc = headings.filter(h => h.level <= 2).map(h => `<li class="toc-level-${h.level}"><a href="#${h.id}"><span>${escape(h.title)}</span></a></li>`).join('');
    const title = `<section class="cover"><p class="eyebrow">AR-01</p><h1>CryptoScript Workbench<br>${L.architecture}</h1><p class="subtitle">${L.subtitle}</p><dl><dt>${L.generated}</dt><dd>${timestamp}</dd><dt>${L.revision}</dt><dd><code>${revision}</code></dd><dt>${L.branch}</dt><dd>${escape(branch)}</dd><dt>${L.chapters}</dt><dd>${numbers.join(', ')}</dd><dt>${L.tooling}</dt><dd>${status ? L.dirty : L.clean}</dd></dl><h2>${L.baseline}</h2><table><thead><tr><th>${L.chapter}</th><th>${L.baseline}</th></tr></thead><tbody>${baselines}</tbody></table><p class="cover-note">${L.coverNote}</p></section>`;
    const css = fontCss + (await fs.readFile(path.join(tools, 'print.css'), 'utf8')).replace('CryptoScript Workbench · Architecture', L.title).replace('Architecture Recovery · Reading edition', L.footer);
    const html = `<!doctype html><html lang="${lang}"><head><meta charset="utf-8"><title>${L.title}</title><style>${css}</style></head><body>${title}<nav class="contents" id="contents"><h1>${L.contents}</h1><ol>${toc}</ol></nav>${content}</body></html>`;
    await fs.writeFile(path.join(generated, 'architecture.html'), html);
    await page.setContent(html, { waitUntil: 'load' });
    await page.evaluate(() => document.fonts.ready);
    await page.evaluate(() => {
      // Keep finding names out of narrow ID columns; retain every source cell.
      for (const table of document.querySelectorAll('table')) {
        const labels = [...table.querySelectorAll('thead th')].map(el => el.innerHTML);
        const rows = [...table.querySelectorAll('tbody tr')];
        if (rows.length && rows.every(row => /^F\d+-\d+\s/.test(row.cells[0].textContent))) {
          const records = document.createElement('div'); records.className = 'finding-records';
          for (const row of rows) {
            const record = document.createElement('article'); record.className = 'finding-record';
            record.innerHTML = `<h3>${row.cells[0].innerHTML}</h3>` + [...row.cells].slice(1).map((cell, i) => `<div class="finding-field"><h4>${labels[i + 1]}</h4><div>${cell.innerHTML}</div></div>`).join('');
            records.append(record);
          }
          table.replaceWith(records);
        }
      }
      // Author-approved spellings stay intact; break paths at separators only.
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      const nodes = []; while (walker.nextNode()) nodes.push(walker.currentNode);
      for (const node of nodes) {
        if (!node.parentElement.closest('td,th,.finding-record') || node.parentElement.closest('style,svg')) continue;
        if (!/[/.(:]/.test(node.textContent)) continue;
        const fragment = document.createDocumentFragment();
        for (const piece of node.textContent.split(/(?<=[/.(:])/)) { fragment.append(document.createTextNode(piece)); if (/[/.(:]$/.test(piece)) fragment.append(document.createElement('wbr')); }
        node.replaceWith(fragment);
      }
    });
    await fs.writeFile(path.join(generated, 'architecture.html'), await page.content());
    // Compare source tokens to the unpaginated DOM before pagination.
    const expectedBlocks = [];
    for (const document of documents.values()) {
      let inTable = false;
      for (const token of document.tokens) {
        if (token.type === 'table_open') inTable = true;
        if (token.type === 'table_close') inTable = false;
        if (token.type === 'inline') expectedBlocks.push({ source: document.file, kind: inTable ? 'table' : 'prose', text: inlineText(token) });
        if (token.type === 'fence' || token.type === 'code_block') expectedBlocks.push({ source: document.file, kind: 'code', text: token.content });
      }
    }
    const before = await page.evaluate(() => Object.fromEntries([...document.querySelectorAll('[data-source]')].map(el => [el.dataset.source, el.textContent])));
    for (const block of expectedBlocks) if (!normalize(before[block.source]).includes(normalize(block.text))) throw new Error(`Source text missing from HTML: ${block.source}: ${block.text.slice(0, 100)}`);
    assertSourceBlocks(expectedBlocks, before, 'Prepared HTML');
    await page.addScriptTag({ content: 'window.PagedConfig={auto:false};' });
    await page.addScriptTag({ path: path.join(tools, 'node_modules/pagedjs/dist/paged.polyfill.js') });
    await page.evaluate(() => {
      class RepeatedTableHeaders extends Paged.Handler {
        renderNode(clone, source) {
          if (clone.nodeType !== 1 || !clone.closest('tbody')) return;
          const table = clone.closest('table');
          const original = source.nodeType === 1 ? source.closest('table') : null;
          const header = original?.querySelector('thead');
          if (!table || table.querySelector('thead') || !header) return;
          const repeated = header.cloneNode(true);
          repeated.dataset.repeatedHeader = 'true';
          for (const el of [repeated, ...repeated.querySelectorAll('*')]) { el.removeAttribute('data-ref'); el.removeAttribute('id'); }
          // Insert during layout, before overflow is measured, not after pagination.
          table.prepend(repeated);
        }
      }
      Paged.registerHandlers(RepeatedTableHeaders);
    });
    await page.evaluate(async () => { await window.PagedPolyfill.preview(); });
    const layout = await page.evaluate(headings => {
      const pages = [...document.querySelectorAll('.pagedjs_page')];
      const positions = headings.map(h => {
        const el = document.querySelector(`.pagedjs_pages [id="${h.id}"]`);
        if (!el) throw new Error(`Heading not paginated: ${h.id}`);
        return { ...h, page: Number(el.closest('.pagedjs_page').dataset.pageNumber) };
      });
      const failures = [];
      const pageText = [];
      for (const p of pages) {
        const area = p.querySelector('.pagedjs_area').getBoundingClientRect();
        pageText.push(p.querySelector('.pagedjs_page_content').textContent);
        for (const el of p.querySelectorAll('.pagedjs_page_content p,.pagedjs_page_content tr,.pagedjs_page_content td,.pagedjs_page_content th,.pagedjs_page_content pre,.pagedjs_page_content h1,.pagedjs_page_content h2,.pagedjs_page_content h3,.finding-record,.diagram-graphic')) {
          if (el.closest('svg')) continue;
          const r = el.getBoundingClientRect();
          if (r.width && (r.left < area.left - 2 || r.right > area.right + 2 || r.top < area.top - 2 || r.bottom > area.bottom + 2)) failures.push({ page: p.dataset.pageNumber, element: el.tagName, text: el.textContent.slice(0, 100), area: { left: area.left, right: area.right, top: area.top, bottom: area.bottom }, bounds: { left: r.left, right: r.right, top: r.top, bottom: r.bottom } });
          if (el.scrollWidth > el.clientWidth + 3 && el.clientWidth) failures.push({ page: p.dataset.pageNumber, overflow: el.tagName, text: el.textContent.slice(0, 100) });
        }
      }
      const broken = [...document.querySelectorAll('.pagedjs_pages a[href^="#"]')].map(a => a.getAttribute('href').slice(1)).filter(id => !document.querySelector(`.pagedjs_pages [id="${CSS.escape(id)}"]`));
      const tablesWithoutHeaders = [...document.querySelectorAll('.pagedjs_pages table')].filter(t => t.querySelector('tbody tr') && !t.querySelector('thead'));
      if (tablesWithoutHeaders.length) failures.push({ tablesWithoutHeaders: tablesWithoutHeaders.length });
      const splitWords = [];
      const walker = document.createTreeWalker(document.querySelector('.pagedjs_pages'), NodeFilter.SHOW_TEXT);
      while (walker.nextNode()) {
        const node = walker.currentNode;
        if (!node.parentElement.closest('td,th,.finding-record') || node.parentElement.closest('svg,style')) continue;
        for (const match of node.textContent.matchAll(/[\p{L}\p{N}]+/gu)) {
          const range = document.createRange(); range.setStart(node, match.index); range.setEnd(node, match.index + match[0].length);
          const lines = new Set([...range.getClientRects()].filter(r => r.width).map(r => Math.round(r.top)));
          if (lines.size > 1) splitWords.push({ page: node.parentElement.closest('.pagedjs_page').dataset.pageNumber, word: match[0] });
        }
      }
      if (splitWords.length) failures.push({ splitWords });
      const diagramPages = [...document.querySelectorAll('.pagedjs_pages .diagram-page')].map(el => ({ id: el.id, page: Number(el.closest('.pagedjs_page').dataset.pageNumber), graphics: el.querySelectorAll('.diagram-graphic > svg').length }));
      if (new Set(diagramPages.map(p => p.page)).size !== diagramPages.length || diagramPages.some(p => p.graphics !== 1)) failures.push({ diagramPages });
      return { pages: pages.length, headings: positions, failures, broken, splitWords, diagramPages, pageGeometry: pages.map(p => ({ page: p.dataset.pageNumber, classes: p.className, width: p.getBoundingClientRect().width, height: p.getBoundingClientRect().height, boxWidth: p.querySelector('.pagedjs_pagebox').getBoundingClientRect().width, boxHeight: p.querySelector('.pagedjs_pagebox').getBoundingClientRect().height })), repeatedTableHeaders: document.querySelectorAll('[data-repeated-header]').length, pageText };
    }, headings);
    await fs.writeFile(path.join(generated, 'layout-check.json'), JSON.stringify(layout, null, 2));
    if (layout.failures.length || layout.broken.length) throw new Error(`Layout/link validation failed; see generated/layout-check.json (${layout.failures.length} overflows, ${layout.broken.length} links).`);
    const pagedText = normalize(layout.pageText.join(''));
    for (const block of expectedBlocks) if (!pagedText.includes(normalize(block.text))) throw new Error(`Text lost during pagination: ${block.source}: ${block.text.slice(0, 100)}`);
    const paginatedSources = await page.evaluate(() => {
      const sources = {};
      for (const el of document.querySelectorAll('.pagedjs_pages [data-source]')) sources[el.dataset.source] = (sources[el.dataset.source] || '') + el.textContent;
      return sources;
    });
    assertSourceBlocks(expectedBlocks, paginatedSources, 'Paginated HTML');
    // Documents start on their own pages. Exclude cover, TOC, diagrams, appendix
    // introductions and neighboring documents from each source's PDF text region.
    const sectionIds = new Set([...documents.values()].map(d => d.headings[0].id));
    for (const diagram of diagramRecords) sectionIds.add(docId(diagram.file));
    sectionIds.add('appendix-evidence'); sectionIds.add('appendix-entry');
    const sectionPages = layout.headings.filter(h => sectionIds.has(h.id)).map(h => h.page);
    const sourceSections = [...documents.values()].map(d => {
      const startPage = layout.headings.find(h => h.id === d.headings[0].id).page;
      const endPage = Math.min(layout.pages + 1, ...sectionPages.filter(p => p > startPage)) - 1;
      return { source: d.file, startPage, endPage };
    });
    // Paged.js computes mixed page boxes; Chromium also needs native print-page
    // names on those boxes. Set these after pagination so Paged.js cannot rewrite
    // the physical-sheet rules. No PDF page replacement or rasterization is used.
    await page.evaluate(() => {
      for (const p of document.querySelectorAll('.pagedjs_page')) {
        const r = p.querySelector('.pagedjs_pagebox').getBoundingClientRect();
        p.style.width = `${r.width}px`; p.style.height = `${r.height}px`;
        const sheet = p.querySelector('.pagedjs_sheet');
        sheet.style.width = `${r.width}px`; sheet.style.height = `${r.height}px`;
        p.style.page = r.width > r.height ? 'physical-landscape' : 'physical-portrait';
      }
      const style = document.createElement('style');
      style.textContent = '@page physical-portrait{size:210mm 297mm;margin:0}@page physical-landscape{size:297mm 210mm;margin:0}';
      document.head.append(style);
    });
    const raw = await page.pdf({ preferCSSPageSize: true, printBackground: true, tagged: true });
    const pdf = await PDFDocument.load(raw);
    pdf.setTitle(L.title);
    pdf.catalog.set(PDFName.of('Lang'), PDFHexString.fromText(lang));
    pdf.setAuthor('CryptoScript Workbench');
    pdf.setSubject(`${L.subtitle}; ${L.revision}: ${revision}`);
    pdf.setCreationDate(date); pdf.setModificationDate(date);
    // Explicit outlines use the actual paginated heading pages, including SVG pages.
    // This avoids Chromium's treatment of cloned headings in paged HTML.
    const context = pdf.context;
    const outlineRef = context.nextRef();
    const items = layout.headings.filter(h => h.level <= 2);
    const tree = [];
    let parent;
    for (const heading of items) {
      const node = { ...heading, ref: context.nextRef(), children: [] };
      if (heading.level === 1 || !parent) { tree.push(node); parent = node; }
      else parent.children.push(node);
    }
    context.assign(outlineRef, context.obj({ Type: 'Outlines', First: tree[0].ref, Last: tree.at(-1).ref, Count: items.length }));
    function outlineSiblings(nodes, parentRef) {
      nodes.forEach((h, i) => {
        const item = context.obj({ Title: PDFHexString.fromText(h.title), Parent: parentRef, Dest: [pdf.getPages()[h.page - 1].ref, PDFName.of('Fit')] });
        if (i) item.set(PDFName.of('Prev'), nodes[i - 1].ref);
        if (i + 1 < nodes.length) item.set(PDFName.of('Next'), nodes[i + 1].ref);
        if (h.children.length) {
          item.set(PDFName.of('First'), h.children[0].ref); item.set(PDFName.of('Last'), h.children.at(-1).ref);
          item.set(PDFName.of('Count'), PDFNumber.of(h.children.length));
          outlineSiblings(h.children, h.ref);
        }
        context.assign(h.ref, item);
      });
    }
    outlineSiblings(tree, outlineRef);
    pdf.catalog.set(PDFName.of('Outlines'), outlineRef);
    pdf.catalog.set(PDFName.of('PageMode'), PDFName.of('UseOutlines'));
    await fs.writeFile(output, await pdf.save());
    for (const [file, bytes] of original) if (sha256(await fs.readFile(path.join(architecture, file))) !== sha256(bytes)) throw new Error(`Source changed during generation: ${file}`);
    const tooling = await Promise.all(['repeatability.mjs', 'publication.mjs', 'locales.mjs', 'bilingual-checks.mjs', 'bilingual.test.mjs', 'generate.mjs', 'validate.mjs', 'markdown.mjs', 'content-checks.mjs', 'regression.test.mjs', 'print.css', 'package.json', 'pnpm-lock.yaml', 'pnpm-workspace.yaml'].map(async file => ({ file, sha256: sha256(await fs.readFile(path.join(tools, file))) })));
    const manifest = { language: lang, schemaVersion: 2, documentationRevision: revision, branch, generatedAt: timestamp, sourceDateEpoch: epoch || null, workingTreeStatus: status, authoritativeSourceChanges: sourceStatus, chapters, evidence, diagrams: diagramRecords, sources: [...original].map(([file, bytes]) => ({ file, sha256: sha256(bytes), bytes: bytes.length })), baselines: chapters.map(file => ({ file, implementationBaseline: documents.get(file).baseline || null })), headings: layout.headings, links: linkRecords, sourceBlocks: expectedBlocks, sourceSections, layoutPages: layout.pages, warnings, nodeVersion: process.version, platform: process.platform, chromiumVersion: browser.version(), tooling, pdfSha256: sha256(await fs.readFile(output)) };
    await fs.writeFile(path.join(generated, 'generation-manifest.json'), JSON.stringify(manifest, null, 2));
    await validate(generated, lang);
    console.log(`Generated ${output}\n${layout.pages} pages; ${chapters.length} chapters; ${evidence.length} evidence records; ${diagrams.length} diagrams.`);
    warnings.forEach(w => console.warn(`Publication finding: ${w}`));
  } finally { await browser.close(); }

} catch (error) {
  await invalidate();
  throw error;
}
