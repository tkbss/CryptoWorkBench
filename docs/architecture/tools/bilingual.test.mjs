import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import { language, publication, outputDirectory } from './publication.mjs';
import { assertParity, validateLanguageSources } from './bilingual-checks.mjs';
import { assertCurrentSources } from './content-checks.mjs';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const files = ['CryptoScript-Workbench-Architecture.pdf', 'generation-manifest.json', 'validation.json'];
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
async function fixture(t) {
  const dir = await fs.mkdtemp(path.join(root, 'generated/bilingual-regression-'));
  const docs = path.join(dir, 'docs/architecture');
  await fs.mkdir(docs, { recursive: true });
  for (const name of ['chapters', 'de', 'diagrams', 'evidence']) await fs.cp(path.join(root, name), path.join(docs, name), { recursive: true });
  for (const name of ['publication.json', 'README.md']) await fs.copyFile(path.join(root, name), path.join(docs, name));
  await fs.mkdir(path.join(docs, 'tools'));
  for (const item of await fs.readdir(path.join(root, 'tools'), { withFileTypes: true })) if (item.isFile()) await fs.copyFile(path.join(root, 'tools', item.name), path.join(docs, 'tools', item.name));
  await fs.symlink(path.join(root, 'tools/node_modules'), path.join(docs, 'tools/node_modules'), process.platform === 'win32' ? 'junction' : 'dir');
  for (const lang of ['en', 'de']) {
    const out = outputDirectory(docs, lang); await fs.mkdir(out, { recursive: true });
    for (const file of files) await fs.copyFile(path.join(outputDirectory(root, lang), file), path.join(out, file));
  }
  t.after(async () => {
    assert.equal(path.dirname(path.resolve(dir)), path.join(root, 'generated'));
    assert.ok(path.basename(dir).startsWith('bilingual-regression-'));
    await fs.unlink(path.join(docs, 'tools/node_modules'));
    await fs.rm(dir, { recursive: true, force: true });
  });
  return docs;
}
test('CLI defaults to English and rejects unsupported options', () => {
  assert.equal(language([]), 'en'); assert.equal(language(['--lang', 'de']), 'de');
  for (const args of [['--lang', 'fr'], ['--lang'], ['--lang', '../'], ['--unknown'], ['--lang', 'en', 'extra']]) assert.throws(() => language(args), /Usage/);
});
test('both editions select only chapters 1–4 and separate output namespaces', async () => {
  for (const lang of ['en', 'de']) assert.deepEqual((await publication(root, lang)).chapters.map(f => Number(path.basename(f).slice(0, 2))), [1,2,3,4]);
  assert.notEqual(outputDirectory(root, 'en'), outputDirectory(root, 'de'));
});
test('chapter numbering, examples, evidence, links, block counts and diagram topology match', async () => { await assertParity(root); });
test('both source sets resolve against pinned Git objects and hashes', async () => {
  for (const lang of ['en', 'de']) await validateLanguageSources(root, lang);
});

