import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';

export function language(args = process.argv.slice(2)) {
  if (!args.length) return 'en';
  if (args.length !== 2 || args[0] !== '--lang' || !['en', 'de'].includes(args[1]))
    throw new Error('Usage: --lang en|de');
  return args[1];
}
export const outputDirectory = (root, lang) => path.join(root, 'generated', ...(lang === 'de' ? ['de'] : []));
export async function publication(root, lang) {
  assert.ok(['en', 'de'].includes(lang), 'Invalid publication language');
  const config = JSON.parse(await fs.readFile(path.join(root, 'publication.json'), 'utf8'));
  assert.equal(config.schemaVersion, 1);
  const edition = config.languages[lang];
  assert.ok(edition, 'Missing language edition');
  const chapters = config.chapters.map(f => edition.prefix + 'chapters/' + f);
  const diagrams = config.diagrams.map(f => edition.prefix + 'diagrams/' + f);
  const evidence = [edition.prefix + 'evidence/ar-01-baseline-reconciliation.md'];
  const readme = edition.prefix + 'README.md';
  const listed = async (dir, regex) => (await fs.readdir(path.join(root, edition.prefix, dir))).filter(f => regex.test(f)).sort();
  const actual = await listed('chapters', /^\d+-.*\.md$/);
  const nums = actual.map(f => Number(f.split('-')[0]));
  assert.equal(new Set(nums).size, nums.length, 'Duplicate chapter numbers');
  assert.deepEqual(actual.filter(f => !config.excludedChapters.includes(f)), [...config.chapters].sort(), 'Current-source inventory differs: chapters');
  assert.deepEqual(await listed('diagrams', /\.mmd$/), [...config.diagrams].sort(), 'Current-source inventory differs: diagrams');
  const sources = [...chapters, ...evidence, readme, ...diagrams, 'publication.json', 'evidence/ar-01-source-register.json'];
  for (const file of sources) {
    assert.ok(!path.isAbsolute(file) && !file.split('/').includes('..'), 'Source must stay inside architecture');
    try { await fs.access(path.join(root, file)); }
    catch { throw new Error(`Current-source inventory differs: missing ${file}`); }
  }
  return { config, edition, chapters, diagrams, evidence, readme, sources, language: lang };
}
