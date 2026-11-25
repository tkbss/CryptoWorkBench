import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import { language, publication, outputDirectory } from './publication.mjs';
import { assertParity, validateLanguageSources } from './bilingual-checks.mjs';
import { validate } from './validate.mjs';
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
const expectedChapterNumbers = [1, 2, 3, 4, 8, 9];
test('both editions select the independently expected chapters and separate output namespaces', async () => {
  for (const lang of ['en', 'de']) assert.deepEqual((await publication(root, lang)).chapters.map(f => Number(path.basename(f).slice(0, 2))), expectedChapterNumbers);
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

test('mixed-baseline publication rejects invalid evidence, provenance and navigation', async t => {
  const docs = await fixture(t);
  async function rejected(file, mutate, check, expected) {
    const target = path.join(docs, file);
    const original = await fs.readFile(target, 'utf8');
    const changed = mutate(original);
    assert.notEqual(changed, original, 'Fixture mutation applied');
    await fs.writeFile(target, changed);
    try { await assert.rejects(check, expected); }
    finally { await fs.writeFile(target, original); }
  }
  const check = () => validateLanguageSources(docs, 'en');
  const register = 'evidence/mcp-a1-source-register.json';
  const changeSource = update => raw => {
    const data = JSON.parse(raw);
    update(data.entries[0].sources[0]);
    return JSON.stringify(data);
  };
  await rejected(register, changeSource(s => s.commit = '0'.repeat(40)), check, /Unknown source baseline/);
  await rejected(register, changeSource(s => s.sha256 = '0'.repeat(64)), check, /Source digest/);
  await rejected(register, changeSource(s => s.path = 'absent-documentation-fixture.cs'), check, /git show|Command failed/);
  const chapter = 'chapters/08-interfaces-and-integration.md';
  await rejected(chapter, s => s.replace('[E02]', '[E99]'), check, /Invalid evidence reference/);
  await rejected(chapter, s => s.replace(/(Implementation baseline \| `)[a-f0-9]{40}/, '$1' + '0'.repeat(40)), check, /Incorrect chapter provenance/);
  await rejected(chapter, s => s.replace('09-data-flows-and-execution-sequences.md)', '09-data-flows-and-execution-sequences.md#absent)'), check, /Missing anchor/);
});
test('translated diagrams retain message order, actor identity and state endpoints', async t => {
  const docs = await fixture(t);
  for (const [file, before, after] of [
    ['08-pairing', 'C->>P:', 'P->>C:'],
    ['08-editor-control', 'Releasing --> Gui:', 'Releasing --> Mcp:'],
    ['09-execution', '%% event approve', '%% event reordered'],
  ]) {
    const target = path.join(docs, 'de/diagrams', file + '.mmd');
    const original = await fs.readFile(target, 'utf8');
    const changed = original.replace(before, after);
    assert.notEqual(changed, original);
    await fs.writeFile(target, changed);
    try { await assert.rejects(assertParity(docs), /Diagram topology/); }
    finally { await fs.writeFile(target, original); }
  }
});

async function withChanges(docs, changes, action) {
  const saved = new Map();
  try {
    for (const [file, mutate] of changes) {
      const full = path.join(docs, file);
      const original = await fs.readFile(full);
      saved.set(full, original);
      const edited = mutate(original.toString());
      assert.notEqual(edited, original.toString(), 'Unapplied fixture: ' + file);
      await fs.writeFile(full, edited);
    }
    await action();
  } finally {
    for (const [full, bytes] of saved) await fs.writeFile(full, bytes);
  }
}
const editions = ['en','de'];
const chapterPath = (lang,num) => (lang === 'en' ? '' : 'de/') + 'chapters/' + ({
  1:'01-introduction.md', 8:'08-interfaces-and-integration.md', 9:'09-data-flows-and-execution-sequences.md'
})[num];
const sourcePrefix = 'https://github.com/tkbss/CryptoWorkBench/blob/';
const laterSource = sourcePrefix + '519d5048aa24523494a2749a4c799df993c91fcd/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs#L1';
const historySource = sourcePrefix + 'd7fddeb182a782c8085f066fbb64efea1e2e9581/docs/architecture/chapters/09-data-flows-and-execution-sequences.md#L43-L49';
function roleChanges(lang) {
  const [primary, extra] = lang === 'en' ? ['Implementation baseline','D1 update'] : ['Implementierungsstand','D1-Ergänzung'];
  const row = text => text.split('\n').find(s => s.startsWith('| ' + extra + ' |'));
  return {
    reversed: s => s.replace(primary,'TEMP').replace(extra,primary).replace('TEMP',extra),
    duplicate: s => s.replace(row(s), row(s)+'\n'+row(s)),
    missing: s => s.replace(row(s)+'\n',''),
    conflicting: s => s.replace(row(s),row(s)+'\n'+row(s).replace(extra,primary)),
    incorrect: s => s.replace(row(s),row(s).replace(/[a-f0-9]{40}/,'0'.repeat(40)))
  };
}
test('metadata roles, table cardinality and SHA identities are enforced in both editions', async t => {
  const docs = await fixture(t);
  for (const lang of editions) for (const [name,change] of Object.entries(roleChanges(lang))) {
    await t.test(lang+' '+name, () => withChanges(docs,[[chapterPath(lang,1),change]], () =>
      assert.rejects(validateLanguageSources(docs,lang), /Incorrect chapter provenance/)));
  }
});

test('resolved evidence citations enforce the same identity and chapter scope in either Markdown form', async t => {
  const docs = await fixture(t);
  for (const [label, target] of [
    ['E99','mcp-a1.md#e01'], ['E01','mcp-a1.md#e02'], ['R01','ar-01-baseline-reconciliation.md#r01'],
    ['E01','ar-01-baseline-reconciliation.md#e01'], ['E01','mcp-a1.md#missing']
  ]) for (const style of ['inline','reference']) {
    const url = '../evidence/'+target;
    const markdown = style==='inline' ? '['+label+']('+url+')' : '['+label+'][source]\n\n[source]: '+url;
    await t.test(label+' '+target+' '+style, () => withChanges(docs,[[chapterPath('en',8),s=>s+'\n'+markdown+'\n']], () =>
      assert.rejects(validateLanguageSources(docs,'en'), /Invalid evidence reference|Evidence outside chapter provenance/)));
  }
  await withChanges(docs,[[chapterPath('en',8),s=>s+'\n[**E01**][source]\n\n[source]: ../evidence/mcp-a1.md#e01\n']], () =>
    validateLanguageSources(docs,'en'));
});

test('direct implementation and historical citations have distinct source and purpose constraints', async t => {
  const docs = await fixture(t);
  const file = chapterPath('en',9);
  await validateLanguageSources(docs,'en'); // Includes an authorized implementation link and the F03 decision.
  for (const [name,change] of [
    ['later implementation', s=>s+'\n[Implementation]('+laterSource+')\n'],
    ['unregistered historical range',s=>s.replace(historySource,historySource.replace('L43-L49','L40-L43'))],
    ['historical evidence used as implementation',s=>s.replace('[historical chapter record]','[Implementation]')],
    ['historical citation in another section',s=>s+'\n[historical chapter record]('+historySource+')\n']
  ]) await t.test(name, () => withChanges(docs,[[file,change]], () =>
    assert.rejects(validateLanguageSources(docs,'en'), /outside chapter provenance|historical citation/)));
});

test('reviewed range anchor, physical lines and grouped appendix links', async t => {
  const docs=await fixture(t), register='evidence/mcp-a1-source-register.json';
  for (const [name,range] of [['in-bounds substitution',[2,2]],['reversed',[2,1]],['past end',[1,999999]],['phantom last line',null]]) {
    const change=s=>{const data=JSON.parse(s), source=data.entries[0].sources[0];source.ranges=[range || [source.lines,source.lines]];return JSON.stringify(data);};
    await t.test(name,()=>withChanges(docs,[[register,change]],()=>assert.rejects(validateLanguageSources(docs,'en'),/Invalid source range|Reviewed evidence ranges/)));
  }
  for(const lang of editions) for (const [name,sub] of [['wrong group','CryptoWorkBench.Mcp/PinnedMcpStdio.cs#L1-L31'],['wrong span','CryptoScript/CryptoScript.csproj#L1-L2']]) {
    const file=(lang==='de'?'de/':'')+'evidence/mcp-a1.md';
    await t.test(lang+' '+name,()=>withChanges(docs,[[file,s=>s.replace('CryptoScript/CryptoScript.csproj#L1-L31',sub)]],()=>
      assert.rejects(validateLanguageSources(docs,lang),/appendix sources/)));
  }
});

test('matching malformed editions fail source, bilingual and generation gates and invalidate old outputs',async t=>{
  const docs=await fixture(t);
  for(const [name,num,change,reason] of [
    ['wrong role',1,(s,lang)=>roleChanges(lang).reversed(s),/Incorrect chapter provenance/],
    ['E99',8,s=>s+'\n[E99][r1]\n\n[r1]: ../evidence/mcp-a1.md#e01\n',/Invalid evidence reference/],
    ['out-of-scope R01',8,s=>s+'\n[R01][r1]\n\n[r1]: ../evidence/ar-01-baseline-reconciliation.md#r01\n',/Evidence outside chapter provenance/],
    ['later commit',8,s=>s+'\n[Implementation]('+laterSource+')\n',/outside chapter provenance/]
  ]) await t.test(name,()=>withChanges(docs,editions.map(lang=>[chapterPath(lang,num),s=>change(s,lang)]),async()=>{
    await assert.rejects(assertParity(docs),reason);
    for(const lang of editions) {
      await assert.rejects(validateLanguageSources(docs,lang),reason);
      const out=outputDirectory(docs,lang);
      for(const f of files) await fs.copyFile(path.join(outputDirectory(root,lang),f),path.join(out,f));
      const run=spawnSync(process.execPath,[path.join(docs,'tools/generate.mjs'),'--lang',lang],{encoding:'utf8'});
      assert.notEqual(run.status,0);assert.match(run.stderr,reason);
      for(const f of files) await assert.rejects(fs.access(path.join(out,f)),{code:'ENOENT'});
    }
  }));
});

test('standalone tooling inventory fails closed in both editions',async t=>{
  const docs=await fixture(t);
  for(const lang of editions) {
    const out=outputDirectory(docs,lang), manifest=path.join(out,'generation-manifest.json');
    const original=await fs.readFile(manifest);
    for(const [name,mutate] of [
      ['empty',()=>[]], ['missing',a=>a.slice(1)], ['duplicate',a=>[...a,a[0]]],
      ['unexpected',a=>[...a,{file:'other',sha256:'0'.repeat(64)}]],
      ['digest',a=>a.map((e,i)=>i?e:{...e,sha256:'0'.repeat(64)})],
      ['changed stylesheet',a=>a], ['omitted inventory and changed stylesheet',()=>[]]
    ]) await t.test(lang+' '+name,async()=>{
      const m=JSON.parse(original);m.tooling=mutate(m.tooling);
      await fs.writeFile(manifest,JSON.stringify(m));
      await fs.writeFile(path.join(out,'validation.json'),'{"passed":true}');
      const check=()=>assert.rejects(validate(out,lang),/Required tooling inventory|Tooling differs/);
      if(name.includes('stylesheet')) await withChanges(docs,[['tools/print.css',s=>s+'\n/* drift */\n']],check);
      else await check();
      await assert.rejects(fs.access(path.join(out,'validation.json')),{code:'ENOENT'});
    });
    await fs.writeFile(manifest,original);
  }
});

test('resolved evidence claims in descriptive links and canonical appendix groups', async t => {
  const docs = await fixture(t);
  const cases = [
    ['unregistered', 'E99 (project references)', '../evidence/mcp-a1.md', /Invalid evidence reference/],
    ['missing group', 'Project (E01)', '../evidence/mcp-a1.md', /Invalid evidence reference/],
    ['wrong group', 'Project (**E01**)', '../evidence/mcp-a1.md#e02', /Invalid evidence reference/],
    ['wrong appendix', 'Project E01', '../evidence/ar-01-baseline-reconciliation.md#e01', /Invalid evidence reference/],
    ['wrong document', 'Project E01', '../README.md', /Invalid evidence reference/],
    ['unknown in navigation', 'Project E99', '../chapters/01-introduction.md', /Invalid evidence reference/],
    ['outside chapter', 'Historical **R01**', '../evidence/ar-01-baseline-reconciliation.md#%7201', /outside chapter provenance/],
    ['conflicting claims', 'E01 and E02', '../evidence/mcp-a1.md#e01', /Invalid evidence reference/],
    ['descriptive', 'Project (**E01**)', '../evidence/%6dcp-a1.md#%6501'],
    ['appendix navigation', 'Appendix', '../evidence/mcp-a1.md']
  ];
  for (const lang of editions) {
    for (const [name, label, destination, reason] of cases) {
      for (const style of ['inline', 'reference']) {
        const link = style === 'inline' ? '[' + label + '](' + destination + ')' :
          '[' + label + '][f2-check]\n\n[f2-check]: ' + destination;
        await t.test([lang, name, style].join(' '), () => withChanges(docs, [
          [chapterPath(lang, 8), text => text + '\n' + link + '\n']
        ], async () => {
          const check = validateLanguageSources(docs, lang);
          if (reason) await assert.rejects(check, reason);
          else await check;
        }));
      }
    }
    for (const extra of ['E01.', 'e01!', '**E01**']) {
      const appendix = (lang === 'de' ? 'de/' : '') + 'evidence/mcp-a1.md';
      await t.test(lang + ' conflicting heading ' + extra, () => withChanges(docs, [
        [appendix, text => text.replace('### E01', '### ' + extra + '\n\n### E01')]
      ], () => assert.rejects(validateLanguageSources(docs, lang), /Evidence appendix group inventory|Noncanonical evidence/)));
    }
  }
});

test('direct repository citations use authorized immutable file objects', async t => {
  const docs = await fixture(t);
  const repoUrl = 'https://github.com/tkbss/CryptoWorkBench';
  const file = 'CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs';
  const a1 = '8c58e2bd564280b71412cfd6ed9997688bd0bde4';
  const later = '519d5048aa24523494a2749a4c799df993c91fcd';
  const invalid = /Invalid immutable source reference/;
  const variants = [
    [repoUrl + '/blame/' + later + '/' + file + '#L50', invalid],
    [repoUrl + '/blame/master/' + file, invalid],
    [repoUrl + '/blob/master/' + file, invalid],
    [repoUrl + '/tree/' + a1 + '/' + file, invalid],
    [repoUrl + '/raw/' + a1 + '/' + file, invalid],
    [repoUrl + '/%62lame/' + a1 + '/' + file, invalid],
    [repoUrl + '/issues/../blame/master/' + file, invalid],
    [repoUrl.replace('CryptoWorkBench', '%43ryptoWorkBench') + '/blame/master/' + file, invalid],
    ['https://raw.githubusercontent.com/tkbss/CryptoWorkBench/' + a1 + '/' + file, invalid],
    ['https://api.github.com/repos/tkbss/CryptoWorkBench/contents/' + file, invalid],
    ['https://media.githubusercontent.com/media/tkbss/CryptoWorkBench/master/' + file, invalid],
    [repoUrl + '/blob/' + later + '/' + file, /outside chapter provenance/],
    [repoUrl + '/blob/' + a1 + '/' + file + '#L999999', /Source line target/],
    [repoUrl + '/blob/' + a1 + '/' + file + '#L50'],
    [repoUrl + '/issues/1'],
    [repoUrl]
  ];
  for (const lang of editions) {
    for (const [url, reason] of variants) {
      await t.test(lang + ' ' + url, () => withChanges(docs, [
        [chapterPath(lang, 8), text => text + '\n[Pipeline](' + url + ')\n']
      ], async () => {
        const check = validateLanguageSources(docs, lang);
        if (reason) await assert.rejects(check, reason);
        else await check;
      }));
    }
    const label = lang === 'de' ? 'historische Kapitel' : 'historical chapter record';
    await withChanges(docs, [[chapterPath(lang, 9), s => s.replace('[' + label + ']', '[Pipeline]')]],
      () => assert.rejects(validateLanguageSources(docs, lang), /historical citation/));
  }
});

async function publicationFixture(t) {
  const docs = await fixture(t);
  for (const lang of editions) {
    const from = outputDirectory(root, lang), to = outputDirectory(docs, lang);
    for (const file of ['architecture.html', 'layout-check.json', 'diagram-layout-check.json', 'diagrams']) {
      await fs.cp(path.join(from, file), path.join(to, file), {recursive: true});
    }
    await validate(to, lang);
  }
  return docs;
}
async function editionHashes(docs, lang) {
  const directory = outputDirectory(docs, lang), hashes = {};
  const record = async (relative) => {
    for (const entry of await fs.readdir(path.join(directory, relative), {withFileTypes: true})) {
      if (!relative && entry.name === 'de') continue;
      const file = path.join(relative, entry.name);
      if (entry.isDirectory()) await record(file);
      else hashes[file] = hash(await fs.readFile(path.join(directory, file)));
    }
  };
  await record('');
  return hashes;
}
async function seedPublication(docs, lang) {
  const from = outputDirectory(root, lang), to = outputDirectory(docs, lang);
  for (const file of files) await fs.copyFile(path.join(from, file), path.join(to, file));
}
async function publicationState(docs, lang) {
  const state = {};
  for (const file of files) {
    try { state[file] = hash(await fs.readFile(path.join(outputDirectory(docs, lang), file))); }
    catch (error) { if (error.code !== 'ENOENT') throw error; state[file] = null; }
  }
  return state;
}
function command(docs, name, args) {
  const run = spawnSync(process.execPath, [path.join(docs, 'tools', name + '.mjs'), ...args],
    {encoding: 'utf8', timeout: 600000});
  assert.ifError(run.error);
  return run;
}
async function assertFailedCommand(docs, lang, name, reason) {
  const other = lang === 'en' ? 'de' : 'en';
  const before = await publicationState(docs, lang), opposite = await editionHashes(docs, other);
  assert.ok(Object.values(before).every(Boolean) && Object.values(opposite).every(Boolean));
  const result = command(docs, name, ['--lang', lang]);
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, reason);
  const after = await publicationState(docs, lang);
  const expected = {...before, 'validation.json': null};
  if (name === 'generate') for (const file of files) expected[file] = null;
  assert.deepEqual(after, expected);
  assert.deepEqual(await editionHashes(docs, other), opposite);
  console.log(JSON.stringify({command: name, lang, reason: reason.source, before, after, opposite}));
}
test('each R2 provenance finding fails its own publication gates', async t => {
  const docs = await publicationFixture(t);
  const source = 'https://github.com/tkbss/CryptoWorkBench/blame/519d5048aa24523494a2749a4c799df993c91fcd/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs#L50';
  for (const [name, file, edit, reason] of [
    ['descriptive E99', 'chapters/08-interfaces-and-integration.md', s => s + '\n[E99 (project references)][r2-id]\n\n[r2-id]: ../evidence/mcp-a1.md\n', /Invalid evidence reference/],
    ['fragment collision', 'evidence/mcp-a1.md', s => s.replace('### E01', '### E01.\n\nR2 appendix heading check.\n\n### E01'), /Evidence appendix group inventory/],
    ['later blame', 'chapters/08-interfaces-and-integration.md', s => s + '\n[Pipeline](' + source + ')\n', /Invalid immutable source reference/]
  ]) await t.test(name, () => withChanges(docs, editions.map(lang => [(lang === 'de' ? 'de/' : '') + file, edit]), async () => {
    await assert.rejects(assertParity(docs), reason);
    for (const lang of editions) {
      await assert.rejects(validateLanguageSources(docs, lang), reason);
      for (const edition of editions) await seedPublication(docs, edition);
      await assertFailedCommand(docs, lang, 'generate', reason);
      await seedPublication(docs, lang);
      // Match the fixture input bytes so standalone validation reaches the semantic gate.
      const manifestPath = path.join(outputDirectory(docs, lang), 'generation-manifest.json');
      const manifest = JSON.parse(await fs.readFile(manifestPath));
      const sourcePath = (lang === 'de' ? 'de/' : '') + file;
      const bytes = await fs.readFile(path.join(docs, sourcePath));
      Object.assign(manifest.sources.find(s => s.file === sourcePath), {bytes: bytes.length, sha256: hash(bytes)});
      await fs.writeFile(manifestPath, JSON.stringify(manifest));
      await assertFailedCommand(docs, lang, 'validate', reason);
    }
  }));
});

test('publication bootstrap isolates early failures and recovers after restoration', async t => {
  const docs = await publicationFixture(t);
  const tools = path.join(docs, 'tools');
  for (const lang of editions) {
    for (const [failure, file, reason] of [
      ['missing publication', 'publication.mjs', /ERR_MODULE_NOT_FOUND/],
      ['missing provenance', 'provenance.mjs', /ERR_MODULE_NOT_FOUND/],
      ['missing dependency', 'node_modules', /ERR_MODULE_NOT_FOUND/],
      ['invalid syntax', 'publication.mjs', /SyntaxError/],
      ['initialization', 'provenance.mjs', /F2 initialization fixture/]
    ]) await t.test(lang + ' ' + failure, async () => {
      const original = path.join(tools, file), backup = original + '.held';
      await fs.rename(original, backup);
      try {
        if (failure === 'invalid syntax') await fs.writeFile(original, 'export const = ;');
        if (failure === 'initialization') await fs.writeFile(original, "throw Error('F2 initialization fixture');\n" + await fs.readFile(backup, 'utf8'));
        for (const name of ['generate', 'validate']) {
          for (const edition of editions) await seedPublication(docs, edition);
          await assertFailedCommand(docs, lang, name, reason);
        }
      } finally {
        if (!failure.startsWith('missing')) await fs.unlink(original);
        await fs.rename(backup, original);
      }
    });
    await t.test(lang + ' restored tools and invalid arguments', async () => {
      const other = lang === 'en' ? 'de' : 'en', opposite = await editionHashes(docs, other);
      for (const name of ['generate', 'validate']) {
        const ok = command(docs, name, ['--lang', lang]);
        assert.equal(ok.status, 0, ok.stderr);
        const before = await publicationState(docs, lang);
        assert.ok(Object.values(before).every(Boolean));
        const invalid = command(docs, name, ['--lang', lang, '--extra']);
        assert.match(invalid.stderr, /Usage/);
        assert.notEqual(invalid.status, 0);
        assert.deepEqual(await publicationState(docs, lang), before);
        assert.deepEqual(await editionHashes(docs, other), opposite);
      }
    });
  }
});
