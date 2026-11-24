import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { chromium } from 'playwright';
import { createMarkdown } from './markdown.mjs';
import { assertCurrentSources, assertSourceBlocks } from './content-checks.mjs';

const tools = path.dirname(fileURLToPath(import.meta.url));
const architecture = path.resolve(tools, '..');
const generated = path.join(architecture, 'generated');
const publications = ['CryptoScript-Workbench-Architecture.pdf', 'generation-manifest.json', 'validation.json'];
const block = (text, kind = 'prose', source = 'example.md') => ({ source, text, kind });

// Test fixtures live only in ignored generated/. Never edit real documentation.
async function fixture(t) {
  const root = await fs.mkdtemp(path.join(generated, 'technical-regression-'));
  const docs = path.join(root, 'docs/architecture');
  const fixtureTools = path.join(docs, 'tools');
  const output = path.join(docs, 'generated');
  await fs.mkdir(fixtureTools, { recursive: true });
  await fs.mkdir(output);
  for (const name of ['chapters', 'evidence', 'diagrams', 'de']) await fs.cp(path.join(architecture, name), path.join(docs, name), { recursive: true });
  await fs.copyFile(path.join(architecture, 'publication.json'), path.join(docs, 'publication.json'));
  await fs.copyFile(path.join(architecture, 'README.md'), path.join(docs, 'README.md'));
  for (const entry of await fs.readdir(tools, { withFileTypes: true })) if (entry.isFile()) await fs.copyFile(path.join(tools, entry.name), path.join(fixtureTools, entry.name));
  await fs.symlink(path.join(tools, 'node_modules'), path.join(fixtureTools, 'node_modules'), process.platform === 'win32' ? 'junction' : 'dir');
  for (const name of [...publications, 'layout-check.json']) await fs.copyFile(path.join(generated, name), path.join(output, name));
  await fs.cp(path.join(generated, 'diagrams'), path.join(output, 'diagrams'), { recursive: true });
  t.after(async () => {
    // Verify absolute deletion target and unlink the dependency junction first.
    assert.equal(path.dirname(path.resolve(root)), path.resolve(generated));
    assert.ok(path.basename(root).startsWith('technical-regression-'));
    await fs.unlink(path.join(fixtureTools, 'node_modules'));
    await fs.rm(root, { recursive: true, force: true });
  });
  return { root, docs, fixtureTools, output };
}

function run(tool, cwd) {
  const result = spawnSync(process.execPath, [tool], { cwd, encoding: 'utf8', timeout: 600000, env: { ...process.env, SOURCE_DATE_EPOCH: '1792065600' } });
  assert.ifError(result.error);
  assert.notEqual(result.status, 0, `Expected command failure: ${result.stdout}`);
  assert.ok(!result.stdout.includes('Validation passed:'), 'Failure must not report validation success');
  return result;
}

async function absent(output) {
  for (const name of publications) await assert.rejects(fs.access(path.join(output, name)), { code: 'ENOENT' });
}

test('raw scripts, event handlers and dangerous HTML/Markdown links stay inert', async () => {
  const md = createMarkdown();
  assert.equal(md.options.html, false);
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage();
    await page.route('**/*', route => route.abort());
    const inputs = [
      '<script>globalThis.rawHtmlExecuted = true</script>',
      '<img src=x onerror="globalThis.rawHtmlExecuted=true">',
      '<svg onload="globalThis.rawHtmlExecuted=true"></svg>',
      '<a href="javascript:globalThis.rawHtmlExecuted=true">danger</a>',
      '<a href="&#x6a;avascript:alert(1)">encoded</a>',
      '<a href="data:text/html,<script>alert(1)</script>">data</a>',
      '[danger](javascript:alert%281%29)',
      '[encoded](jav&#x61;script:alert%281%29)',
      '[data](data:text/html;base64,PHNjcmlwdD4=)',
      '[vbscript](vbscript:msgbox%281%29)'
    ];
    for (const source of inputs) {
      const rendered = md.render(source);
      await page.setContent(rendered);
      assert.equal(await page.evaluate(() => globalThis.rawHtmlExecuted), undefined, source);
      assert.equal(await page.locator('script,img,svg,iframe,object,embed,a').count(), 0, source);
      // Markdown entity decoding affects displayed text, never creates elements.
      if (source.startsWith('<')) assert.equal((await page.locator('body').innerText()).trim(), source.replace('&#x6a;', 'j'));
    }
  } finally { await browser.close(); }
});

test('ordinary Markdown formatting, code, tables and safe links remain supported', () => {
  const html = createMarkdown().render('# Heading\n\n**Bold** and *italic* with `code`.\n\n| A | B |\n|---|---|\n| One | Two |\n\n[web](https://example.com) [local](chapters/01-introduction.md#scope) [mail](mailto:review@example.com)\n\n```text\n<script>literal code</script>\n```');
  for (const text of ['<h1>Heading</h1>', '<strong>Bold</strong>', '<em>italic</em>', '<table>', '<code>code</code>', 'href="https://example.com"', 'href="chapters/01-introduction.md#scope"', 'href="mailto:review@example.com"', '&lt;script&gt;literal code&lt;/script&gt;']) assert.ok(html.includes(text), text);
});

for (const stage of ['Prepared HTML', 'Paginated HTML', 'PDF']) {
  test(`${stage}: repeated prose requires separate output occurrences`, () => {
    const expected = [block('Repeated identical paragraph.'), block('Repeated identical paragraph.')];
    assert.throws(() => assertSourceBlocks(expected, { 'example.md': 'Repeated identical paragraph.' }, stage), /missing or out of order/);
    assertSourceBlocks(expected, { 'example.md': 'Repeated identical paragraph. Repeated identical paragraph.' }, stage);
  });
}

