import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import path from 'node:path';

import { documentHeadings, inlineText, slug } from './markdown.mjs';

const metadata = {
  en: ['Metadata', 'Value', 'Implementation baseline', 'D1 update'],
  de: ['Metadaten', 'Wert', 'Implementierungsstand', 'D1-Ergänzung']
};
const evidenceId = /^[RE]\d+$/i;
const roleNames = Object.values(metadata).flatMap(labels => labels.slice(2));
const visible = tokens => tokens.filter(t => ['text', 'code_inline', 'image'].includes(t.type)).map(t => t.content).join('').trim();

export function validateMetadata(tokens, lang, declaration, baselines, file) {
  const message = 'Incorrect chapter provenance: ' + file;
  const [key, value, primary, supplement] = metadata[lang];
  const expected = [[primary, baselines[declaration.primary].commit],
    ...declaration.updates.map(name => [supplement, baselines[name].commit])];
  let row, table, beforeSection = true, metadataTable = false, tables = 0;
  const found = [];
  for (const t of tokens) {
    if (t.type === 'heading_open' && t.tag === 'h2') beforeSection = false;
    if (t.type === 'table_open') { table = true; metadataTable = false; }
    if (t.type === 'table_close') table = false;
    if (!table) continue;
    if (t.type === 'tr_open') row = [];
    if (t.type === 'inline') row.push(visible(t.children));
    if (t.type !== 'tr_close') continue;
    if (row[0] === key && row[1] === value) {
      assert.ok(beforeSection && row.length === 2, message);
      metadataTable = true;
      tables++;
    } else if (roleNames.includes(row[0]) || (metadataTable && row.slice(1).some(cell => /\b[a-f0-9]{40}\b/i.test(cell)))) {
      assert.ok(metadataTable && row.length === 2, message);
      found.push(row);
    }
  }
  assert.equal(tables, 1, message);
  assert.deepEqual(found.sort(), expected.sort(), message);
}

export function markdownLinks(tokens) {
  const result = [];
  let heading = '';
  for (const [index,t] of tokens.entries()) {
    if (t.type !== 'inline') continue;
    if (tokens[index-1]?.type === 'heading_open') heading = visible(t.children);
    const children = t.children;
    for (let i = 0; i < children.length; i++) {
      if (children[i].type !== 'link_open') continue;
      let end = i+1;
      while (end < children.length && children[end].type !== 'link_close') end++;
      result.push({href: children[i].attrGet('href'), label: inlineText({ children: children.slice(i+1,end) }).trim(), heading});
      i = end;
    }
  }
  return result;
}

export const physicalLines = bytes => {
  if (!bytes.length) return 0;
  const lines = bytes.toString().split(/\r\n|\n|\r/);
  return lines.length - (lines.at(-1) === '' ? 1 : 0);
};

export function checkReviewedRanges(definition, entries) {
  if (!definition.reviewedRangesSha256) {
    assert.ok(!entries.some(e => e.sources.some(s => s.ranges)), 'Missing reviewed range anchor');
    return;
  }
  const records = [];
  for (const entry of entries) {
    assert.ok(entry.sources.length > 0, 'Missing reviewed sources');
    for (const source of entry.sources) {
      assert.ok(source.ranges?.length, 'Missing reviewed ranges');
      for (const [start,end] of source.ranges) records.push([entry.id,source.commit,source.path,start,end].join('\t'));
    }
  }
  const digest = crypto.createHash('sha256').update(records.sort().join('\n') + '\n').digest('hex');
  assert.equal(digest, definition.reviewedRangesSha256, 'Reviewed evidence ranges differ from original input');
}

export function validateAppendix(tokens, entries, file) {
  const registered = new Set(entries.map(e => e.id));
  const headings = documentHeadings(tokens, file);
  const groups = headings.filter(h => evidenceId.test(slug(h.title)) || registered.has(h.title));
  assert.deepEqual(groups.map(h => h.title), entries.map(e => e.id), 'Evidence appendix group inventory');
  for (const {title, fragment} of groups) {
    assert.equal(fragment, title.toLowerCase(), 'Noncanonical evidence destination: ' + title);
  }
  const links = markdownLinks(tokens);
  if (entries.some(e => e.sources.some(s => s.ranges))) {
    for (const link of links) assert.ok(registered.has(link.heading), 'Reviewed appendix link outside evidence group');
  }
  for (const e of entries) {
    const expected = e.sources.map(s => {
      if (!s.ranges) return s.url;
      const [first,last] = s.ranges[0];
      return s.url + '#L' + first + (last === first ? '' : '-L' + last);
    });
    const actual = links.filter(l => l.heading === e.id).map(l => l.href.replace(/#L(\d+)-L\1$/, '#L$1'));
    assert.deepEqual(actual,expected,'Evidence appendix sources differ: '+e.id);
  }
}

export function validateCitation(link, file, pub, identities, permitted) {
  const hash = link.href.indexOf('#');
  const anchor = hash < 0 ? '' : decodeURIComponent(link.href.slice(hash+1));
  const relative = decodeURIComponent(link.href.split('#')[0]);
  const target = relative ? path.posix.join(path.posix.dirname(file), relative) : file;
  const claims = link.label.match(/(?<![\p{L}\p{N}_])[RE]\d+(?![\p{L}\p{N}_])/giu) || [];
  const internal = !/^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(link.href);
  if (!evidenceId.test(link.label) && !evidenceId.test(anchor) && !(internal && claims.length)) return;
  const ids = new Set(claims);
  const [id] = ids;
  const record = identities.get(id);
  assert.ok(ids.size === 1 && record && target === pub.edition.prefix + record.appendix &&
    anchor === id.toLowerCase(), 'Invalid evidence reference: ' + link.label);
  if (permitted) assert.ok(permitted.includes(record.name), 'Evidence outside chapter provenance');
}

export function checkSourceLink(link, file, pub, permitted, commit, sourcePath) {
  if (!permitted) return;
  const historical = (pub.config.historicalReferences || []).filter(ref =>
    ref.url.split('#')[0] === link.href.split('#')[0]);
  if (historical.length) {
    assert.ok(historical.some(ref => ref.purpose?.trim() && ref.chapter === path.basename(file) &&
      ref.url === link.href && link.heading.split(' ')[0] === ref.section && ref.labels[pub.language] === link.label),
    'Unregistered historical citation or incorrect purpose');
  } else {
    assert.ok(permitted.some(key => pub.config.baselines[key].commit === commit),
      'Implementation source outside chapter provenance: ' + sourcePath);
  }
}
