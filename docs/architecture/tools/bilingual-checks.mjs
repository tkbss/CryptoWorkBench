import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { publication } from './publication.mjs';
import { diagramModel } from './diagram-model.mjs';
import { createMarkdown, documentHeadings } from './markdown.mjs';

import { markdownLinks, validateMetadata, validateCitation, validateAppendix, checkSourceLink, checkReviewedRanges, physicalLines } from './provenance.mjs';

const immutableObjects = new Map();
const md = createMarkdown();
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const read = (root, file) => fs.readFile(path.join(root, file), 'utf8');
function links(tokens) {
  return tokens.flatMap(t => [...(t.type === 'link_open' ? [t.attrGet('href')] : []), ...links(t.children || [])]);
}
// Repository navigation is explicitly separate from implementation citations.
function validateExternalLink(href) {
  assert.match(href, /^(https?:|mailto:)/i, 'Unsafe link scheme');
  const url = new URL(href);
  const host = url.hostname.toLowerCase().replace(/\.$/, '');
  const pathname = path.posix.normalize(decodeURIComponent(url.pathname).replaceAll('\\', '/'));
  const repo = /^\/tkbss\/CryptoWorkBench(?:\.git)?(?:\/(.*))?$/i.exec(pathname);
  if (repo && /^(?:www\.)?github\.com$/.test(host)) {
    const route = (repo[1] || '').replace(/\/$/, '');
    const navigation = /^(?:|issues|pulls|discussions|releases|actions|(?:issues|pull|discussions)\/\d+|actions\/runs\/\d+(?:\/job\/\d+)?)$/;
    assert.ok(navigation.test(route), 'Invalid immutable source reference');
  }
  const rawFile = repo && /^(?:raw\.githubusercontent\.com|raw\.github\.com|codeload\.github\.com|github\.dev)$/.test(host);
  const apiFile = host === 'api.github.com' && /^\/repos\/tkbss\/CryptoWorkBench(?:\/|$)/i.test(pathname);
  const mediaFile = host === 'media.githubusercontent.com' && /^\/media\/tkbss\/CryptoWorkBench(?:\/|$)/i.test(pathname);
  assert.ok(!rawFile && !apiFile && !mediaFile, 'Invalid immutable source reference');
}
export async function validateLanguageSources(root, lang) {
  const pub = await publication(root, lang);
  const repo = execFileSync('git', ['rev-parse', '--show-toplevel'], {cwd: root, encoding: 'utf8'}).trim();
  const git = (...args) => execFileSync('git', args, {cwd: repo, stdio: ['ignore','pipe','pipe']});
  if (!immutableObjects.has(repo)) immutableObjects.set(repo, new Map());
  const objects = immutableObjects.get(repo);
  const object = (commit, file) => {
    const key = commit + ':' + file;
    if (!objects.has(key)) objects.set(key, git('cat-file', 'blob', key));
    return objects.get(key);
  };
  const identities = new Map();
  const appendices = new Map();
  let sourceCount = 0, linkCount = 0;
  for (const [name, definition] of Object.entries(pub.config.baselines)) {
    const registry = JSON.parse(await read(root, definition.register));
    assert.match(definition.commit, /^[a-f0-9]{40}$/);
    assert.equal(registry.baseline, definition.commit, 'Registry baseline');
    appendices.set(pub.edition.prefix + definition.appendix, registry.entries);
    for (const entry of registry.entries) {
      assert.ok(!identities.has(entry.id), 'Duplicate evidence identity');
      identities.set(entry.id, {name, appendix: definition.appendix});
      for (const source of entry.sources) {
        assert.equal(source.commit, registry.baseline, 'Unknown source baseline');
        const bytes = object(source.commit, source.path);
        assert.equal(sha(bytes), source.sha256, 'Source digest: ' + source.path);
        assert.equal(bytes.toString().split('\n').length, source.lines, 'Source line count');
        assert.equal(source.url, 'https://github.com/tkbss/CryptoWorkBench/blob/' + source.commit + '/' + source.path, 'Immutable source URL');
        const limit = physicalLines(bytes);
        let previous = 0;
        for (const range of source.ranges || []) {
          assert.ok(Array.isArray(range) && range.length === 2, 'Invalid source range');
          const [first,last] = range;
          assert.ok(first >= previous, 'Invalid source range order');
          previous = first;
          assert.ok(Number.isInteger(first) && Number.isInteger(last) && first > 0 && first <= last && last <= limit, 'Invalid source range');
        }
        sourceCount++;
      }
    }
    checkReviewedRanges(definition, registry.entries);
    for (const file of registry.absentPaths) {
      let absent = false;
      try { git('cat-file', '-e', registry.baseline + ':' + file); } catch { absent = true; }
      assert.ok(absent, 'Removed path exists: ' + file);
    }
  }
  for (const file of [...pub.chapters, ...pub.evidence, pub.readme]) {
    const text = await read(root, file), tokens = md.parse(text, {});
    const provenance = pub.config.provenance[path.basename(file)];
    const permitted = provenance && [provenance.primary, ...provenance.updates];
    if (provenance) validateMetadata(tokens, lang, provenance, pub.config.baselines, file);
    if (appendices.has(file)) validateAppendix(tokens, appendices.get(file), file);
    for (const link of markdownLinks(tokens)) {
      const {href} = link;
      validateCitation(link, file, pub, identities, permitted);
      linkCount++;
      const pinned = href.match(/^https:\/\/github\.com\/tkbss\/CryptoWorkBench\/blob\/([a-f0-9]{40})\/([^?#]+)(?:#L(\d+)(?:-L(\d+))?)?$/);
      if (pinned) {
        const sourcePath = pinned[2].split('/').map(segment => {
          const decoded = decodeURIComponent(segment);
          assert.ok(decoded && decoded !== '.' && decoded !== '..' && !/[/\\\0]/.test(decoded),
            'Invalid immutable source reference');
          return decoded;
        }).join('/');
        checkSourceLink(link, file, pub, permitted, pinned[1], sourcePath);
        const bytes = object(pinned[1], sourcePath);
        if (pinned[3]) assert.ok(+pinned[3] >= 1 && +(pinned[4] || pinned[3]) >= +pinned[3] && +(pinned[4] || pinned[3]) <= physicalLines(bytes), 'Source line target');
      } else if (/^[a-z][a-z0-9+.-]*:/i.test(href)) {
        validateExternalLink(href);
      } else {
        const [relative, fragment] = href.split('#');
        const target = relative ? path.posix.normalize(path.posix.join(path.posix.dirname(file), decodeURIComponent(relative))) : file;
        assert.ok(!target.startsWith('../'), 'Link escapes architecture');
        const body = await read(root, target);
        if (fragment) assert.ok(documentHeadings(md.parse(body, {}), target).some(h => h.fragment === decodeURIComponent(fragment)), `Missing anchor: ${file}: ${href}`);
      }
    }
  }
  return { language: lang, sourceCount, linkCount, baselines: Object.keys(pub.config.baselines) };
}
function structure(text) {
  const tokens = md.parse(text, {});
  return {
    numberedHeadings: tokens.filter(t => t.type === 'inline' && /^\d+\.(?:\d+\s|\s)/.test(t.content)).map(t => t.content.match(/^\d+\.(?:\d+)?/)[0]),
    headingLevels: tokens.filter(t => t.type === 'heading_open').map(t => t.tag),
    code: tokens.filter(t => t.type === 'fence').map(t => [t.info, t.content]),
    evidence: markdownLinks(tokens).map(l => l.label).filter(label => /^[RE]\d+$/.test(label)),
    links: links(tokens),
    paragraphs: tokens.filter(t => t.type === 'paragraph_open').length,
    tableRows: tokens.filter(t => t.type === 'tr_open').length,
    listItems: tokens.filter(t => t.type === 'list_item_open').length
  };
}
export async function assertParity(root) {
  const en = await publication(root, 'en'), de = await publication(root, 'de');
  for (let i = 0; i < en.chapters.length; i++) assert.deepEqual(structure(await read(root, en.chapters[i])), structure(await read(root, de.chapters[i])), `Translation structure: ${en.chapters[i]}`);
  for (let i = 0; i < en.diagrams.length; i++) {
    assert.deepEqual(diagramModel(await read(root, en.diagrams[i])).topology, diagramModel(await read(root, de.diagrams[i])).topology, 'Diagram topology: ' + en.diagrams[i]);
  }
  for (let i = 0; i < en.evidence.length; i++) assert.deepEqual(structure(await read(root, en.evidence[i])), structure(await read(root, de.evidence[i])), 'Evidence parity');
  const sources = [await validateLanguageSources(root, 'en'), await validateLanguageSources(root, 'de')];
  return { sources, chapters: en.chapters.length, diagrams: en.diagrams.length, semanticTranslationReview: 'pending human review' };
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
  const { sources, ...parity } = await assertParity(root);
  console.log(JSON.stringify({ parity, sources }, null, 2));
}
