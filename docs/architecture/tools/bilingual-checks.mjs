import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { publication } from './publication.mjs';
import { createMarkdown } from './markdown.mjs';

const md = createMarkdown();
const slug = text => text.toLowerCase().replace(/<[^>]*>/g, '').replace(/[^\p{L}\p{N}_\-\s]/gu, '').replace(/\s/g, '-');
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const read = (root, file) => fs.readFile(path.join(root, file), 'utf8');
function links(tokens) {
  return tokens.flatMap(t => [...(t.type === 'link_open' ? [t.attrGet('href')] : []), ...links(t.children || [])]);
}
function headingIds(tokens) {
  const counts = new Map();
  return tokens.filter(t => t.type === 'heading_open').map(t => {
    const token = tokens[tokens.indexOf(t) + 1];
    const base = slug(token.content), count = counts.get(base) || 0;
    counts.set(base, count + 1); return base + (count ? `-${count}` : '');
  });
}
export async function validateLanguageSources(root, lang) {
  const pub = await publication(root, lang);
  const registry = JSON.parse(await read(root, 'evidence/ar-01-source-register.json'));
  assert.equal(registry.baseline, pub.config.baseline, 'Registry baseline');
  // Fixtures may be nested inside a worktree. Resolve Git's actual root before
  // object lookup so Windows does not probe an overlong relative pathname.
  const repo = execFileSync('git', ['rev-parse', '--show-toplevel'], { cwd: path.resolve(root, '../..'), encoding: 'utf8' }).trim();
  const git = (...args) => execFileSync('git', args, { cwd: repo, stdio: ['ignore', 'pipe', 'pipe'] });
  let sourceCount = 0, linkCount = 0;
  for (const entry of registry.entries) for (const source of entry.sources) {
    assert.equal(source.commit, pub.config.baseline, 'Current source commit');
    const bytes = git('show', `${source.commit}:${source.path}`, '--');
    assert.equal(sha(bytes), source.sha256, `Source digest: ${source.path}`);
    assert.equal(bytes.toString().split('\n').length, source.lines, 'Source line count');
    assert.equal(source.url, `https://github.com/tkbss/CryptoWorkBench/blob/${source.commit}/${source.path}`, 'Immutable source URL');
    sourceCount++;
  }
  for (const file of registry.absentPaths) {
    let absent = false;
    try { git('cat-file', '-e', `${registry.baseline}:${file}`); } catch { absent = true; }
    assert.ok(absent, `Removed path exists: ${file}`);
  }
  for (const file of [...pub.chapters, ...pub.evidence, pub.readme]) {
    const text = await read(root, file), tokens = md.parse(text, {});
    if (pub.chapters.includes(file)) assert.ok(text.includes(pub.config.baseline), `Chapter baseline: ${file}`);
    for (const href of links(tokens)) {
      linkCount++;
      const pinned = href.match(/^https:\/\/github\.com\/tkbss\/CryptoWorkBench\/blob\/([a-f0-9]{40})\/([^#]+)(?:#L(\d+))?$/);
      if (pinned) {
        const bytes = git('show', `${pinned[1]}:${decodeURIComponent(pinned[2])}`, '--');
        if (pinned[3]) assert.ok(Number(pinned[3]) <= bytes.toString().split('\n').length, 'Source line target');
      } else if (/^[a-z][a-z0-9+.-]*:/i.test(href)) {
        assert.match(href, /^(https?:|mailto:)/i, 'Unsafe link scheme');
      } else {
        const [relative, fragment] = href.split('#');
        const target = relative ? path.posix.normalize(path.posix.join(path.posix.dirname(file), decodeURIComponent(relative))) : file;
        assert.ok(!target.startsWith('../'), 'Link escapes architecture');
        const body = await read(root, target);
        if (fragment) assert.ok(headingIds(md.parse(body, {})).includes(decodeURIComponent(fragment)), `Missing anchor: ${file}: ${href}`);
      }
    }
  }
  return { language: lang, sourceCount, linkCount, baseline: registry.baseline };
}
function structure(text) {
  const tokens = md.parse(text, {});
  return {
    numberedHeadings: tokens.filter(t => t.type === 'inline' && /^\d+\.(?:\d+\s|\s)/.test(t.content)).map(t => t.content.match(/^\d+\.(?:\d+)?/)[0]),
    headingLevels: tokens.filter(t => t.type === 'heading_open').map(t => t.tag),
    code: tokens.filter(t => t.type === 'fence').map(t => [t.info, t.content]),
    evidence: [...text.matchAll(/\[(R\d+)\]/g)].map(m => m[1]),
    links: links(tokens),
    paragraphs: tokens.filter(t => t.type === 'paragraph_open').length,
    tableRows: tokens.filter(t => t.type === 'tr_open').length,
    listItems: tokens.filter(t => t.type === 'list_item_open').length
  };
}
export async function assertParity(root) {
  const en = await publication(root, 'en'), de = await publication(root, 'de');
  for (let i = 0; i < en.chapters.length; i++) assert.deepEqual(structure(await read(root, en.chapters[i])), structure(await read(root, de.chapters[i])), `Translation structure: ${en.chapters[i]}`);
  const graph = text => text.split('\n').filter(s => !s.startsWith('%%')).join('\n').replace(/"[^"]*"/g, '"LABEL"');
  for (let i = 0; i < en.diagrams.length; i++) assert.equal(graph(await read(root, en.diagrams[i])), graph(await read(root, de.diagrams[i])), `Diagram topology: ${en.diagrams[i]}`);
  return { chapters: en.chapters.length, diagrams: en.diagrams.length, semanticTranslationReview: 'pending human review' };
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
  console.log(JSON.stringify({ parity: await assertParity(root), sources: [await validateLanguageSources(root, 'en'), await validateLanguageSources(root, 'de')] }, null, 2));
}
