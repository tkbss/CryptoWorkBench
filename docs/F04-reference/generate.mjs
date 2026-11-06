// Independent CWM v1 reference: ECMAScript JSON.stringify + Node crypto.
// Run: node docs/F04-reference/generate.mjs
import { createHmac, hkdfSync } from 'node:crypto';
import { writeFileSync } from 'node:fs';
const directory = new URL('./', import.meta.url);
const canonical = value => typeof value === 'object'
  ? '{' + Object.keys(value).sort().map(k => JSON.stringify(k) + ':' + canonical(value[k])).join(',') + '}'
  : JSON.stringify(value);
const keyHex = '0123456789ABCDEFFEDCBA9876543210';
const key = Buffer.from(keyHex, 'hex');
const source = Buffer.from([0, 1, 0, 128, 255, 13, 10]);
const hmac = (k, bytes) => createHmac('sha256', k).update(bytes).digest('hex').toUpperCase();
const fileMac = { file: 'input.bin', size: source.length, mechanism: 'HMAC-SHA256', parameters: {}, mac: hmac(key, source) };
const derived = hkdfSync('sha256', key, Buffer.from('CryptoWorkBench/CWM/v1/HKDF-SHA256'),
  Buffer.from('CryptoWorkBench/CWM/v1/metadataAuth/HMAC-SHA256'), 32);
const container = { format: 'CWM', version: 1, fileMac, metadataAuth: { mechanism: 'HMAC-SHA256',
  canonicalization: 'RFC8785-JCS', keyDerivation: 'HKDF-SHA256', mac: hmac(derived, canonical(fileMac)) } };
writeFileSync(new URL('reference.json', directory), JSON.stringify({ keyHex, sourceHex: source.toString('hex').toUpperCase(),
  canonical: canonical(fileMac), derivedKeyHex: Buffer.from(derived).toString('hex').toUpperCase(), container }, null, 2) + '\n');
const strings = ['', '\r', '1', '\u0080', 'ö', '€', '😀', 'דּ', '/\\"\b\t\n\f\r\u000f', '<>&\u2028\u2029', 'e\u0301', 'é'];
const vectors = [];
for (let i = 0; i < strings.length; i++) {
  const value = { z: { [strings[i]]: strings[(i + 1) % strings.length], a: strings[(i + 3) % strings.length] },
    file: strings[i], size: [0, 1, 9007199254740991, 65536][i % 4] };
  vectors.push({ input: JSON.stringify(value), expected: canonical(value) });
}
// Exercise every ASCII control-character escape, property ordering and no Unicode normalization.
const controls = { controls: String.fromCharCode(...Array.from({ length: 32 }, (_, i) => i)), 'é': 'é', 'e\u0301': 'e\u0301' };
vectors.push({ input: JSON.stringify(controls), expected: canonical(controls) });
writeFileSync(new URL('jcs-vectors.json', directory), JSON.stringify(vectors, null, 2) + '\n');