// F-R1-01: structural EN/DE parity alone allowed both editions to lose these
// still-valid constraints. Guard their presence in the current chapter, not
// merely in the historical archive. This is retention, not semantic approval.
for (const lang of ['en', 'de']) test(`F1 retention: ${lang} redeclaration and ScriptBook constraints`, async () => {
  const text = await fs.readFile(path.join(root, lang === 'de' ? 'de/chapters/04-language-architecture.md' : 'chapters/04-language-architecture.md'), 'utf8');
  const section = (body, number) => body.match(new RegExp(`^## 4\\.${number} [\\s\\S]*?(?=^## |$(?![\\s\\S]))`, 'm'))?.[0] || '';
  const check = body => {
    const declarations = section(body, 6), constraints = section(body, 9);
    for (const identifier of ['VariableDeclarationEvaluator', 'VariableDictionary.Add', 'AssignmentEvaluator', 'VariableDictionary.Update']) assert.ok(declarations.includes(identifier), `${lang}: redeclaration/assignment evidence ${identifier}`);
    assert.match(declarations, lang === 'en' ? /Redeclaring an existing variable identifier replaces the existing dictionary entry/ : /erneute Deklaration eines vorhandenen Variablenbezeichners ersetzt den bestehenden Verzeichniseintrag/);
    assert.match(declarations, lang === 'en' ? /Subsequent assignment is a distinct operation/ : /nachfolgende Zuweisung ist eine eigenständige Operation/);
    assert.match(declarations, lang === 'en' ? /retains its declared type/ : /erhält dessen deklarierten Typ/);
    for (const identifier of ['WorkbenchSaveCommit.WriteSnapshot', 'Encoding.ASCII.GetBytes', 'CryptoScriptEditViewModel', 'File.ReadAllLinesAsync(filePath, pipeline.ExecutionToken)']) assert.ok(constraints.includes(identifier), `${lang}: ScriptBook evidence ${identifier}`);
    assert.match(constraints, lang === 'en' ? /Non-ASCII content[\s\S]*may be altered during serialization/ : /Nicht-ASCII-Inhalte[\s\S]*bei der Serialisierung verändert werden/);
    assert.match(constraints, lang === 'en' ? /does not restore characters lost during saving/ : /stellt beim Speichern verlorene Zeichen nicht wieder her/);
    assert.match(declarations, /\[R10\]/); assert.match(declarations, /\[R12\]/); assert.match(constraints, /\[R05\]/);
  };
  check(text);
  // Demonstrate that deleting either restored explanation is detected, even
  // though deleting it from both languages would still satisfy parity.
  for (const marker of ['VariableDictionary.Add', 'WorkbenchSaveCommit.WriteSnapshot']) {
    const without = text.split(/\r?\n\r?\n/).filter(p => !p.includes(marker)).join('\n\n');
    assert.throws(() => check(without), assert.AssertionError, `${lang}: omitted ${marker}`);
  }
  const register = JSON.parse(await fs.readFile(path.join(root, 'evidence/ar-01-source-register.json'), 'utf8'));
  const hostSources = register.entries.find(e => e.id === 'R05').sources.map(s => s.path);
  assert.ok(hostSources.includes('CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs'));
  assert.ok(hostSources.includes('CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs'));
});

for (const lang of ['en', 'de']) test(`${lang}: failed generation invalidates only selected output`, async t => {
  const docs = await fixture(t), other = lang === 'en' ? 'de' : 'en';
  const before = await Promise.all(files.map(async f => hash(await fs.readFile(path.join(outputDirectory(docs, other), f)))));
  await fs.writeFile(path.join(docs, lang === 'de' ? 'de/chapters/01-duplicate.md' : 'chapters/01-duplicate.md'), '# Duplicate\n');
  const result = spawnSync(process.execPath, [path.join(docs, 'tools/generate.mjs'), '--lang', lang], { encoding: 'utf8' });
  assert.notEqual(result.status, 0); assert.match(result.stderr, /Duplicate chapter numbers/);
  for (const f of files) await assert.rejects(fs.access(path.join(outputDirectory(docs, lang), f)), { code: 'ENOENT' });
  assert.deepEqual(await Promise.all(files.map(async f => hash(await fs.readFile(path.join(outputDirectory(docs, other), f))))), before);
});
test('missing German chapter fails closed without changing English output', async t => {
  const docs = await fixture(t); await fs.unlink(path.join(docs, 'de/chapters/01-introduction.md'));
  await assert.rejects(publication(docs, 'de'), /inventory differs/);
  await publication(docs, 'en');
});
test('translated source drift invalidates German manifest while English remains current', async t => {
  const docs = await fixture(t);
  await fs.appendFile(path.join(docs, 'de/chapters/01-introduction.md'), '\nÄnderung: Größe und Maß.\n');
  const manifest = async lang => JSON.parse(await fs.readFile(path.join(outputDirectory(docs, lang), 'generation-manifest.json'), 'utf8'));
  await assert.rejects(assertCurrentSources(await manifest('de'), docs), /Current source differs/);
  await assertCurrentSources(await manifest('en'), docs);
});
test('wrong-language manifest fails standalone validation', async t => {
  const docs = await fixture(t), file = path.join(outputDirectory(docs, 'de'), 'generation-manifest.json');
  const manifest = JSON.parse(await fs.readFile(file, 'utf8')); manifest.language = 'en'; await fs.writeFile(file, JSON.stringify(manifest));
  const result = spawnSync(process.execPath, [path.join(docs, 'tools/validate.mjs'), '--lang', 'de'], { encoding: 'utf8' });
  assert.notEqual(result.status, 0); assert.match(result.stderr, /Publication language mismatch/);
  await assert.rejects(fs.access(path.join(outputDirectory(docs, 'de'), 'validation.json')));
  await fs.access(path.join(outputDirectory(docs, 'en'), 'validation.json'));
});
