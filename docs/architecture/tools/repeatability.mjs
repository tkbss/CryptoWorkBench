import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import assert from 'node:assert/strict';
import { outputDirectory } from './publication.mjs';
const tools = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(tools, '..');
const run = args => new Promise((resolve, reject) => {
  const child = spawn(process.execPath, args, { cwd: path.resolve(root, '../..'), stdio: 'inherit', env: { ...process.env, SOURCE_DATE_EPOCH: process.env.SOURCE_DATE_EPOCH || '1793923200' } });
  child.on('error', reject); child.on('exit', code => code === 0 ? resolve() : reject(new Error(`Generation failed: ${code}`)));
});
const comparable = m => ({
  language: m.language, revision: m.documentationRevision, sources: m.sources, tooling: m.tooling,
  baselines: m.baselines, pages: m.layoutPages, headings: m.headings, links: m.links,
  blocks: m.sourceBlocks, sections: m.sourceSections,
  diagrams: m.diagrams.map(({ svgSha256, ...d }) => d)
});
const reports = [];
for (const lang of ['en', 'de']) {
  const directory = outputDirectory(root, lang);
  const read = async () => JSON.parse(await fs.readFile(path.join(directory, 'generation-manifest.json'), 'utf8'));
  await run([path.join(tools, 'generate.mjs'), '--lang', lang]);
  const first = await read();
  await fs.writeFile(path.join(directory, 'repeatability-first.json'), JSON.stringify(first, null, 2));
  await run([path.join(tools, 'generate.mjs'), '--lang', lang]);
  const second = await read();
  assert.deepEqual(comparable(first), comparable(second), `${lang}: semantic/layout reproducibility`);
  reports.push({ language: lang, passed: true, pages: second.layoutPages, pdfByteIdentical: first.pdfSha256 === second.pdfSha256,
    firstPdfSha256: first.pdfSha256, secondPdfSha256: second.pdfSha256,
    sourceDateEpoch: second.sourceDateEpoch, compared: Object.keys(comparable(second)) });
}
const readManifest = async lang => JSON.parse(await fs.readFile(path.join(outputDirectory(root, lang), 'generation-manifest.json'), 'utf8'));
const en = await readManifest('en'), de = await readManifest('de');
const topology = m => m.diagrams.map(d => ({ topology: d.topology, nodes: d.vertices.map(v => v.id), edges: d.edges.map(e => ({ from: e.from, to: e.to, type: e.type, stroke: e.stroke })), groups: d.boundaries.map(b => b.id) }));
assert.deepEqual(topology(en), topology(de), 'Rendered Mermaid topology parity');
const report = { passed: true, renderedTopologyParity: true, reports };
await fs.writeFile(path.join(root, 'generated/repeatability.json'), JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
