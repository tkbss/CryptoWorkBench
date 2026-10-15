import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';

export const normalizeText = text => text.normalize('NFC').replace(/[\s\u00ad\u200b]/g, '');

// Count distinct non-overlapping occurrences within each source. Cover, TOC,
// diagrams and neighboring sources cannot supply missing source occurrences.
// Prose/code must also retain source order. Table order is intentionally exempt:
// approved finding records repeat/reposition the original column labels.
export function assertSourceBlocks(blocks, sourceText, stage) {
  const groups = new Map();
  const cursors = new Map();
  for (const block of blocks) {
    assert.equal(typeof sourceText[block.source], 'string', `${stage}: Missing source region: ${block.source}`);
    const needle = normalizeText(block.text);
    if (!needle) continue;
    const key = JSON.stringify([block.source, needle]);
    const group = groups.get(key) || { source: block.source, needle, required: 0 };
    group.required++;
    groups.set(key, group);
    if (block.kind !== 'table') {
      const text = normalizeText(sourceText[block.source]);
      const at = text.indexOf(needle, cursors.get(block.source) || 0);
      assert.ok(at >= 0, `${stage}: Source block missing or out of order: ${block.source}: ${block.text.slice(0, 100)}`);
      cursors.set(block.source, at + needle.length);
    }
  }
  for (const { source, needle, required } of groups.values()) {
    const text = normalizeText(sourceText[source]);
    let count = 0, cursor = 0, at;
    while ((at = text.indexOf(needle, cursor)) >= 0) { count++; cursor = at + needle.length; }
    assert.ok(count >= required, `${stage}: Source block multiplicity: ${source}: required ${required}, found ${count}: ${needle.slice(0, 100)}`);
  }
  return { groups: groups.size, blocks: blocks.length };
}

export async function assertCurrentSources(manifest, architecture) {
  const filesIn = async (dir, pattern) => (await fs.readdir(path.join(architecture, dir))).filter(f => pattern.test(f)).map(f => `${dir}/${f}`);
  const current = [...await filesIn('chapters', /^\d+-.*\.md$/), ...await filesIn('evidence', /\.md$/), 'README.md', ...await filesIn('diagrams', /\.mmd$/)].sort();
  assert.deepEqual(manifest.sources.map(s => s.file).sort(), current, 'Current-source inventory differs from generation manifest; regenerate the PDF');
  for (const source of manifest.sources) {
    const bytes = await fs.readFile(path.join(architecture, source.file));
    assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'), source.sha256, `Current source differs from manifest: ${source.file}; regenerate the PDF`);
    assert.equal(bytes.length, source.bytes, `Current-source byte length: ${source.file}`);
  }
}