test('table-cell multiplicity cannot reuse one occurrence or overlapping text', () => {
  const expected = Array.from({ length: 3 }, () => block('Repeated finding identifier', 'table'));
  assert.throws(() => assertSourceBlocks(expected, { 'example.md': 'Repeated finding identifier Repeated finding identifier' }, 'PDF'), /required 3, found 2/);
  assertSourceBlocks(expected, { 'example.md': 'Repeated finding identifier '.repeat(3) }, 'PDF');
  assert.throws(() => assertSourceBlocks([block('aaa', 'table'), block('aaa', 'table')], { 'example.md': 'aaaa' }, 'PDF'), /required 2, found 1/);
});

test('prose order and per-source boundaries prevent borrowing other occurrences', () => {
  assert.throws(() => assertSourceBlocks([block('First paragraph.'), block('Second paragraph.')], { 'example.md': 'Second paragraph. First paragraph.' }, 'PDF'), /missing or out of order/);
  assert.throws(() => assertSourceBlocks([block('Same.'), block('Same.')], { 'example.md': 'Same.', 'another.md': 'Same.' }, 'PDF'), /missing or out of order/);
  assert.throws(() => assertSourceBlocks([block('Missing paragraph.')], { 'example.md': 'Other paragraph.' }, 'PDF'), /missing or out of order/);
});

test('standalone current-source validation rejects changed bytes and clears old pass report', async t => {
  const f = await fixture(t);
  await fs.appendFile(path.join(f.docs, 'README.md'), '\nChanged regression input.\n');
  const result = run(path.join(f.fixtureTools, 'validate.mjs'), f.root);
  assert.match(result.stderr, /Current source differs from manifest: README.md/);
  await assert.rejects(fs.access(path.join(f.output, 'validation.json')), { code: 'ENOENT' });
});

test('current-source validation rejects added and removed source files', async t => {
  const f = await fixture(t);
  const manifest = JSON.parse(await fs.readFile(path.join(f.output, 'generation-manifest.json'), 'utf8'));
  await assertCurrentSources(manifest, f.docs);
  const extra = path.join(f.docs, 'diagrams/99-extra.mmd');
  await fs.writeFile(extra, 'flowchart LR\n A-->B\n');
  await assert.rejects(assertCurrentSources(manifest, f.docs), /inventory differs/);
  await fs.unlink(extra);
  await fs.unlink(path.join(f.docs, manifest.sources[0].file));
  await assert.rejects(assertCurrentSources(manifest, f.docs), /inventory differs/);
});

test('changed input plus early generation failure removes the old publication', async t => {
  const f = await fixture(t);
  await fs.appendFile(path.join(f.docs, 'README.md'), '\nChanged regression input.\n');
  await fs.writeFile(path.join(f.docs, 'chapters/01-duplicate.md'), '# Duplicate chapter\n');
  const result = run(path.join(f.fixtureTools, 'generate.mjs'), f.root);
  assert.match(result.stderr, /Duplicate chapter numbers/);
  await absent(f.output);
  assert.match(run(path.join(f.fixtureTools, 'validate.mjs'), f.root).stderr, /ENOENT/);
});

test('dependency-load failure also invalidates a previous publication', async t => {
  const f = await fixture(t);
  await fs.unlink(path.join(f.fixtureTools, 'markdown.mjs'));
  assert.match(run(path.join(f.fixtureTools, 'generate.mjs'), f.root).stderr, /ERR_MODULE_NOT_FOUND/);
  await absent(f.output);
});

test('late failure after writing a new PDF/manifest removes all publication files', async t => {
  const f = await fixture(t);
  await fs.appendFile(path.join(f.docs, 'README.md'), '\nChanged regression input.\n');
  // Fail only after the real generator has written both new publication files.
  await fs.writeFile(path.join(f.fixtureTools, 'validate.mjs'), `import fs from 'node:fs/promises';\nimport path from 'node:path';\nexport async function validate(dir) {\n await fs.access(path.join(dir, 'CryptoScript-Workbench-Architecture.pdf'));\n const manifest = JSON.parse(await fs.readFile(path.join(dir, 'generation-manifest.json'), 'utf8'));\n if (!manifest.sourceSections) throw new Error('Fixture did not reach new publication');\n await fs.writeFile(path.join(dir, 'validation.json'), JSON.stringify({passed:true}));\n throw new Error('Injected post-publication validation failure');\n}\n`);
  const result = run(path.join(f.fixtureTools, 'generate.mjs'), f.root);
  assert.match(result.stderr, /Injected post-publication validation failure/);
  await absent(f.output);
});

test('PDF validation detects an expected duplicate whose second occurrence is absent', async t => {
  const f = await fixture(t);
  const file = path.join(f.output, 'generation-manifest.json');
  const manifest = JSON.parse(await fs.readFile(file, 'utf8'));
  const index = manifest.sourceBlocks.findIndex(b => b.kind === 'prose' && b.text.length > 150);
  assert.ok(index >= 0, 'Generate with occurrence accounting before running regression tests');
  manifest.sourceBlocks.splice(index + 1, 0, { ...manifest.sourceBlocks[index] });
  await fs.writeFile(file, JSON.stringify(manifest));
  assert.match(run(path.join(f.fixtureTools, 'validate.mjs'), f.root).stderr, /Source block missing or out of order|Source block multiplicity/);
  await assert.rejects(fs.access(path.join(f.output, 'validation.json')), { code: 'ENOENT' });
});
