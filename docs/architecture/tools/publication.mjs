import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';

export const requiredTooling = Object.freeze(['provenance.mjs', 'diagram-model.mjs', 'repeatability.mjs', 'publication.mjs', 'locales.mjs', 'bilingual-checks.mjs', 'bilingual.test.mjs', 'generate.mjs', 'validate.mjs', 'markdown.mjs', 'content-checks.mjs', 'regression.test.mjs', 'print.css', 'package.json', 'pnpm-lock.yaml', 'pnpm-workspace.yaml']);

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
  assert.equal(config.schemaVersion, 2);
  const edition = config.languages[lang];
  assert.ok(edition, 'Missing language edition');
  const chapters = config.chapters.map(f => edition.prefix + 'chapters/' + f);
  const diagrams = config.diagrams.map(f => edition.prefix + 'diagrams/' + f);
  const baselines = Object.values(config.baselines);
  const evidence = baselines.map(b => edition.prefix + b.appendix);
  assert.deepEqual(Object.keys(config.provenance).sort(), [...config.chapters].sort(), 'Chapter provenance inventory');
  for (const {primary, updates} of Object.values(config.provenance)) {
    assert.ok(Array.isArray(updates) && new Set([primary, ...updates]).size === updates.length + 1, 'Duplicate chapter baseline');
    for (const id of [primary, ...updates]) assert.ok(Object.hasOwn(config.baselines, id), 'Unknown chapter baseline');
  }
  const readme = edition.prefix + 'README.md';
  const listed = async (dir, regex) => (await fs.readdir(path.join(root, edition.prefix, dir))).filter(f => regex.test(f)).sort();
  const actual = await listed('chapters', /^\d+-.*\.md$/);
  const nums = actual.map(f => Number(f.split('-')[0]));
  assert.equal(new Set(nums).size, nums.length, 'Duplicate chapter numbers');
  assert.deepEqual(actual.filter(f => !config.excludedChapters.includes(f)), [...config.chapters].sort(), 'Current-source inventory differs: chapters');
  assert.deepEqual(await listed('diagrams', /\.mmd$/), [...config.diagrams].sort(), 'Current-source inventory differs: diagrams');
  const sources = [...chapters, ...evidence, readme, ...diagrams, 'publication.json', ...baselines.map(b => b.register)];
  for (const file of sources) {
    assert.ok(!path.isAbsolute(file) && !file.split('/').includes('..'), 'Source must stay inside architecture');
    try { await fs.access(path.join(root, file)); }
    catch { throw new Error(`Current-source inventory differs: missing ${file}`); }
  }
  return { config, edition, chapters, diagrams, evidence, readme, sources, language: lang };
}
